/* CppHeader2CS was created by Ryan White  (Updated: 10/12/2014)
   
   Purpose: A tool to share simple constants, structs, and a few other items between a C/C++ and a C#
   project at compile time.  Despite it's name its not a full C to C# converter.  It is mostly a tool 
   to use for converting simple items found in a c header file to a c# file.
   
   License: Code Project Open License (CPOL) 1.02 - Feel free to use however you wish in any 
   commercial or open source projects. The author is not responsible for any damages caused by this 
   software. Use it at your own risk. Please feel free to contribute by emailing fixes 
   or additions to s u n s e t q u e s t AT h o t m a i l . c o m.
  
   Project link:  http://www.codeproject.com/Articles/800111/Passing-C-Cplusplus-Constants-e
   
   Visual studio usage: 
    1) Copy the CppHeader2CS.exe file to some location
    2) Open the Project Properties for the C# project and then navigate to the Build Events Section.
    3) In the Pre-build event command line enter something like the following: 
       C:\[tool location]\CppHeader2CS.exe "$(ProjectDir)MyInput.h" "$(ProjectDir)myCppSharedItems.cs"
       Depending on your needs you will need to adjust the file names and locations above. Visual Studio 
       helps with this using the macros button.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace cppHeaderParse
{
    class Program
    {
        /// <summary> ConstDesc flags all the types a #define might be.</summary>
        [Flags]
        public enum ConstDesc
        {
            none = 0,
            canBeInt32 = 1 << 1,
            canBeFloat = 1 << 2,
            canBeDouble= 1 << 3,
            canBeBool =  1 << 4,
            canBeAny = ~0
        }
        
        /// <summary>Matches a variable declaration with an assignment</summary>
        public const string VarWithAssignment = @" 
           (public\s*?|protected\s*?|private\s*?)?
           (static\s*?)?
           (const\s*?)?
           (?<type>
             (((un)?signed)\s*?)? ((char|wchar_t|void)\s*?\*|short\s+int|long\s+(int|long)|int|char|short|long)
             |[a-zA-Z_][a-zA-Z0-9_]*
           )\s*
           (?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*
           (:\s*\d+\s*)? # match bit fields
           (?<post>.*?;[\t\ ]*(//[^\r\n]*?(?=\r\n))?)";

        /// <summary>
        /// This is the main parser RegEx.  It is what is used to decode the file.  For debugging, take out
        /// a section and debug it in a RegEx debugger.
        /// </summary>
        public const string mainParser = @"
            ( #### decode c++ version of enum;  EXAMPLE:enum myEnum{ red, green, blue };
              (?<=\r\n|;|\}) \s*
              enum\s+?(class|\s+?struct\s+?)?(?<enum_name>[a-zA-Z_][a-zA-Z0-9_]*)\s*?
               \{\s*?
                    (?<enum_rows>[^;}]*?)
               \}\s*?;\s*?([\ \t]*(?<comments>//.*?)\r\n)?
            )|( #### decode c version of enum;  EXAMPLE:typedef enum { red, green, blue } myEnum;
              (?<=\r\n|;|\}) \s* 
              \s*typedef\senum\s+?\s*
               \{\s*
                    (?<enum_rows>[^;}]*?)
               \}\s*(?<enum_name>[a-zA-Z_][a-zA-Z0-9_]*)\s*?;\s*?([\ \t]*(?<comments>//.*?)\r\n)?
            )|( #### Match defines without text EXAMPLE:#DEFINE test //test
              (?<=\r\n) [\ \t]*
              \#DEFINE[\ \t]*
              (?<def_name>[a-zA-Z_][a-zA-Z0-9_]*)
              (\([\ \t]*
                (?<def_params>([a-zA-Z_][a-zA-Z0-9_]*)(\[\ \t]*,[\ \t]*(?!\)))?)+ 
              [\ \t]*\))?
              [\ \t]*(?<def_value>[^\r\n]*?)
              [\ \t]*(?<comments>//[^\r\n]*?)?\r\n # define ends on line
            )|( #### Decode #DEFINE with value   EXAMPLE:#DEFINE MyDefine bla a,b //test
              (?<=\r\n) [\ \t]*
              \#DEFINE[\ \t]*
              (?<def_name>[a-zA-Z_][a-zA-Z0-9_]+)
               [\ \t]*(?<comments>//.*?)?\r\n  # define ends on line
            )|( #### Decode c/c++ Preprocessor directives   EXAMPLE:#ifndef __MY_INCLUDED__
              (?<=\r\n)[\ \t]*(?<def_other>\#(IFDEF|IFNDEF|IF|ELSE|ELIF\s|ENDIF|UNDEF|ERROR|
               LINE|PRAGMA\s+REGION|PRAGMA\s+ENDREGION))(?<def_stuff>.*?)\r\n
            )|( #### Decode structs  EXAMPLE: struct Cat {int a; int b;}
              (?<=\r\n|;|\}) \s*
              struct\s+(?<struct_name>[a-zA-Z_][a-zA-Z0-9_]*)
               \s*\{\s*
                (?<struct_rows>[^\}]*?)
               \s*\}\s*
              (?<struct_imp>[a-zA-Z_][a-zA-Z0-9_]*)?\s*;([\ \t]*(?<comments>//.*?)\r\n)?
            )|( #### Match: const constants  EXAMPLE:const int myNum = 5; static char *SDK_NAME = ""fast"";
              (?<=\r\n|;|\}) " + VarWithAssignment + @"
            )|( ####  Match: Commands EXAMPLE:\\C2CS_Set_Namespace: mynamespace 
              //[\ \t]*(?<cmd>(C2CS_Set_Namespace|C2CS_Set_ClassName|C2CS_Class_Write|
              C2CS_NS_Write|C2CS_TOP_Write))[\ \t]?(?<cmd_val>.*?)\r\n
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
        public static Dictionary<string, ConstDesc> consts = new Dictionary<string, ConstDesc>();

        /// <summary>This is a dictionary of all the built-in and user defined types.
        /// The key is the c/c++ format and the value is the c# translation.</summary>
        public static Dictionary<string, string> typeConversions = new Dictionary<string, string>()
        {
            { "bool","bool"},
            { "longlong","long"},
            { "char*","String"},
            { "wchar_t","Char"},
            { "wchar_t*","string"},
            { "unsignedchar","byte"},
            { "unsignedshort","ushort"},
            { "unsignedint","uint"},
            { "unsignedlong","uint"},
            { "unsignedlonglong","ulong"},
            { "signedchar","SByte"},
            { "signedshort","short"},
            { "signedint","int"},
            { "signedlong","uint"},
            { "signedlonglong","long"},
            { "char","SByte"},
            { "short","short"},
            { "shortint","short"},
            { "int","int"},
            { "long","long"},
            { "float","float"},
            { "double","double"},
            { "void*","UIntPtr"},
        };

       
        static void Main(string[] args)
        {
            
            // This is for timing - it can be commented out if desired
            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            
            // Display some simple help message if no options are given or "\?" or "-?"
            if (args.Length == 0)
            {
                Console.WriteLine("Error: No command line parameters supplied for CppHeader2CS");
                Console.WriteLine("usage: cppHeaderParse.exe input_file [output_file]");
                Console.WriteLine("If output_file is unspecified then the output is printed to the console.");
                return;
            }

            // Display some simple help message if no options are given or "\?" or "-?"
            if (args.Length == 1 && (args[0] == "-?" || args[0] == "-h"))
            {
                DisplayHelp();
                return;
            }

            // Let’s try and read in the file
            string text = "";
            try
            {
                text = File.ReadAllText(args[0]) + "\r\n";
            }
            catch (System.Exception ex)
            {
                Console.Write("Error: Error reading " + args[0] + ".  Details:" + ex.ToString());
                Console.WriteLine("usage: cppHeaderParse.exe input_file [output_file]");
            }

            // Let’s remove all the /*...*/ style comments
            text = Regex.Replace(text, @"/\*[^*]*\*+(?:[^*/][^*]*\*+)*/", String.Empty);

            // Let’s create the three output containers, these will be merged at the end
            StringBuilder top_area = new StringBuilder("// Generated using CppHeader2CS\r\n");
            StringBuilder usings_area = new StringBuilder("\r\nusing System;\r\nusing System.Runtime.InteropServices;\r\n");
            StringBuilder ns_area = new StringBuilder(text.Length);
            StringBuilder class_area = new StringBuilder(text.Length);     

            // Let’s now process the input file
            try
            {
                MatchCollection matches = Regex.Matches(text, mainParser, RegexOptions.Multiline |
                                RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace |
                                RegexOptions.ExplicitCapture);

                foreach (Match match in matches)
                {
                    // First, let’s check to see if the user wants to skip the source line using "// C2CS_SKIP"
                    string comments = match.Groups["comments"].Value;
                    if (comments.Contains("C2CS_SKIP"))
                    {
                        // we are going to do nothing here.
                        //Console.WriteLine("C2CS_SKIP time: " + timer.ElapsedMilliseconds + "ms");
                    }
                    // Second, see if it is a #define with a parameter, Example: #define myVal 8 OR #define myInt 8+8
                    else if (match.Groups["def_name"].Success && !match.Groups["def_params"].Success
                        && (match.Groups["def_value"].Length > 0))
                    {
                        MatchDefineWithParameters(class_area, match);
                        //Console.WriteLine("#define with a parameter time: " + timer.ElapsedMilliseconds + "ms");
                    }
                    // Next, let’s see if it is a #define without a parameter, Example: #define test
                    else if (match.Groups["def_name"].Success && match.Groups["def_value"].Length == 0)
                    {
                        top_area.AppendLine("#define " + match.Groups["def_name"].Value + comments);
                        //Console.WriteLine("#define without a parameter time: " + timer.ElapsedMilliseconds + "ms");
                    }
                    // Match other types of predefinitions, Example: #if (myDef && myDef)
                    else if (match.Groups["def_other"].Success)
                    {
                        string defType = match.Groups["def_other"].Value.ToLower();
                        if (defType == "#ifdef") defType = "#if";
                        else if (defType == "#ifndef") defType = "#if !";
                        else if (defType.EndsWith("endregion")) defType = "#endregion";
                        else if (defType.EndsWith("region")) defType = "#region";

                        ns_area.AppendLine(defType + match.Groups["def_stuff"].Value);
                        class_area.AppendLine(defType + match.Groups["def_stuff"].Value);
                        //Console.WriteLine("other predefinitions time: " + timer.ElapsedMilliseconds + "ms");
                    }
                    // Match structs,  Example: struct SomeStruct3 {char a; char b; char c;};
                    else if (match.Groups["struct_name"].Success)
                    {
                        //build the struct here
                        string structName = match.Groups["struct_name"].Value;

                        //ns_section.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
                        ns_area.AppendLine("    public struct " + structName + "\r\n    {");

                        string stuff = match.Groups["struct_rows"].ToString() + "\r\n";
                        MatchCollection temp2 = Regex.Matches(stuff, VarWithAssignment, RegexOptions.Multiline
                            | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
                        foreach (Match structRow in temp2)
                            if (!structRow.ToString().Contains("C2CS_SKIP"))
                            {
                                // Lets convert the Var to c# format, if it fails then don't add the converted value
                                string convertedVar = VarWithAssignmentConverter(structRow);
                                if (convertedVar != null)
                                    ns_area.AppendLine("        public " + convertedVar);
                            }

                        ns_area.AppendLine("    } " + match.Groups["comments"].Value);

                        if (match.Groups["struct_imp"].Success && match.Groups["struct_imp"].Length > 0)
                            class_area.AppendLine("        public static " + structName + " "
                                + match.Groups["struct_imp"].Value + "; " + match.Groups["comments"].Value);

                        ns_area.AppendLine();

                        // now add the struct type to the allowed type lists.
                        typeConversions.Add(structName, structName);

                        //Console.WriteLine("enum time: " + timer.ElapsedMilliseconds + "ms");
                    }
                    // Match constants,  Example: const int myVal = 55;
                    else if (match.Groups["type"].Success)
                    {
                        string convertedVar = VarWithAssignmentConverter(match);
                        if (convertedVar != null)
                            class_area.AppendLine("        public const " + convertedVar);
                        //Console.WriteLine("structs time: " + timer.ElapsedMilliseconds + "ms");
                    }
                    // Match enums
                    else if (match.Groups["enum_name"].Success)
                    {
                        StringBuilder sb = new StringBuilder();
                        bool useFlags = false;
                        string name = match.Groups["enum_name"].ToString();
                        string rows = match.Groups["enum_rows"].ToString() + ",";
                        sb.AppendLine("    public enum " + name);
                        sb.AppendLine("    {");
                        MatchCollection items = Regex.Matches(rows,
                            @"\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*(?:\s*=\s*([^\,\r\n\{\}\;]*?))?\s*,");
                        foreach (Match r in items)
                        {
                            bool hasValue = r.Groups[2].Success;
                            useFlags |= hasValue;
                            sb.AppendLine("        " + r.Groups[1].ToString() +
                                (hasValue ? " = " + r.Groups[2].ToString() : "") + ",");
                        }
                        sb.AppendLine("    };");

                        // if any c++ style values then enable flags
                        if (useFlags)
                            ns_area.AppendLine("    [Flags]");

                        ns_area.Append(sb + "\r\n");

                        // now add the enum type to the allowed type lists.
                        typeConversions.Add(name, name);

                        //Console.WriteLine("enum time: " + timer.ElapsedMilliseconds + "ms");
                    }
                    // Match C2CS commands, Example: //C2CS_Set_ClassName myClass
                    else if (match.Groups["cmd"].Success)
                    {
                        string cmd = match.Groups["cmd"].ToString();
                        string cmd_val = match.Groups["cmd_val"].ToString();

                        if (cmd_val == "(blank line)") cmd_val = "";

                        if (cmd == "C2CS_Set_Namespace")
                            namespace_name = cmd_val;
                        else if (cmd == "C2CS_Set_ClassName")
                            class_name = cmd_val;
                        else if (cmd == "C2CS_NS_Write")
                            ns_area.AppendLine(cmd_val);
                        else if (cmd == "C2CS_TOP_Write")
                            top_area.AppendLine(cmd_val);
                        else if (cmd == "C2CS_Class_Write")
                            class_area.AppendLine("        " + cmd_val);
                        //Console.WriteLine("Match C2CS commands time: " + timer.ElapsedMilliseconds + "ms");
                    }
                    //Console.WriteLine(ns_section.ToString());
                }

                // Now let’s combine all the StringBuilder sections into "final"
                StringBuilder final = new StringBuilder(text.Length + 128);
                final.Append(top_area);
                final.Append(usings_area);
                final.Append("\r\n\r\nnamespace " + namespace_name + "\r\n{\r\n");
                final.Append(ns_area);
                final.Append("    class " + class_name + "\r\n    {\r\n");
                final.Append(class_area);
                final.AppendLine("    }\r\n}");

                // If an optional output parameter was given then write to that file or else just display
                if (args.Length == 2)
                {
                    // Normally we would just write the output to a file here but when a c# project is
                    // open, VS has a pop-up that prompts the user if they want to replace the file. This
                    // can be annoying. To fix this we only replace the file if something changed. One
                    // other advantage is it leaves the "date modified" when there are no changes.
                    if (File.Exists(args[1]) && String.Compare(File.ReadAllText(args[1]), final.ToString()) == 0)
                        Console.WriteLine("Info: Bypassing CppHeader2CS conversion because no changes were detected.");
                    else
                        File.WriteAllText(args[1], final.ToString());
                }
                else
                    Console.Write(final);
            }
            catch (System.Exception ex)
            {
                Console.Write("Error: Error when processing source file.  Details:" + ex.ToString());
            }

            // Display the time it took for the conversion
            Console.WriteLine("Info: CppHeader2CS conversion time: " + timer.ElapsedMilliseconds + "ms");
        }


        /// <summary>
        /// This functions takes variables with an assignment and converts them into c# format.
        /// </summary>
        /// <param name="match">The RegEx Match that contains [type], [name], and [post]</param>
        /// <returns>The c/c++ variable to c# format.</returns>
        private static string VarWithAssignmentConverter(Match match)
        {
            string type = Regex.Replace(match.Groups["type"].ToString(), @"\s", ""); //removes whitespace
            string name = match.Groups["name"].ToString();
            string post = match.Groups["post"].ToString();
            string csVersion = "";
            bool found = typeConversions.TryGetValue(type, out csVersion);
            return found ? csVersion + " " + name + post : null;
        }


        /// <summary>
        /// This function takes a RegEx Match of a C/C++ #define (with a parameter) and appends a "pubic 
        /// const SomeType someName" to a string builder.  This function will do its best to assign a type 
        /// from the #define. The user can also specify a type by adding "C2CS_TYPE:MyType" in the comments 
        /// (on the same line) after the #define. If no type can be determined then a string is used.
        /// </summary>
        /// <param name="class_area">The string builder to append the new line to.</param>
        /// <param name="match">A regex.Match with the three named groups: def_value, def_name, and comments.</param>
        private static void MatchDefineWithParameters(StringBuilder class_area, Match match)
        {
            string val = match.Groups["def_value"].Value.Trim();
            string valLower = val.ToLower();
            if (val[0] == '(' && val[val.Length - 1] == ')')
                val = val.Substring(1, val.Length - 2);
            string name = match.Groups["def_name"].Value;
            string comments = match.Groups["comments"].Value;
            int outInt;
            float outFloat;
            double outDouble;
            bool outBool;

            //////  Let’s first see if the type is specified by the user  //////
            Match specifiedType = Regex.Match(comments, 
                @"C2CS_TYPE:\s?((?:(?:(?!\d)\w+(?:\.(?!\d)\w+)*)\.)?(?:(?!\d)\w+))(?:\s.*|$)");
            if (specifiedType.Success)
            {
                class_area.AppendLine("        public const " + specifiedType.Groups[1].ToString() + " " 
                    + name + " = " + val + "; " + comments);
                AddVarName(name, ConstDesc.canBeFloat);
            }
            //////  Let’s see if it can be parsed to simple int  //////
            else if (Int32.TryParse(val, NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, null, 
                out outInt))
            {
                class_area.AppendLine("        public const int " + name + " = " + outInt + "; " + comments);
                AddVarName(name, ConstDesc.canBeFloat | ConstDesc.canBeDouble | ConstDesc.canBeInt32);
            }
            //////  Let’s see if it can be parsed to double  //////
            else if (double.TryParse(val.TrimEnd('d', 'D'), out outDouble))
            {
                class_area.AppendLine("        public const double " + name + " = " + val + "; " + comments);
                AddVarName(name, ConstDesc.canBeDouble);
            }               
            //////  Let’s see if it can be parsed to float  //////
            else if (float.TryParse(val.TrimEnd('f', 'F'), out outFloat))
            {
                class_area.AppendLine("        public const float " + name + " = " + val + "; " + comments);
                AddVarName(name, ConstDesc.canBeFloat | ConstDesc.canBeDouble);
            }
            //////  Let’s see if it can be parsed to simple boolean  //////
            else if (bool.TryParse(val.ToLower(), out outBool))
            {
                class_area.AppendLine("        public const bool " + name + " = " + val.ToLower() + "; " 
                    + comments);
                AddVarName(name, ConstDesc.canBeBool);
            }
            //////  Let’s see if it can be parsed to simple int  //////
            else if (val.StartsWith("0x")  && Int32.TryParse(val.Substring(2), NumberStyles.AllowHexSpecifier,
                null, out outInt))
            {
                class_area.AppendLine("        public const int " + name + " = " + val + "; " + comments);
                AddVarName(name, ConstDesc.canBeFloat | ConstDesc.canBeInt32);
            }            
            //////  Okay, no luck so far, let’s see if it might be a simple int or float expressions //////
            // Note: There are some expression tools in .net that can be used to make this section clean 
            //       and powerful but I'm afraid the performance is not going to be that great since it is 
            //       doing full expressions.
            else
            {
                MatchCollection items;
                bool isIntExpression = false, isBoolExpression = false;

                //can we recognize the expression as a bool or Int
                items = Regex.Matches(val, @"
^(?:
# Match as many as 'Val opp' as possible
-?[\s\(]*? # allow '-' and '('
(?<id>
  [a-zA-Z_][a-zA-Z0-9_]*  #match var name
  |\d+(?:\.\d*)? (?:d|D|f|F|u|l|ll|LL)?  #match number
  |0[xX][0-9a-fA-F]+b?
) #match var name
[\s\)]* # allow space and ')'
[\+\-\*\/] #  operation
[\s\(]* #needed?
)*
# get the final Val
-?[\s\(]*? # allow '-' and '('
(?<id>
  [a-zA-Z_][a-zA-Z0-9_]*  #match var name
  |\d+(?:\.\d*)? (?:d|D|f|F|u|l|ll|LL)?  #match number
  |0[xX][0-9a-fA-F]+b?
) #match var name
[\s\)]* # allow space and ')'
$", RegexOptions.IgnorePatternWhitespace);
                isIntExpression = (items.Count > 0);
                if (!isIntExpression)
                {
                    items = Regex.Matches(val, @"
                            ^([\s\(]*
                              !*(true|false|(?<id>(?!\d)\w+))
                                [\s\(\)]*(&&|\|\|)[\s\(\)]*
                              )*
                              !*(true|false|(?<id>(?!\d)\w+))
                            [\s\)]*$
                            ", RegexOptions.IgnorePatternWhitespace| RegexOptions.ExplicitCapture);
                    isBoolExpression = (items.Count > 0);
                }

                if (isIntExpression)
                {
                    ConstDesc allConst = (ConstDesc)int.MaxValue;
                    foreach (Capture item in items[0].Groups["id"].Captures)
                    {
                        ConstDesc thisConstDesc;
                        if (Int32.TryParse(item.Value, out outInt)) //todo: allow hex
                            allConst &= ConstDesc.canBeFloat | ConstDesc.canBeDouble | ConstDesc.canBeInt32;
                        else if (double.TryParse(item.Value.TrimEnd('d'), out outDouble))
                            allConst &= ConstDesc.canBeDouble;
                        else if (float.TryParse(item.Value.TrimEnd('f'), out outFloat))
                            allConst &= ConstDesc.canBeDouble | ConstDesc.canBeFloat;
                        else if (consts.TryGetValue(item.Value, out thisConstDesc))
                            allConst &= thisConstDesc;
                        else
                        {
                            allConst = ConstDesc.none;
                            break;
                        }
                    }

                    // we should have some types that are supported, let’s wee if int is first
                    if (allConst.HasFlag(ConstDesc.canBeInt32))
                    {
                        class_area.AppendLine("        public const int " + name + " = " + val + "; " + comments);
                        AddVarName(name, ConstDesc.canBeInt32);
                    }
                    // if we cannot use int then let’s try float
                    else if (allConst.HasFlag(ConstDesc.canBeFloat))
                    {
                        class_area.AppendLine("        public const float " + name + " = " + val + "; " + comments);
                        AddVarName(name, ConstDesc.canBeFloat);
                    }
                    // if we cannot use int then let’s try float
                    else if (allConst.HasFlag(ConstDesc.canBeDouble))
                    {
                        class_area.AppendLine("        public const double " + name + " = " + val + "; " + comments);
                        AddVarName(name, ConstDesc.canBeFloat);
                    }
                    // int or float did not work, let’s just default to a string
                    else
                    {
                        class_area.AppendLine("        public const string " + name + " = \"" + val + "\"; " + comments);
                        AddVarName(name, ConstDesc.none);
                    }
                }

                // is it a boolean expression
                if (isBoolExpression) 
                {
                    ConstDesc allConst = (ConstDesc)int.MaxValue;
                    foreach (Capture item in items[0].Groups["id"].Captures)
                    {
                        ConstDesc thisConstDesc;
                        if (bool.TryParse(item.Value, out outBool))
                            allConst &= ConstDesc.canBeBool;
                        else if (consts.TryGetValue(item.Value, out thisConstDesc))
                            allConst &= thisConstDesc;
                        else
                        {
                            allConst = ConstDesc.none;
                            break;
                        }
                    }

                    // let’s see if the int type was supported
                    if (allConst.HasFlag(ConstDesc.canBeBool))
                    {
                        class_area.AppendLine("        public const bool " + name + " = " + val + "; " + comments);
                        consts.Add(name, ConstDesc.canBeBool);
                    }
                    // if not, then let’s just default to a string
                    else
                    {
                        class_area.AppendLine("        public const string " + name + " = \"" + val + "\"; " + comments);
                        consts.Add(name, ConstDesc.none);
                    }
                }

                //the RegEx's could not match to an simple into or boolean expression, let’s default to string.
                if (!isBoolExpression && !isIntExpression)
                {
                    class_area.AppendLine("        public const string " + name + " = \"" + val + "\"; " + comments);
                    consts.Add(name, ConstDesc.none);
                }
            }
        }

        private static void AddVarName(string name, ConstDesc vall)
        {
            if (consts.ContainsKey(name))
                Console.WriteLine("Error: '" + name + "' already exists.");
            else
                consts.Add(name, vall);
        }

        private static void DisplayHelp()
        {
            Console.WriteLine("");
            Console.WriteLine("Command Line Usage");
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("CppHeader2CS.exe input_file [output_file]");
            Console.WriteLine("  input_file - this is the C/C++ header file.  It should be a simple file meant for the C/C++ to C# sharing.");
            Console.WriteLine("  output_file - this is an optional C# output file that is typically part of a C# project.");
            Console.WriteLine("");
            Console.WriteLine("Usage Instructions");
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine(" 1.	Copy the CppHeader2CS.exe file to some location");
            Console.WriteLine(" 2.	Open the Project Properties for the C# project and then navigate to the Build Events Section.");
            Console.WriteLine(" 3.	In the Pre-build event command line enter something like the following:");
            Console.WriteLine("");
            Console.WriteLine(@"     C:\[tool location]\CppHeader2CS.exe ""$(ProjectDir)MyInput.h"" ""$(ProjectDir)myCppSharedItems.cs""");
            Console.WriteLine("");
            Console.WriteLine("      Depending on your needs this the above paths will need to be modified.  Visual studio helps with this using the macros button.");
            Console.WriteLine("");
            Console.WriteLine("Command List (located in c/c++ source file in comments)");
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("// C2CS_TOP_Write text to write - C2CS_TOP_Write is a direct pass-through that writes text above the namespace. This can be useful whenever there is a need to print at the top of the output file. Example:  //C2CS_TOP_Write using System.IO;");
            Console.WriteLine("// C2CS_NS_Write text to write - C2CS_NS_Write is a direct pass-through that writes text above the default class but in the namespace area. This area usually contains enums and structs.");
            Console.WriteLine("// C2CS_Class_Write text to write - C2CS_Class_Write is a direct pass-through that writes text in the class area.  CppHeader2CS writes only to a single class.");
            Console.WriteLine("// C2CS_Set_Namespace MyNsName - C2CS_Set_Namespace sets the namespace name. This is optional and if unspecified defaults to C2CS.");
            Console.WriteLine("// C2CS_Set_ClassName MyClass - C2CS_Set_ClassName sets the class name to use. This is optional and if unspecified defaults to Constants.");
            Console.WriteLine("// C2CS_TYPE MyType - C2CS_TYPE is used in the comments after a #define to specify what type to use.  This is required if the automatic detection does not work as expected or the programmer wants to force a type. Example: #Default mySum (2+1) //C2CS_TYPE int;");
            Console.WriteLine("// C2CS_SKIP - Adding C2CS_SKIP in any comment forces CppHeader2CS to ignore the current line.");
        }
    }
}
