// C# 9 'init' accessors and records require System.Runtime.CompilerServices.IsExternalInit,
// which ships with .NET 5+ but NOT with netstandard2.1. Declaring it ourselves is the
// standard, compiler-sanctioned workaround and lets us keep records + init on the Unity-
// compatible target framework.
//
// This type is intentionally internal so it cannot collide with the real one when this
// assembly is consumed by a modern runtime.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
