//C2CS_TOP_Write // This is both a demo and testing file.  This file represents a c/c++ header file that we want to share basic items with a C# project.  These can be constants, structures, predefinitions, and enums.
  
/* Commands and usage               Description
===========================================================
"// C2CS_TOP_Write text to write"	C2CS_TOP_Write writes text after above the namespace. This can be useful whenever their is a need to print at the top of the output file. Example:  //C2CS_TOP_Write using System.IO;
"// C2CS_NS_Write text to write"	C2CS_NS_Write writes text after the namespace name but before the class name. This area usually contains enums and structs.
"// C2CS_Class_Write text to write"	C2CS_Class_Write writes text after the command to the Class area.  CppHeader2CS write only one output class and this command writes items there.
"// C2CS_Set_Namespace My_NS_name"	C2CS_Set_Namespace sets the namespace name to use. This is optional and if unspecified defaults to  CppHeader2CS.
"// C2CS_Set_ClassName My_Class"	C2CS_Set_ClassName sets the class name to use. This is optional and if unspecified defaults to Constants.
"// C2CS_TYPE MyType"				C2CS_TYPE is used in the comments after #define to specify what type to use.  This is sometimes required if the automatic detection does not work as expected or you want to force it to some type. 
"// C2CS_SKIP"						Add C2CS_SKIP in any comment and that whole line will be ignored
*/

// Lets begin with some with some basic commands. These would be in the c/c++ source file.

// The C2CS_Set_Namespace command can optionally be used to set a namespace name. If unspecified it will use "C2CS" as the namespace.
//C2CS_Set_Namespace mynamespace

// The C2CS_Set_ClassName command can optionally be used to set a class name. If unspecified it will use "Constants" as the class.
//C2CS_Set_ClassName myClass

// These 3 write commands can be used to write text to one of the 3 main sections...
// C2CS_TOP_Write // This comments will appear in the namespace section.
// C2CS_NS_Write // This comments will appear in the namespace section.
// C2CS_Class_Write // This comment will appear in the class section.




// Lets begin with some examples....
// #ifndef will be converted to #if ! and will be duplicated in both the namespace area and the class area.
#ifndef __MYTEST_INCLUDED__


// Any Classes will be skipped however if there is any information to be pulled out it will bring it over.  This could cause issues because if there are items with the same name then the C# version will fail.
class MyClass : public MyParent   
{
public:						   // ignored - all output is public
  std::vector<int> avector;    // skipped - vectors are not implemented
  Foo* foo;                    // skipped - only built in types are supported 
  void Func(Foo& myFoo);       // skipped - function header

  friend class MyFriendClass;  // skipped - not implemented

};

// Global variables and constants get added to the class area...
static char *SDK_name = "fluidsD3D9";	// re-labeled into a string
static char* SDK_name2 = "fluidsD3D9";	// re-labeled into a string
protected static char myChar = -100;				// char is re-labeled to sbyte
static short int myShort = -100;		// short int is re-labeled to Int16
const int NUM_CALCS=6; 

// This will be added to both the namespace and class areas...
#endif // __MYTEST_INCLUDED__

// Here are some structs that get converted into the namespace area but if there is a following implementation it gets added to the class area....
// Multiline struct test
  struct SomeStruct1 
  {
    unsigned 
		long 
		long 
		test127; 
    protected 
		long 
		_test_28;
		float 
		_test_29;
  } 
someStructInstance1 ;  // <-- this will actually get put into the class area

// Here we add a StructLayout to a struct...
// C2CS_NS_Write  [StructLayout(LayoutKind.Sequential)]
struct SomeStruct2{
    unsigned long long test123; //this will get converted to a UInt64
    public long test124;// some comments
    public char SHOULD_BE_SKIPPED66; // this line should be skipped because C2CS_SKIP is here
	void *myVoidPtr;
} someStructInstance2 ;  //some notes here


// Struct testing with predefs....
#define TEST
#IF (TEST && TEST) //TEST COMMENT

  struct _SomeStruct0 {
	public int test1;
	private int test2;
  };

#else

  struct SomeStruct3 {unsigned char r; unsigned char b; unsigned char g;}; //all on one line
  
#endif // TEST && TEST


// Enum samples/Testing...
// typeless c style enum
typedef enum 
 {
   test1
} test1;


enum EnumTest2 {
	test1 = 33,
	test2 = 7,
	test3 = test2 };

// single line test
enum EnumTest3{ red, green, blue };


enum EnumTest4
 {
   _Test,
};

// The source file should be error free, CppHeader2CS will sometimes skip incorrect code...
enum SHOULD_BE_SKIPPED41{ red, green, blue } e;  // skipped - invalid enum
enum { red, green, blue } SHOULD_BE_SKIPPED42;  // skipped - invalid enum
enum SHOULD_BE_SKIPPED44{ red;green;blue };  // skipped - invalid enum


