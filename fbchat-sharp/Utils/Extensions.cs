using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;

namespace fbchat_sharp
{
    internal static class Extensions
    {
        public static IEnumerable<T> OrEmptyIfNull<T>(this IEnumerable<T> source)
        {
            return source ?? Enumerable.Empty<T>();
        }

        public static void update<TKey, TValue1, TValue2>(this Dictionary<TKey, TValue1> dict, Dictionary<TKey, TValue2> mergeDict)
        {
            if (mergeDict != null)
            {
                foreach (var entry in mergeDict)
                {
                    dict[entry.Key] = (TValue1)Convert.ChangeType(entry.Value, typeof(TValue1));
                }
            }
        }

        public static JToken get(this JToken token, string key = null)
        {
            if (key != null)
            {
                return token.Type != JTokenType.Null && token[key] != null && token[key].Type != JTokenType.Null ?
                    token[key] : null;
            }
            return token.Type != JTokenType.Null ? token : null;
        }

        public static Dictionary<string, List<Cookie>> GetAllCookies(this CookieContainer container)
        {            
            var allCookies = new Dictionary<string, List<Cookie>>();
            string domain = ".facebook.com";
            var url = string.Format("https://{0}/", domain[0] == '.' ? domain.Substring(1) : domain);
            allCookies[domain] = new List<Cookie>();
            allCookies[domain].AddRange(container.GetCookies(new Uri(url)).Cast<Cookie>());

            /*
            var domainTableField = container.GetType().GetRuntimeFields().FirstOrDefault(x => x.Name == "m_domainTable") ??
                                   container.GetType().GetRuntimeFields().FirstOrDefault(x => x.Name == "_domainTable");
            var domains = (IDictionary)domainTableField.GetValue(container);

            foreach (var val in domains.Values)
            {
                var type = val.GetType().GetRuntimeFields().FirstOrDefault(x => x.Name == "m_list") ??
                           val.GetType().GetRuntimeFields().FirstOrDefault(x => x.Name == "_list");
                var values = (IDictionary)type.GetValue(val);

                foreach (CookieCollection cookies in values.Values)
                {
                    var domain = cookies.Cast<Cookie>().FirstOrDefault()?.Domain;
                    if (domain == null) continue;
                    if (!allCookies.ContainsKey(domain))
                        allCookies[domain] = new List<Cookie>();
                    allCookies[domain].AddRange(cookies.Cast<Cookie>());
                }
            }
            */
            return allCookies;
        }

        public static TValue GetValueOrDefault<TKey, TValue>
            (this IDictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defaultValue = default(TValue))
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
        }

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
