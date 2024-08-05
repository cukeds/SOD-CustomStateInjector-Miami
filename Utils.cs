using System;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace SODCustomStateInjectorMiami;

public static class Utils
{
    public static int LoadStatesLength()
    {
        return Enum.GetValues(typeof(CityConstructor.LoadState)).Length;
    }
}

