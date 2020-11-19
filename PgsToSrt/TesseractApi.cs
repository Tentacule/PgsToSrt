using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Tesseract.Interop;
using Tncl.NativeLoader;

namespace PgsToSrt
{
    static class TesseractApi
    {
        private const string _assemblyName = "Assembly.Tesseract";

        public static void Initialize()
        {
            var tessApiType = typeof(Tesseract.Page).Assembly.GetType("Tesseract.Interop.TessApi");
            var leptApiType = typeof(Tesseract.Page).Assembly.GetType("Tesseract.Interop.LeptonicaApi");

            var tessApiCustomType = CreateInterfaceType<ITessApiSignatures>("tesseract41", "tesseract", "4");
            var leptApiCustomType = CreateInterfaceType<ILeptonicaApiSignatures>("leptonica-1.80.0", "lept", "5");


            var loader = new NativeLoader();
            loader.WindowsOptions.UseSetDllDirectory = true;

            var tessApiNative = (ITessApiSignatures)loader.CreateInstance(tessApiCustomType);
            var leptApiNative = (ILeptonicaApiSignatures)loader.CreateInstance(leptApiCustomType);

            tessApiType.GetField("native", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, tessApiNative);
            leptApiType.GetField("native", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, leptApiNative);
        }

        public static Type CreateInterfaceType<T>(string windowsLibraryName, string commonLibraryName, string version)
        {
            var type = typeof(T);
            var infos = GetInfo(type);
            var typeName = $"{_assemblyName}.{type.Name}2";

            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(_assemblyName), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(_assemblyName);
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public, null, new Type[1] { type });

            foreach (var item in infos)
            {
                var methodInfo = item.Key;
                var parametersType = from p in methodInfo.GetParameters() select p.ParameterType;

                var methodBuilder = typeBuilder.DefineMethod(
                    methodInfo.Name, methodInfo.Attributes,
                    methodInfo.CallingConvention, methodInfo.ReturnType, parametersType.ToArray());

                // NativeLoaderOverrideAttribute
                var nativeLoaderOverrideAttributeType = typeof(NativeLoaderOverrideAttribute);
                var nativeLoaderOverrideAttributeConstructorInfo = nativeLoaderOverrideAttributeType.GetConstructor(new Type[0] { });
                var nativeLoaderOverrideAttributePpropNameValues = new Dictionary<string, object>();

                nativeLoaderOverrideAttributePpropNameValues.Add("LibraryName", windowsLibraryName);
                nativeLoaderOverrideAttributePpropNameValues.Add("Platform", Platform.Windows);

                var (propertyInfos, propertyValues) = GetProperties(nativeLoaderOverrideAttributeType, nativeLoaderOverrideAttributePpropNameValues);
                var attributeBuilder = new CustomAttributeBuilder(nativeLoaderOverrideAttributeConstructorInfo, new object[0] { }, propertyInfos, propertyValues);
                methodBuilder.SetCustomAttribute(attributeBuilder);

                // RuntimeUnmanagedFunctionPointerAttribute
                var runtimeUnmanagedFunctionPointerAttributeType = typeof(RuntimeUnmanagedFunctionPointerAttribute);
                var runtimeUnmanagedFunctionPointerAttributeConstructorInfo = runtimeUnmanagedFunctionPointerAttributeType.GetConstructor(new Type[0] { });
                var runtimeUnmanagedFunctionPointerAttributePropNameValues = new Dictionary<string, object>();

                runtimeUnmanagedFunctionPointerAttributePropNameValues.Add("LibraryName", commonLibraryName);
                runtimeUnmanagedFunctionPointerAttributePropNameValues.Add("LibraryVersion", version);
                runtimeUnmanagedFunctionPointerAttributePropNameValues.Add("CallingConvention", CallingConvention.Cdecl);
                runtimeUnmanagedFunctionPointerAttributePropNameValues.Add("EntryPoint", item.Value["EntryPoint"]);

                var (propertyInfos2, propertyValues2) = GetProperties(runtimeUnmanagedFunctionPointerAttributeType, runtimeUnmanagedFunctionPointerAttributePropNameValues);
                var attributeBuilder2 = new CustomAttributeBuilder(runtimeUnmanagedFunctionPointerAttributeConstructorInfo, new object[0] { }, propertyInfos2, propertyValues2);
                methodBuilder.SetCustomAttribute(attributeBuilder2);
            }

            return typeBuilder.CreateType();
        }

        public static List<KeyValuePair<MethodInfo, Dictionary<string, object>>> GetInfo(Type type)
        {
            var result = new List<KeyValuePair<MethodInfo, Dictionary<string, object>>>();
            var runtimeDllImportAttributeType = typeof(Tesseract.Page).Assembly.GetType("InteropDotNet.RuntimeDllImportAttribute");

            foreach (var methodInfo in type.GetMethods())
            {
                var runtimeDllImportAttribute = methodInfo.GetCustomAttribute(runtimeDllImportAttributeType);

                if (runtimeDllImportAttribute != null)
                {
                    var dict = new Dictionary<string, object>();

                    foreach (var propertyInfo in runtimeDllImportAttribute.GetType().GetProperties())
                    {
                        dict.Add(propertyInfo.Name, propertyInfo.GetValue(runtimeDllImportAttribute));
                    }

                    foreach (var fieldInfo in runtimeDllImportAttribute.GetType().GetFields())
                    {
                        dict.Add(fieldInfo.Name, fieldInfo.GetValue(runtimeDllImportAttribute));
                    }

                    result.Add(new KeyValuePair<MethodInfo, Dictionary<string, object>>(methodInfo, dict));
                }
            }

            return result;
        }

        public static (PropertyInfo[] propertyInfos, object[] propertyValues) GetProperties(Type type, Dictionary<string, object> propertyNameValues)
        {
            var p1 = new List<PropertyInfo>();
            var p2 = new List<object>();

            foreach (var item in propertyNameValues)
            {
                var propertyName = item.Key;
                var propertyValue = item.Value;

                var property = type.GetProperty(propertyName);
                p1.Add(property);
                p2.Add(propertyValue);
            }

            return (p1.ToArray(), p2.ToArray());
        }

    }
}
