using System.Collections;
using System.Net;
using System.Reflection;

namespace wpfapp.Helpers
{
    public class WtfWpf
    {
        public static void UnsetRestrictedHeaders()
        {
            // use reflection to remove IsRequestRestricted from headerInfo hash table
            Assembly a = typeof(HttpWebRequest).Assembly;
            foreach (FieldInfo f in a.GetType("System.Net.HeaderInfoTable").GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (f.Name == "HeaderHashTable")
                {
                    Hashtable hashTable = f.GetValue(null) as Hashtable;
                    foreach (string sKey in hashTable.Keys)
                    {

                        object headerInfo = hashTable[sKey];
                        foreach (FieldInfo g in a.GetType("System.Net.HeaderInfo").GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                        {

                            if (g.Name == "IsRequestRestricted")
                            {
                                bool b = (bool)g.GetValue(headerInfo);
                                if (b)
                                {
                                    g.SetValue(headerInfo, false);
                                    System.Diagnostics.Debug.WriteLine(sKey + "." + g.Name + " changed to false");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
