/* CppHeader2CS was created by Ryan S White  (Updated: 6/27/2025, Created: 2014)
   
   Purpose: A tool to share simple constants, structs, and a few other items between a C/C++ and a C#
   project at compile time.  Despite it's name its not a full C to C# converter.  It is mostly a tool 
   to use for converting simple items found in a c header file to a c# file.
   
   License: MIT - Feel free to use however you wish. The author is not responsible for any damages 
   caused by this code. Use it at your own risk. Please feel free to contribute.
  
   Project link:  https://github.com/SunsetQuest/CppHeaderToCSharpConverter/
   Past publication: http://www.codeproject.com/Articles/800111/Passing-C-Cplusplus-Constants-e
   
   Visual studio usage: 
    1) Copy the CppHeader2CS.exe file to some location on your local drive.
    2) Open the Project Properties for the C# project and then navigate to the 'Build Events' section.
    3) In the Pre-build event command line enter something like the following: 
       C:\[tool location]\CppHeader2CS.exe "$(ProjectDir)MyInput.h" "$(ProjectDir)myCppSharedItems.cs"
       Depending on the needs, adjust the file names and locations above. Visual Studio helps with 
       this using the macros path locations.
*/
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CppHeader2CS;

partial class Program
{
    /// <summary> ConstDesc flags all the types a #define might be.</summary>
    [Flags]
    public enum ConstDesc
    {
        none = 0,
        canBeInt32 = 1 << 1,
        canBeFloat = 1 << 2,
        canBeDouble = 1 << 3,
        canBeBool = 1 << 4,
        canBeAny = ~0
    }

