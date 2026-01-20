// ============================================================================
// MANIFEST GENERATOR
// ============================================================================
// Generates JSON manifest from object instance using reflection.
//
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace RailSDK
{
    internal static class ManifestGenerator
    {
        public static object Generate(object instance, Dictionary<string, MethodInfo> methodCache)
        {
            var functions = new List<object>();

            foreach (var kvp in methodCache)
            {
                var method = kvp.Value;
                var parameters = new List<object>();

                foreach (var param in method.GetParameters())
                {
                    parameters.Add(new
                    {
                        name = param.Name,
                        type = MapType(param.ParameterType),
                        required = !param.HasDefaultValue
                    });
                }

                functions.Add(new
                {
                    name = method.Name,
                    description = "",
                    parameters
                });
            }

            return new
            {
                processId = Process.GetCurrentProcess().Id,
                language = "dotnet",
                sdkVersion = "2.0.0",
                context = instance.GetType().Name,
                functions
            };
        }

        private static string MapType(Type type)
        {
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
                return "integer";
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "number";
            if (type == typeof(string))
                return "string";
            if (type == typeof(bool))
                return "boolean";
            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
                return "array";
            return "object";
        }
    }
}



