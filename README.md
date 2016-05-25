# CppHeaderToCSharpConverter
A compile-time tool for converting C/C++ header constants, enums, and structures to C#.

Site: http://www.codeproject.com/Articles/800111/Passing-C-Cplusplus-Constants-e 

Description: This command line tool extracts C/C++ constants, predefinitions, structs, and enums from a C/C++ header file and then outputs it to a C# file. This tool is not a full featured C/C++ to C# converter - it does not convert functions to C#. This tool is meant to copy header information from a C/C++ file to a C# file so that constants, predefinitions, structs, and enums can be shared between C/C++ project and C# projects. This conversion would typically be done using Visual Studio's pre-build command line events. The goal is to keep one set of constants and enums for both a C/C++ and C# project. These constants and enums are extracted from a given C/C++ file, converted, and are then written to a generated C# file that is usually already added to a C# project. The conversion is extremely quick so it should not have much impact on your total compile time.

##History
 - 5/30/2014 - First working program, #define support only
 - 6/20/2014 - Added more preprocessor support, structs and constant support
 - 6/27/2014 - Added support for source file commands
 - 7/12/2014 - Added #define auto-detect type support
 - 7/21/2014 - Added enum support
 - 7/26/2014 - Renamed from C2CS to CppHeader2CS (C2CS can be misinterpreted as a full C to C# converter)
 - 7/27/2014 - Initial post on CodeProject
 - 10/15/2014
   - same-file custom types are not supported.  "struct A { ... }; struct B { A a;...}" will convert.
   - decimals without suffix now default to double
   - added 'd' suffix support
   - added additional double support
   - added void* support
   - bitfield structs are accepted but bit portion is removed
   - other fixes and code cleanup
 - 5/6/2016
   - Added support to accept Linux/Mac OS X style Line-Feed (\n) line endings
   - Added to GitHub.
 - 5/22/2016 
   - Added support for the 'if defined(...)' style of predefs.
   - Performance: added faster method to remove whitespace.
   - Other: General code/comment cleanups (minor)
