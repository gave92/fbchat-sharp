using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace fbchat_sharp.API.Models
{
    public class Utils
    {
        private readonly static Uri SomeBaseUri = new Uri("http://someurl");

        public static string strip_json_cruft(string text)
        {
            try
            {
                return text.Substring(text.IndexOf("{"));
            }
            catch (Exception)
            {
                throw new FBchatException(string.Format("No JSON object found: {0}", text));
            }
        }

        public static long now()
        {
            return (long)DateTimeOffset.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        public static void handle_payload_error(JToken j)
        {
            if (j["error"] == null || j["error"].Type == JTokenType.Null)
                return;
            var error = j["error"].Value<int>();
            if (j["error"].Value<int>() == 1357001)
            {
                throw new FBchatNotLoggedIn(
                    string.Format("Error #{0} when sending request: {1}", error, j["errorDescription"]),
                    fb_error_code: error,
                    fb_error_message: j["errorDescription"]?.Value<string>());
            }
            else if (j["error"].Value<int>() == 1357004)
            {
                throw new FBchatPleaseRefresh(
                    string.Format("Error #{0} when sending request: {1}", error, j["errorDescription"]),
                    fb_error_code: error,
                    fb_error_message: j["errorDescription"]?.Value<string>());
            }
            else if (new int[] { 1357031, 1545010, 1545003 }.Contains(j["error"].Value<int>()))
            {
                throw new FBchatInvalidParameters(
                    string.Format("Error #{0} when sending request: {1}", error, j["errorDescription"]),
                    fb_error_code: error,
                    fb_error_message: j["errorDescription"]?.Value<string>());
            }
            // TODO: Use j["errorSummary"]
            // "errorDescription" is in the users own language!
        }

        public static void handle_graphql_errors(JToken j)
        {
            JToken errors = null;
            if (j["error"] != null && j["error"].Type == JTokenType.Null)
            {
                errors = j["error"];
            }
            else if (j["errors"] != null && j["errors"].Type == JTokenType.Null)
            {
                errors = j["errors"];
            }
            if (errors != null)
            {
                if (errors.Type == JTokenType.Array)
                    errors = errors[0]; // TODO: Handle multiple errors
                // TODO: Use `summary`, `severity` and `description`
                throw new FBchatFacebookError(
                    string.Format("GraphQL error #{0}: {1} / {2}",
                        errors["code"]?.Value<int>(), errors["message"]?.Value<string>(), errors["debug_info"]?.Value<string>()
                    ),
                    fb_error_code: errors["code"]?.Value<int>() ?? 0,
                    fb_error_message: errors["message"]?.Value<string>()
                );
            }

            var error = j["error"].Value<int>();
            if (j["error"].Value<int>() == 1357001)
            {
                throw new FBchatNotLoggedIn(
                    string.Format("Error #{0} when sending request: {1}", error, j["errorDescription"]),
                    fb_error_code: error,
                    fb_error_message: j["errorDescription"]?.Value<string>());
            }
            else if (j["error"].Value<int>() == 1357004)
            {
                throw new FBchatPleaseRefresh(
                    string.Format("Error #{0} when sending request: {1}", error, j["errorDescription"]),
                    fb_error_code: error,
                    fb_error_message: j["errorDescription"]?.Value<string>());
            }
            else if (new int[] { 1357031, 1545010, 1545003 }.Contains(j["error"].Value<int>()))
            {
                throw new FBchatInvalidParameters(
                    string.Format("Error #{0} when sending request: {1}", error, j["errorDescription"]),
                    fb_error_code: error,
                    fb_error_message: j["errorDescription"]?.Value<string>());
            }
            // TODO: Use j["errorSummary"]
            // "errorDescription" is in the users own language!
        }

        public static string generateOfflineThreadingID()
        {
            long ret = now();
            long value = (long)(new Random().NextDouble() * 4294967295);
            string str = "0000000000000000000000" + Convert.ToString(value, 2);
            str = str.Substring(str.Length - 22);
            string msgs = Convert.ToString(ret, 2) + str;
            return (Convert.ToInt64(msgs, 2)).ToString();
        }

        public static string generateMessageID(string client_id = null)
        {
            long k = now();
            long l = (long)(new Random().NextDouble() * 4294967295);
            return string.Format("<{0}:{1}-{2}@mail.projektitan.com>", k, l, client_id);
        }

        public static JToken get_jsmods_require(JToken j, int index)
        {
            if (j["jsmods"] != null && j["jsmods"]["require"] != null)
            {
                try
                {
                    return j["jsmods"]["require"][0][index][0];
                }
                catch (Exception)
                {
                    Debug.WriteLine("Error when getting jsmods_require: {0}. Facebook might have changed protocol", j);
                    return null;
                }
            }
            return null;
        }

        public static List<string> get_url_parameters(string url, params string[] args)
        {
            List<string> rtn = new List<string>();
            var query = ParseQueryString(url);
            foreach (string arg in args)
            {
                rtn.Add(query.ContainsKey(arg) ? query[arg] : null);
            }
            return rtn;
        }

        public static string get_url_parameter(string url, string param)
        {
            return get_url_parameters(url, param)[0];
        }

        public static Dictionary<string, string> ParseQueryString(string requestQueryString)
        {
            Dictionary<string, string> rc = new Dictionary<string, string>();
            string[] ar1 = requestQueryString.Split(new char[] { '&', '?' });
            foreach (string row in ar1)
            {
                if (string.IsNullOrEmpty(row)) continue;
                int index = row.IndexOf('=');
                if (index < 0) continue;
                rc[Uri.UnescapeDataString(row.Substring(0, index))] = Uri.UnescapeDataString(row.Substring(index + 1)); // use Unescape only parts
            }
            return rc;
        }

        public static ISet<T> require_list<T>(object list_)
        {
            if (list_ as IEnumerable<T> != null)
                return new HashSet<T>((IEnumerable<T>)list_);
            else
                return new HashSet<T>() { (T)list_ };
        }

        public static string mimetype_to_key(string mimetype)
        {
            if (mimetype == null)
                return "file_id";
            if (mimetype == "image/gif")
                return "gif_id";
            var x = mimetype.Split('/');
            if (new string[] { "video", "image", "audio" }.Contains(x[0]))
                return string.Format("{0}_id", x[0]);
            return "file_id";
        }

        public static string GetFileNameFromUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                uri = new Uri(SomeBaseUri, url);
            return Path.GetFileName(uri.LocalPath);
        }
    }
}
