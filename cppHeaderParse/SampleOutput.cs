// Generated using CppHeader2CS
// This is both a demo and testing file.  This file represents a c/c++ header file that we want to share basic items with a C# project.  These can be constants, structures, predefinitions, and enums.
// This comments will appear in the namespace section.
#define TEST
#define DEV_MODE

using System;
using System.Runtime.InteropServices;


namespace mynamespace
{
// This comments will appear in the namespace section.
#if ! __MYTEST_INCLUDED__
#endif // __MYTEST_INCLUDED__
    public struct SomeStruct1
    {
        public ulong test127; 
        public long _test_28;
        public float _test_29;
    } // <-- this will actually get put into the class area

 [StructLayout(LayoutKind.Sequential)]
    public struct SomeStruct2
    {
        public ulong test123; //this will get converted to a UInt64
        public long test124;// some comments
        public UIntPtr myVoidPtr;
    } //some notes here

#if (TEST && TEST) //TEST COMMENT
    public struct _SomeStruct0
    {
        public int test1;
        public int test2;
    } 

#else
    public struct SomeStruct3
    {
        public byte r; 
        public byte b; 
        public byte g;
    } //all on one line

#endif // TEST && TEST
    public enum test1
    {
        test1,
    };

    [Flags]
    public enum EnumTest2
    {
        test1 = 33,
        test2 = 7,
        test3 = test2,
    };

    public enum EnumTest3
    {
        red,
        green,
        blue,
    };

    public enum EnumTest4
    {
        _Test,
    };


// empty lines below created by using an empty "// C2CS_NS_Write" or // C2CS_NS_Write (blank line)


// the #ifdef is converted to #if
#if TEST
#endif // ifdef TEST

#region start
#endregion

    public struct StructWithBitFields
    {
        public int test1;
        public int test2;
    } //Bitfields will be stripped off

    public struct UserTypeTest1
    {
        public SomeStruct2 myUserStruct; 
        public EnumTest2 myUserDefEnum;
    } 

    public struct SHOULD_BE_SKIPPED570
    {
    } // skipped because UserTypeTest2 not defined yet

    public struct UserTypeTest2
    {
        public UserTypeTest1 myUserStruct1; 
        public UserTypeTest1 myUserDefEnum2;
    } 

    class myClass
    {
        // This comment will appear in the class section.
#if ! __MYTEST_INCLUDED__
        public const String SDK_name= "fluidsD3D9";	// re-labeled into a string
        public const String SDK_name2= "fluidsD3D9";	// re-labeled into a string
        public const SByte myChar= -100;				// char is re-labeled to sbyte
        public const short myShort= -100;		// short int is re-labeled to Int16
        public const int NUM_CALCS=6; 
#endif // __MYTEST_INCLUDED__
        public static SomeStruct1 someStructInstance1; // <-- this will actually get put into the class area
        public static SomeStruct2 someStructInstance2; //some notes here
#if (TEST && TEST) //TEST COMMENT
#else
#endif // TEST && TEST
#if TEST
#endif // ifdef TEST
#region start
#endregion
        public const bool My_Bool = true; //public const bool My_Bool = true 
        public const bool My_Bool2 = false; //public const bool My_Bool = false
        
        // Tabs/spaces are okay.  Also beginning and ending parentheses are okay. 
        public const int my_int1 = 131072; //this one has spaces
        public const int my_int2 = 131072; //this one has a tabs
        public const int my_int3 = 131072; //this one has spaces
        public const int my_hex = 0x1F; // hex is converted to an int 
        public const bool MYBOOL0 = true; 
        public const bool MYBOOL1 = false; 
        public const bool MYBOOL2 = true; // test
        
        public const double my_double1 = 3.0; 
        public const double my_double2 = -3.2; 
        public const double my_double3 = -3.2d; 
        public const double my_double4 = -6.673e-11; 
        // Adding "f" to the end of a float is okay.
        public const float my_float1 = 3.3f; 
        public const float my_float2 = 3.3F; 
        public const float my_float3 = 3f; 
        public const byte exprInt0 = 1 + 2; // C2CS_TYPE:byte  <-- here we are telling CppHeader2CS to force a "byte" type.
        public const double exprDouble1 = my_double1 + 2; 
        public const float exprFloat2 = my_float1 + 2; 
        public const double exprDouble3 = my_float1 + my_double1 + my_int1; 
        public const string exprDouble4a = "2.2 + 2.2 + 0xFAb"; 
        public const string exprDouble4b = "2.2 + 0xFAb"; 
        public const double exprDouble4c = 2.2 + 2.2d; 
        public const string exprDouble4 = "2.2 + 2.2d + 0xFAb"; 
        public const bool exprBool5 = MYBOOL2 || true; 
        
        // Any #Defines that cannot be converted into an int, float, or bool will be a string 
        public const string my_string = "MyString"; 
        public const string SHOULD_BE_SKIPPED52_exprBool = "(my_float1 == my_float1) || true"; // mixed boolean and int/floats not supported
        public const SByte SHOULD_BE_SKIPPED77= -100;	// skipped - private is not brought over
    }
}