// C2CS_NS_Write 
// C2CS_NS_Write // empty lines below created by using an empty "// C2CS_NS_Write" or // C2CS_NS_Write (blank line)
// C2CS_NS_Write (blank line)
// C2CS_NS_Write 

// C2CS_NS_Write // the #ifdef is converted to #if
#ifdef TEST

#endif // ifdef TEST
// C2CS_NS_Write
#pragma region start

#pragma endregion
// C2CS_NS_Write

// Adjustments
#define DEV_MODE
#define My_Bool     true  //public const bool My_Bool = true 
#define My_Bool2	false //public const bool My_Bool = false

// C2CS_Class_Write
// C2CS_Class_Write // Tabs/spaces are okay.  Also beginning and ending parentheses are okay. 
#define my_int1 131072      //this one has spaces
#define my_int2	131072	//this one has a tabs
#define my_int3 (131072)    //this one has spaces
#define my_hex	0x1F		// hex is converted to an int 
#define MYBOOL0	true
#define MYBOOL1	false
#define MYBOOL2	(true) // test

// C2CS_Class_Write
#define my_double1 3.0
#define my_double2 -3.2   
#define my_double3 -3.2d   
#define my_double4 -6.673e-11   
// C2CS_Class_Write // Adding "f" to the end of a float is okay.
#define my_float1 3.3f     
#define my_float2 3.3F     
#define my_float3 3f     

#define exprInt0 1 + 2				// C2CS_TYPE:byte  <-- here we are telling CppHeader2CS to force a "byte" type.
#define exprDouble1 my_double1 + 2     
#define exprFloat2 my_float1 + 2     
#define exprDouble3 my_float1 + my_double1 + my_int1   
#define exprDouble4a 2.2 + 2.2 + 0xFAb   
#define exprDouble4b 2.2 + 0xFAb   
#define exprDouble4c 2.2 + 2.2d  
#define exprDouble4 2.2 + 2.2d + 0xFAb   
#define exprBool5 MYBOOL2 || true	

// C2CS_Class_Write
// C2CS_Class_Write // Any #Defines that cannot be converted into an int, float, or bool will be a string 
#define my_string MyString 


// Some items that cannot be converted or are not implemented. All of the following will be bypassed...
const MyType SHOULD_BE_SKIPPED51 = 5; // This will get skipped because UnknownType is not recognized
#define SHOULD_BE_SKIPPED52_exprBool (my_float1 == my_float1) || true  // mixed boolean and int/floats not supported
#define SHOULD_BE_SKIPPED52 143		// This line will be skipped because C2CS_SKIP is in the comments. It can be anywhere.
/*const int SHOULD_BE_SKIPPED53 = 5;// skipped - Anything(including commands) in this style of comment will always be skipped */
typedef int SHOULD_BE_SKIPPED54;	// skipped - typedefs are not supported in c#
// This comment will be ignored since a location has not been specified.
#include <SHOULD_BE_SKIPPED55>		// skipped - included dependencies
#include "SHOULD_BE_SKIPPED56.h"	// skipped - included dependencies
class SHOULD_BE_SKIPPED57;			// skipped - forward declared dependencies
private static char SHOULD_BE_SKIPPED77 = -100;	// skipped - private is not brought over
  struct StructWithBitFields {
	private int test1 : 3;
	private int test2 : 4;
  }; //Bitfields will be stripped off

 // Test in-file type declarations
  struct UserTypeTest1 { SomeStruct2  myUserStruct; EnumTest2 myUserDefEnum; };
  struct SHOULD_BE_SKIPPED570 { UserTypeTest2  myUserStruct1; }; // skipped because UserTypeTest2 not defined yet
  struct UserTypeTest2 { UserTypeTest1  myUserStruct1; UserTypeTest1 myUserDefEnum2; };


// Lets make sure commented out code does not appear in the output...

// static char SHOULD_BE_SKIPPED3 = -100; 
// #define SHOULD_BE_SKIPPED11  // comments
// #define SHOULD_BE_SKIPPED12 123// comments
/* #define SHOULD_BE_SKIPPED1 */
/* #define SHOULD_BE_SKIPPED2 123 */
// static char SHOULD_BE_SKIPPED13 = -100; //comments
// enum SHOULD_BE_SKIPPED21{ red, green, blue }; 
// enum SHOULD_BE_SKIPPED22{ red, green, blue }; // comments
//struct SHOULD_BE_SKIPPED23 {char a; char b; char c;}; 
/*struct SHOULD_BE_SKIPPED23 {char a; char b; char c;}; */