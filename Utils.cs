using System;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace SODCustomStateInjectorMiami;


public static class IL2CPPUtils
{
    
    public static FieldInfo GetFieldIl2cpp(Il2CppObjectBase instance, string fieldName)
    {

        var type = instance.GetType();
        var fieldInfo = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fieldInfo != null)
        {
            return fieldInfo;
        }
        fieldInfo = type.GetField("NativeFieldInfoPtr_" + fieldName,  BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fieldInfo == null)
        {
            throw new MissingFieldException($"Field {fieldName} not found in {type.Name}.");
        }

        return fieldInfo;
    }

    public static T GetFieldValue<T>(Il2CppObjectBase instance, FieldInfo fieldInfo)
    {
        unsafe
        {
            var fieldInfoPtr = (IntPtr)fieldInfo.GetValue(instance);
            void* fieldValue = stackalloc byte[Marshal.SizeOf(typeof(T))];
            IL2CPP.il2cpp_field_get_value(instance.Pointer, fieldInfoPtr, fieldValue);
            return Marshal.PtrToStructure<T>((IntPtr)fieldValue);
        }
    }
    
    
    public static T SetFieldIl2cpp<T>(Il2CppObjectBase instance, FieldInfo fieldInfo, T value)
    {
        unsafe
        {
            
            var fieldInfoPtr = (IntPtr)fieldInfo.GetValue(instance);
            void* fieldValue = stackalloc byte[Marshal.SizeOf(typeof(T))];
            Marshal.StructureToPtr(value, (IntPtr)fieldValue, false);
            IL2CPP.il2cpp_field_set_value(instance.Pointer, fieldInfoPtr, fieldValue);
            return value;
        }
    }

    
}

public static class Utils
{
    public static int LoadStatesLength()
    {
        return Enum.GetValues(typeof(CityConstructor.LoadState)).Length;
    }
}