    /// <summary>Matches a variable declaration with an assignment</summary>
    public const string VarWithAssignment = @" 
           (public[\ \t\r\n]*?|protected[\ \t\r\n]*?|private[\ \t\r\n]*?)?
           (static[\ \t\r\n]*?)?
           (const[\ \t\r\n]*?)?
           (?<type>
             (((un)?signed)[\ \t\r\n]*?)? ((char|wchar_t|void)[\ \t\r\n]*?\*|short[\ \t\r\n]+int|long[\ \t\r\n]+(int|long)|int|char|short|long)
             |[a-zA-Z_][a-zA-Z0-9_]*
           )[\ \t\r\n]*
           (?<name>[a-zA-Z_][a-zA-Z0-9_]*)[\ \t\r\n]*
           (:[\ \t\r\n]*\d+[\ \t\r\n]*)? # match bit fields
           (?<post>.*?;[\t\ ]*(//[^\r?\n]*?(?=\r?\n))?)";

    /// <summary>
    /// This is the main parser RegEx. It is what is used to decode the file. For debugging, take out
    /// a section and debug it in a RegEx debugger.
    /// </summary>
    public const string mainParser = @"
            ( #### decode c++ version of enum;  EXAMPLE:enum myEnum{ red, green, blue };
              (?<=\r?\n|;|\}) [\ \t\r\n]*
              enum[\ \t\r\n]+?(class|[\ \t\r\n]+?struct[\ \t\r\n]+?)?(?<enum_name>[a-zA-Z_][a-zA-Z0-9_]*)[\ \t\r\n]*?
               \{[\ \t\r\n]*
                    (?<enum_rows>[^;}]*?)
               \}[\ \t\r\n]*?;[\ \t\r\n]*?([\ \t]*(?<comments>//.*?)(?:\r?\n))?
            )|( #### decode c version of enum;  EXAMPLE:typedef enum { red, green, blue } myEnum;
              (?<=\r?\n|;|\}) [\ \t\r\n]* 
              [\ \t\r\n]*typedef[\ \t\r\n]enum[\ \t\r\n]+?[\ \t\r\n]*
               \{[\ \t\r\n]*
                    (?<enum_rows>[^;}]*?)
               \}[\ \t\r\n]*(?<enum_name>[a-zA-Z_][a-zA-Z0-9_]*)[\ \t\r\n]*?;[\ \t\r\n]*?([\ \t]*(?<comments>//.*?)(?:\r?\n))?
            )|( #### Match defines without text EXAMPLE:#DEFINE test //test
              (?<=\r?\n) [\ \t]*
              \#DEFINE[\ \t]*
              (?<def_name>[a-zA-Z_][a-zA-Z0-9_]*)
              (\([\ \t]*
                (?<def_params>([a-zA-Z_][a-zA-Z0-9_]*)(\[\ \t]*,[\ \t]*(?!\)))?)+ 
              [\ \t]*\))?
              [\ \t]*(?<def_value>[^\r\n]*?)
              [\ \t]*(?<comments>//[^\r\n]*?)?(?:\r?\n) # define ends on line
            )|( #### Decode #DEFINE with value   EXAMPLE:#DEFINE MyDefine bla a,b //test
              (?<=\r?\n) [\ \t]*
              \#DEFINE[\ \t]*
              (?<def_name>[a-zA-Z_][a-zA-Z0-9_]+)
               [\ \t]*(?<comments>//.*?)?(?:\r?\n)  # define ends on line
            )|( #### Decode c/c++ Preprocessor directives   EXAMPLE:#ifndef __MY_INCLUDED__
              (?<=\r?\n)[\ \t]*(?<def_other>\#(if[\ \t\r\n]+!?defined|IFDEF|IFNDEF|IF|ELSE|ELIF[\ \t\r\n]|ENDIF|UNDEF|ERROR|
               LINE|PRAGMA[\ \t\r\n]+REGION|PRAGMA[\ \t\r\n]+ENDREGION))(?<def_stuff>.*?)(?:\r?\n)
            )|( #### Decode structs  EXAMPLE: struct Cat {int a; int b;}
              (?<=\r?\n|;|\}) [\ \t\r\n]*
              struct[\ \t\r\n]+(?<struct_name>[a-zA-Z_][a-zA-Z0-9_]*)
               [\ \t\r\n]*\{[\ \t\r\n]*
                (?<struct_rows>[^\}]*?)
               [\ \t\r\n]*\}[\ \t\r\n]*
              (?<struct_imp>[a-zA-Z_][a-zA-Z0-9_]*)?[\ \t\r\n]*;([\ \t]*(?<comments>//.*?)(?:\r?\n))?
            )|( #### Match: const constants  EXAMPLE:const int myNum = 5; static char *SDK_NAME = ""fast"";
              (?<=\r?\n|;|\}) " + VarWithAssignment + @"
            )|( ####  Match: Commands EXAMPLE:\\C2CS_Set_Namespace: mynamespace 
              //[\ \t]*(?<cmd>(C2CS_Set_Namespace|C2CS_Set_ClassName|C2CS_Class_Write|
              C2CS_NS_Write|C2CS_TOP_Write))[\ \t]?(?<cmd_val>.*?)(?:\r?\n)
            )";

    /// <summary>
    /// namespace_name is the namespace for the output.  "C2CS" is the default if no name 
    /// is specified using "\\C2CS_Set_Namespace myNamespace".
    /// </summary>
    public static string namespace_name = "C2CS";

    /// <summary>
    /// class_name is the class name where all the constants will be generated.  
    /// "Constants" is the default if no name is specified using "\\C2CS_Set_Class myClassName"
    /// </summary>
    public static string class_name = "Constants";

    /// <summary>
    /// This Dictionary holds a list of all the constant vars outputted along with the possible 
    /// types it can be.
    /// </summary>
    public static Dictionary<string, ConstDesc> constants = [];

    /// <summary>This is a dictionary of all the built-in and user defined types.
    /// The key is the c/c++ format and the value is the c# translation.</summary>
    public static SortedDictionary<string, string> typeConversions = new()
    {
        { "bool","bool"},
        { "char","sbyte"},
        { "char*","string"},
        { "double","double"},
        { "float","float"},
        { "int","int"},
        { "long","long"},
        { "longlong","long"},
        { "short","short"},
        { "shortint","short"},
        { "signedchar","sbyte"},
        { "signedint","int"},
        { "signedlong","uint"},
        { "signedlonglong","long"},
        { "signedshort","short"},
        { "unsignedchar","byte"},
        { "unsignedint","uint"},
        { "unsignedlong","uint"},
        { "unsignedlonglong","ulong"},
        { "unsignedshort","ushort"},
        { "void*","UIntPtr"},
        { "wchar_t","Char"},
        { "wchar_t*","string"},
    };

    private static void Main(string[] args)
    {
        System.Diagnostics.Stopwatch timer = new();
        timer.Start();

        // Uncomment the following line to test with a specific file instead of using command line arguments
        //args =
        //[
        //    "SampleInput.h", // Input file path
        //    "SampleOutput.cs" // Output file path (optional)
        //];

        // Display some simple help message if no options are given or "\?" or "-?"
        if (args.Length == 0)
        {
            Console.WriteLine("Error: No command line parameters supplied for CppHeader2CS");
            Console.WriteLine("If output_file is unspecified, it will be output to the console.");
            Console.WriteLine("usage: cppHeaderParse.exe input_file [output_file]");
            Console.WriteLine("help: cppHeaderParse.exe -h");
            return;
        }

        // Display help if "-?" or "-h" is passed
        if (args.Length == 1 && (args[0] == "-?" || args[0] == "-h"))
        {
            DisplayHelp();
            return;
        }

        // Read the input file
        string text;
        try
        {
            text = File.ReadAllText(args[0]) + "\r\n";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {args[0]}. Details: {ex}");
            Console.WriteLine("usage: cppHeaderParse.exe input_file [output_file]");
            Console.WriteLine("help: cppHeaderParse.exe -h");
            return;
        }

        // Remove all /*...*/ style comments
        text = RemoveAllAreaStyleCommentsRegex().Replace(text, string.Empty);

        // Create the three output containers, which will be merged at the end
        StringBuilder top_area = new("// Generated using CppHeader2CS\r\n");
        StringBuilder usings_area = new("\r\nusing System;\r\nusing System.Runtime.InteropServices;\r\n");
        StringBuilder ns_area = new(text.Length);
        StringBuilder class_area = new(text.Length);

        // Process the input file
        try
        {
            MatchCollection matches = MainParserRegex().Matches(text);

            foreach (Match match in matches)
            {
                // Check if the user wants to skip the source line using "// C2CS_SKIP"
                string comments = match.Groups["comments"].Value;
                if (comments.Contains("C2CS_SKIP"))
                {
                    // Skip this line
                }
                // Handle #define with a parameter
                else if (match.Groups["def_name"].Success && !match.Groups["def_params"].Success
                    && match.Groups["def_value"].Length > 0)
                {
                    MatchDefineWithParameters(class_area, match);
                }
                // Handle #define without a parameter
                else if (match.Groups["def_name"].Success && match.Groups["def_value"].Length == 0)
                {
                    _ = top_area.AppendLine("#define " + match.Groups["def_name"].Value + comments);
                }
                // Handle other predefinitions
                else if (match.Groups["def_other"].Success)
                {
                    string defType = RemoveWhitespace(match.Groups["def_other"].Value).ToLower();
                    if (defType == "#ifdef")
                    {
                        defType = "#if";
                    }
                    else if (defType == "#ifndef")
                    {
                        defType = "#if !";
                    }
                    else if (defType == "#ifdefined")
                    {
                        defType = "#if";
                    }
                    else if (defType == "#if!defined")
                    {
                        defType = "#if !";
                    }
                    else if (defType.EndsWith("endregion"))
                    {
                        defType = "#endregion";
                    }
                    else if (defType.EndsWith("region"))
                    {
                        defType = "#region";
                    }

                    _ = ns_area.AppendLine(defType + match.Groups["def_stuff"].Value);
                    _ = class_area.AppendLine(defType + match.Groups["def_stuff"].Value);

                    // Add an empty line after #endif or #endregion for clarity.
                    if (defType.StartsWith("#end"))
                    {
                        _ = ns_area.AppendLine();
                        _ = class_area.AppendLine();
                    }
                }
                // Handle structs
                else if (match.Groups["struct_name"].Success)
                {
                    // Build the struct
                    string structName = match.Groups["struct_name"].Value;

                    _ = ns_area.AppendLine("    public struct " + structName + "\r\n    {");

                    string stuff = match.Groups["struct_rows"].ToString() + "\r\n";
                    MatchCollection structRows = StructRowsRegex().Matches(stuff);
                    foreach (Match structRow in structRows)
                    {
                        if (!structRow.ToString().Contains("C2CS_SKIP"))
                        {
                            // Convert the variable to C# format
                            string? convertedVar = VarWithAssignmentConverter(structRow);
                            if (convertedVar != null)
                            {
                                _ = ns_area.AppendLine("        public " + convertedVar);
                            }
                        }
                    }

                    _ = ns_area.AppendLine("    } " + match.Groups["comments"].Value);

                    if (match.Groups["struct_imp"].Success && match.Groups["struct_imp"].Length > 0)
                    {
                        _ = class_area.AppendLine("        public static " + structName + " "
                                                + match.Groups["struct_imp"].Value + "; " + match.Groups["comments"].Value);
                    }

                    _ = ns_area.AppendLine();

                    // Add the struct type to the allowed type lists
                    typeConversions[structName] = structName;
                }
                // Handle constants
                else if (match.Groups["type"].Success)
                {
                    if (VarWithAssignmentConverter(match) is { } convertedVar)
                    {
                        _ = class_area.AppendLine("        public const " + convertedVar);
                    }
                }
                // Handle enums
                else if (match.Groups["enum_name"].Success)
                {
                    StringBuilder sb = new();
                    bool useFlags = false;
                    string name = match.Groups["enum_name"].ToString();
                    string rows = match.Groups["enum_rows"].ToString();
                    _ = sb.AppendLine("    public enum " + name);
                    _ = sb.AppendLine("    {");
                    MatchCollection enumRows = enumRowsRegex().Matches(rows);
                    bool lastCR = true;
                    for (int i = 0; i < enumRows.Count; i++)
                    {
                        // Get the current enum member GroupCollection
                        GroupCollection g = enumRows[i].Groups;

                        string memberName = g["name"].Value;
                        string initializer = g["init"].Value;
                        string comment = g["comment"].Value;
                        bool hasCR = g["CR"].Success;
                        bool hasInit = initializer.Length > 0;

                        useFlags |= hasInit;

                        if (lastCR)
                        {
                            _ = sb.Append(' ', 8);
                        }

                        _ = sb.Append(memberName);
                        if (hasInit)
                        {
                            _ = sb.Append(" = " + initializer);
                        }

                        if (i < enumRows.Count - 1 && memberName.Length > 0)
                        {
                            _ = sb.Append(", ");
                        }

                        _ = sb.Append(comment);
                        if (hasCR)
                        {
                            _ = sb.Append("\r\n");
                        }

                        lastCR = hasCR;
                    }

                    // If any C++ style values then enable flags
                    if (useFlags)
                    {
                        _ = ns_area.AppendLine("    [Flags]");
                    }

                    _ = ns_area.Append(sb + "\r\n    };\r\n\r\n");

                    // Add the enum type to the allowed type lists
                    typeConversions[name] = name;
                }
                // Handle C2CS commands
                else if (match.Groups["cmd"].Success)
                {
                    string cmd = match.Groups["cmd"].ToString();
                    string cmd_val = match.Groups["cmd_val"].ToString();

                    if (cmd_val == "(blank line)")
                    {
                        cmd_val = "";
                    }

                    if (cmd == "C2CS_Set_Namespace")
                    {
                        namespace_name = cmd_val;
                    }
                    else if (cmd == "C2CS_Set_ClassName")
                    {
                        class_name = cmd_val;
                    }
                    else if (cmd == "C2CS_NS_Write")
                    {
                        _ = ns_area.AppendLine(cmd_val);
                    }
                    else if (cmd == "C2CS_TOP_Write")
                    {
                        _ = top_area.AppendLine(cmd_val);
                    }
                    else if (cmd == "C2CS_Class_Write")
                    {
                        _ = class_area.AppendLine("        " + cmd_val);
                    }
                }
            }

            // Combine all the StringBuilder sections into "final"
            StringBuilder final = new(text.Length + 128);
            _ = final.Append(top_area);
            _ = final.Append(usings_area);
            _ = final.Append($"\r\n\r\nnamespace {namespace_name}\r\n{{\r\n");
            _ = final.Append(ns_area);
            _ = final.Append($"    class {class_name}\r\n    {{\r\n");
            _ = final.Append(class_area);
            _ = final.AppendLine("    }\r\n}");

            // Write to the output file if specified
            if (args.Length == 2)
            {
                // Only replace the file if something changed
                if (File.Exists(args[1]) && string.Compare(File.ReadAllText(args[1]), final.ToString()) == 0)
                {
                    Console.WriteLine("Info: Bypassing CppHeader2CS conversion because no changes were detected.");
                }
                else
                {
                    File.WriteAllText(args[1], final.ToString());
                }
            }
            else
            {
                Console.Write(final);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error when processing source file. Details: {ex}");
        }

        // Display the time it took for the conversion
        Console.WriteLine($"Info: CppHeader2CS conversion time: {timer.ElapsedMilliseconds}ms");
    }


    /// <summary>
    /// This functions takes variables with an assignment and converts them into C# format.
    /// </summary>
    /// <param name="match">The RegEx Match that contains [type], [name], and [post]</param>
    /// <returns>The C/C++ variable converted to C# format.</returns>
    private static string? VarWithAssignmentConverter(Match match)
    {
        string type = RemoveWhitespace(match.Groups["type"].ToString()); // Removes whitespace
        string name = match.Groups["name"].ToString();
        string post = match.Groups["post"].ToString();
        bool found = typeConversions.TryGetValue(type, out var csVersion);
        return found ? csVersion + " " + name + post : null;
    }


    /// <summary>
    /// This function processes a C/C++ #define with a parameter and appends it to the class area.
    /// </summary>
    private static void MatchDefineWithParameters(StringBuilder class_area, Match match)
    {
        string val = match.Groups["def_value"].Value.Trim();
        _ = val.ToLower();
        if (val[0] == '(' && val[^1] == ')')
        {
            val = val[1..^1];
        }

        string name = match.Groups["def_name"].Value;
        string comments = match.Groups["comments"].Value;

        // Check if the type is specified by the user
        Match specifiedType = commentsRegex().Match(comments);
        string newLine;
        if (specifiedType.Success)
        {
            newLine = specifiedType.Groups[1].ToString() + " " + name + " = " + val;
            AddVarName(name, ConstDesc.canBeFloat);
        }
        // Check if it can be parsed to int
        else if (int.TryParse(val, NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, null,
            out int outInt))
        {
            newLine = "int " + name + " = " + outInt;
            AddVarName(name, ConstDesc.canBeFloat | ConstDesc.canBeDouble | ConstDesc.canBeInt32);
        }
        // Check if it can be parsed to double
        else if (double.TryParse(val.TrimEnd('d', 'D'), out double _))
        {
            newLine = "double " + name + " = " + val;
            AddVarName(name, ConstDesc.canBeDouble);
        }
        // Check if it can be parsed to float
        else if (float.TryParse(val.TrimEnd('f', 'F'), out float _))
        {
            newLine = "float " + name + " = " + val;
            AddVarName(name, ConstDesc.canBeFloat | ConstDesc.canBeDouble);
        }
        // Check if it can be parsed to bool
        else if (bool.TryParse(val.ToLower(), out bool _))
        {
            newLine = "bool " + name + " = " + val.ToLower();
            AddVarName(name, ConstDesc.canBeBool);
        }
        // Check if it's a hexadecimal int
        else if (val.StartsWith("0x") && int.TryParse(val[2..], NumberStyles.AllowHexSpecifier,
            null, out _))
        {
            newLine = "int " + name + " = " + val;
            AddVarName(name, ConstDesc.canBeFloat | ConstDesc.canBeInt32);
        }
        // Attempt to parse expressions
        else
        {
            // Try to determine if it's an int or bool expression
            MatchCollection items;
            bool isBoolExpression = false;

            // Check for integer expressions
            items = checkForIntegerExpressionsRegex().Matches(val);

            bool isIntExpression = items.Count > 0;

            // Check for boolean expressions
            if (!isIntExpression)
            {
                items = CheckForBooleanExpressionsRegex().Matches(val);

                isBoolExpression = items.Count > 0;
            }

            if (isIntExpression)
            {
                ConstDesc allConst = ConstDesc.canBeAny;
                foreach (Capture item in items[0].Groups["id"].Captures)
                {
                    if (int.TryParse(item.Value, out _))
                    {
                        allConst &= ConstDesc.canBeInt32 | ConstDesc.canBeFloat | ConstDesc.canBeDouble;
                    }
                    else if (double.TryParse(item.Value.TrimEnd('d'), out _))
                    {
                        allConst &= ConstDesc.canBeDouble;
                    }
                    else if (float.TryParse(item.Value.TrimEnd('f'), out _))
                    {
                        allConst &= ConstDesc.canBeFloat | ConstDesc.canBeDouble;
                    }
                    else if (constants.TryGetValue(item.Value, out ConstDesc thisConstDesc))
                    {
                        allConst &= thisConstDesc;
                    }
                    else
                    {
                        allConst = ConstDesc.none;
                        break;
                    }
                }

                if (allConst.HasFlag(ConstDesc.canBeInt32))
                {
                    newLine = "int " + name + " = " + val;
                    AddVarName(name, ConstDesc.canBeInt32);
                }
                else if (allConst.HasFlag(ConstDesc.canBeFloat))
                {
                    newLine = "float " + name + " = " + val;
                    AddVarName(name, ConstDesc.canBeFloat);
                }
                else if (allConst.HasFlag(ConstDesc.canBeDouble))
                {
                    newLine = "double " + name + " = " + val;
                    AddVarName(name, ConstDesc.canBeDouble);
                }
                else
                {
                    newLine = "string " + name + " = \"" + val + "\"";
                    AddVarName(name, ConstDesc.none);
                }
            }
            else if (isBoolExpression)
            {
                ConstDesc allConst = ConstDesc.canBeAny;
                foreach (Capture item in items[0].Groups["id"].Captures)
                {
                    if (bool.TryParse(item.Value, out _))
                    {
                        allConst &= ConstDesc.canBeBool;
                    }
                    else if (constants.TryGetValue(item.Value, out ConstDesc thisConstDesc))
                    {
                        allConst &= thisConstDesc;
                    }
                    else
                    {
                        allConst = ConstDesc.none;
                        break;
                    }
                }

                if (allConst.HasFlag(ConstDesc.canBeBool))
                {
                    newLine = "bool " + name + " = " + val;
                    AddVarName(name, ConstDesc.canBeBool);
                }
                else
                {
                    newLine = "string " + name + " = \"" + val + "\"";
                    AddVarName(name, ConstDesc.none);
                }
            }
            else
            {
                newLine = "string " + name + " = \"" + val + "\"";
                AddVarName(name, ConstDesc.none);
            }
        }
        _ = class_area.Append("        public const " + newLine);
        _ = class_area.AppendLine(comments.Length > 0 ? "; " + comments : ";");
    }

    private static void AddVarName(string name, ConstDesc vall)
    {
        if (!constants.TryAdd(name, vall))
        {
            Console.WriteLine($"Warning: '{name}' is defined more than once. This can be normal when using preprocessor directives.");
        }
    }

    /// <summary>
    /// Displays the help message when no arguments are provided.
    /// </summary>
    private static void DisplayHelp()
    {
        Console.WriteLine("""
        CppHeader2CS.exe in_file [out_file]
          in_file  - A C/C++ header file to convert to c# format for sharing.
          out_file - [optional] A cs file that is typically part of a C# project.
        
        Usage Instructions:
         1.	Copy the CppHeader2CS.exe file to some location
         2.	Open the Project properties for your project and go to "Build Events Section".
         3.	In the Pre-build event command line, enter something like the following:
        
         [path]\CppHeader2CS.exe "$(ProjectDir)MyInput.h" "$(ProjectDir)myCppSharedVars.cs"
        
         Depending on your needs the above paths will need to be modified. 
         Visual studio's macros button can help.
        
        Useful modifiers List: (located in C/C++ source file in comments)
         C2CS_TOP_Write text to write 
           This is a direct pass-through that writes text above the namespace. 
           It can be useful whenever there is a need to print at the top of the 
           output file.  
           Example: //C2CS_TOP_Write using System.IO;
         C2CS_NS_Write text to write
           This is a direct pass-through that writes text above the default 
           class but in the namespace area. This area usually contains enums 
           and structs.
         C2CS_Class_Write text to write 
           This is a direct pass-through that writes text in the class area. 
           It writes only to a single class.
         C2CS_Set_Namespace MyNsName
           This sets the namespace name.  It defaults to C2CS if unspecified.
         C2CS_Set_ClassName MyClass
           This sets the class name to use. If unspecified, defaults to "Constants".
         C2CS_TYPE MyType
           This is used in the comments after a #define to specify a type to use. 
           It is required if the automatic detection does not work as expected or 
           the programmer wants to force a type. 
           Example: #Default mySum (2+1) //C2CS_TYPE int;
         C2CS_SKIP
           Adding C2CS_SKIP in a c# comment forces the line to be ignored.
    """);
    }

    /// <summary>
    /// A fast method that removes any whitespace in a string.
    /// source: Felipe R. Machado 2015 http://www.codeproject.com/Articles/1014073/Fastest-method-to-remove-all-whitespace-from-Strin
    /// </summary>
    public static string RemoveWhitespace(string str)
    {
        var src = str.ToCharArray();
        int dstIdx = 0;
        for (int i = 0; i < str.Length; i++)
        {
            char ch = src[i];
            if (!(char.IsWhiteSpace(ch) || ch == '\u2028' || ch == '\u2029' || ch == '\u0085'))
            {
                src[dstIdx++] = ch;
            }
        }
        return new string(src, 0, dstIdx);
    }

    [GeneratedRegex(mainParser, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace, "en-US")]
    private static partial Regex MainParserRegex();

    // Remove all /*...*/ style comments
    [GeneratedRegex(@"/\*[^*]*\*+(?:[^*/][^*]*\*+)*/")]
    private static partial Regex RemoveAllAreaStyleCommentsRegex();

    [GeneratedRegex(VarWithAssignment, RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex StructRowsRegex();

    [GeneratedRegex(@"
[\ \t]*(?:
(?<CR>\r?\n)
|(?:(?<comment>//[^\r\n]*?)(?<CR>\r?\n)) # comment only
|(?:
    (?: # Match memberName with an optional initializer (Ex: blue = 1 )
        (?<name>[a-zA-Z_]\w*)   # Match memberName
        (?:[\ \t]|(?<CR>\r?\n))*                     # Any Whitespace
        (?:=[\ \t\r\n]*(?<init>[\w\+\-\*/]+)[\ \t\r\n]*)? # Get '=' and initializer
    )
    (?: # The 2nd half should either be a comment, a CR, or Last Row
        (,[\ \t]*(?<comment>//[^\r\n]*?)(?<CR>\r?\n)) # Ex: ',//some Notes'
        | (?<CR>,[\ \t]*\r?\n) # Match newlines endings Ex:',[CR/LF]'
        | (?:,(?=[\ \t\r\n]*[a-zA-Z_]\w*[\ \t\r\n]*(?:=[\ \t\r\n]*(?:[\w\+\-\*/]+)[\ \t\r\n]*)?(?://[^\r\n]*?|[\ \t\r\n])*(,|$)))
        | (?: # Match Last line without comma. Can be comments or whitespace.
            (?:
                (?:(?<comment>//[^\r\n]*?)) # Any comments or whitespace
                | [\ \t\r\n]                   # Any Whitespace
                # |(?<CR>\r?\n)
            )+
            $                         # Match End
            )
        )
    )
)", RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex enumRowsRegex();


    [GeneratedRegex(@"C2CS_TYPE:[\ \t\r\n]?((?:(?:(?!\d)\w+(?:\.(?!\d)\w+)*)\.)?(?:(?!\d)\w+))(?:[\ \t\r\n].*|$)")]
    private static partial Regex commentsRegex();
    [GeneratedRegex(@"
^(
    (?:
        -?[\s\(]* # allow '-' and '\('
        (?<id>
            [a-zA-Z_][a-zA-Z0-9_]*  # match var name
            |\d+(?:\.\d*)? (?:d|D|f|F|u|l|ll|LL)?  # match number
            |0[xX][0-9a-fA-F]+b?
        )
        [\s\)]* # allow space and '\)'
        [\+\-\*\/] # operation
        [\s\(]* # allow space and '\('
    )*
    -?[\s\(]* # allow '-' and '('
    (?<id>
        [a-zA-Z_][a-zA-Z0-9_]*  # match var name
        |\d+(?:\.\d*)? (?:d|D|f|F|u|l|ll|LL)?  # match number
        |0[xX][0-9a-fA-F]+b?
    )
    [\s\)]* # allow space and ')'
)$", RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex checkForIntegerExpressionsRegex();
    [GeneratedRegex(@"
^(
    (?:
        [\s\(]*
        !*(true|false|(?<id>(?!\d)\w+))
        [\s\(\)]*
        (&&|\|\|)[\s\(\)]*
    )*
    !*(true|false|(?<id>(?!\d)\w+))
    [\s\)]*
)$", RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex CheckForBooleanExpressionsRegex();
}
