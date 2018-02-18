using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace fbchat_sharp
{
    internal static class Extensions
    {
        public static string GetEnumDescriptionAttribute<T>(this T value) where T : struct
        {
            // The type of the enum, it will be reused.
            Type type = typeof(T);

            // If T is not an enum, get out.
            if (!type.GetTypeInfo().IsEnum)
                throw new InvalidOperationException(
                    "The type parameter T must be an enum type.");

            // If the value isn't defined throw an exception.
            if (!Enum.IsDefined(type, value))
                throw new ArgumentException($"Invalid value {value}");

            // Get the static field for the value.
            FieldInfo fi = type.GetRuntimeField(value.ToString());

            // Get the description attribute, if there is one.
            return fi.GetCustomAttributes(typeof(DescriptionAttribute), true).
                Cast<DescriptionAttribute>().SingleOrDefault().Description;
        }
    }
}
