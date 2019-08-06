using AngleSharp.Dom;
using AngleSharp.Parser.Html;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
    public class State
    {
        private const string FB_DTSG_REGEX = "name=\"fb_dtsg\" value=\"(.*?)\"";
        private const string facebookEncoding = "UTF-8";
        private HtmlParser _parser = null;
        public string fb_dtsg = null;
        private string _revision = null;
        private int _counter = 0;
        private string _logout_h = null;
        private Dictionary<string, string> _headers = null;

        private HttpClientHandler HttpClientHandler;
        private HttpClient _http_client;

        private CookieContainer _session
        {
            get { return HttpClientHandler?.CookieContainer; }
        }

        public State(string user_agent = null)
        {
            this.HttpClientHandler = new HttpClientHandler() { UseCookies = true, CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
            this._http_client = new HttpClient(this.HttpClientHandler);
            this._parser = new HtmlParser();

            this._headers = new Dictionary<string, string>() {
                { "Referer", "https://www.facebook.com" },
                { "User-Agent", user_agent ?? Utils.USER_AGENTS[0] },
            };
        }

        private IHtmlCollection<IElement> find_input_fields(string html)
        {
            var document = _parser.Parse(html);
            return document.QuerySelectorAll("input");
        }

        private bool is_home(string url)
        {
            var uri = new Uri(url);
            // Check the urls `/home.php` and `/`
            return (uri.AbsolutePath.Contains("home") || uri.AbsolutePath == "/");
        }

        private static string get_decoded(byte[] content)
        {
            return Encoding.GetEncoding(facebookEncoding).GetString(content, 0, content.Length);
        }

        public async static Task<string> check_request(HttpResponseMessage r)
        {
            if (!r.IsSuccessStatusCode)
                throw new FBchatFacebookError(string.Format("Error when sending request: Got {0} response", r.StatusCode), request_status_code: (int)r.StatusCode);

            var buffer = await r.Content.ReadAsByteArrayAsync();
            if (buffer == null || buffer.Length == 0)
                throw new FBchatFacebookError("Error when sending request: Got empty response");
            string content = get_decoded(buffer);
            return content;
        }

        private async Task<HttpResponseMessage> _2fa_helper(HttpResponseMessage r, string code)
        {
            var soup = this.find_input_fields(await check_request(r));

            var fb_dtsg = soup.Where(i => i.GetAttribute("name").Equals("fb_dtsg")).Select(i => i.GetAttribute("value")).First();
            var nh = soup.Where(i => i.GetAttribute("name").Equals("nh")).Select(i => i.GetAttribute("value")).First();

            var data = new Dictionary<string, string>();
            data["approvals_code"] = code;
            data["fb_dtsg"] = fb_dtsg;
            data["nh"] = nh;
            data["submit[Submit Code]"] = "Submit Code";
            data["codes_submitted"] = 0.ToString();
            Debug.WriteLine("Submitting 2FA code.");

            var url = "https://m.facebook.com/login/checkpoint/";

            r = await this._cleanPost(url, data);
            if (this.is_home(r.RequestMessage.RequestUri.ToString()))
                return r;

            data.Remove("approvals_code");
            data.Remove("submit[Submit Code]");
            data.Remove("codes_submitted");

            data["name_action_selected"] = "save_device";
            data["submit[Continue]"] = "Continue";

            r = await this._cleanPost(url, data);
            if (this.is_home(r.RequestMessage.RequestUri.ToString()))
                return r;

            data.Remove("name_action_selected");
            Debug.WriteLine("Starting Facebook checkup flow.");

            // At this stage, we have dtsg, nh, submit[Continue]
            r = await this._cleanPost(url, data);
            if (this.is_home(r.RequestMessage.RequestUri.ToString()))
                return r;

            data.Remove("submit[Continue]");
            data["submit[This was me]"] = "This Was Me";
            Debug.WriteLine("Verifying login attempt.");

            // At this stage, we have dtsg, nh, submit[This was me]
            r = await this._cleanPost(url, data);
            if (this.is_home(r.RequestMessage.RequestUri.ToString()))
                return r;

            data.Remove("submit[This was me]");
            data["submit[Continue]"] = "Continue";
            data["name_action_selected"] = "save_device";
            Debug.WriteLine("Saving device again.");

            r = await this._cleanPost(url, data);
            return r;
        }

        public static async Task<State> from_session(State state)
        {
            var r = await state._cleanGet(Utils.prefix_url("/"));
            string content = await State.check_request(r);
            var soup = state.find_input_fields(content);
            var fb_dtsg = soup.Where(i => i.GetAttribute("name").Equals("fb_dtsg")).Select(i => i.GetAttribute("value")).FirstOrDefault();
            if (fb_dtsg == null)
            {
                // Fall back to searching with a regex
                var regex = new Regex(State.FB_DTSG_REGEX);
                fb_dtsg = regex.Match(content).Groups[1].Value;
            }

            var client_revision = content.Split(new[] { "\"client_revision\":" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(',')[0];

            var logout_h = soup.Where(i => i.GetAttribute("name").Equals("h")).Select(i => i.GetAttribute("value")).FirstOrDefault();

            state.fb_dtsg = fb_dtsg;
            state._revision = client_revision;
            state._logout_h = logout_h;
            return state;
        }

        public async Task<HttpResponseMessage> _cleanGet(string url, Dictionary<string, string> query = null, int timeout = 30)
        {
            HttpRequestMessage request = null;

            if (query != null)
            {
                var content = new FormUrlEncodedContent(query);
                var query_string = await content.ReadAsStringAsync();
                var builder = new UriBuilder(url) { Query = query_string };
                request = new HttpRequestMessage(HttpMethod.Get, builder.ToString());
            }
            else
            {
                request = new HttpRequestMessage(HttpMethod.Get, url);
            }
            foreach (var header in this._headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            var response = await this._http_client.SendAsync(request);

            if ((int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                return response;
            else
                return await _cleanGet(response.Headers.Location.ToString(), query, timeout);
        }

        public async Task<HttpResponseMessage> _cleanPost(string url, Dictionary<string, string> query = null, Dictionary<string, FB_File> files = null, int timeout = 30)
        {
            if (files != null)
            {
                return await this._postFile(url, query, files, timeout);
            }
            var content = new FormUrlEncodedContent(query);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            foreach (var header in this._headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            request.Content = content;
            var response = await _http_client.SendAsync(request);

            if ((int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                return response;
            else
                return await _cleanPost(response.Headers.Location.ToString(), query, files, timeout);
        }

        private async Task<HttpResponseMessage> _postFile(string url, Dictionary<string, string> query = null, Dictionary<string, FB_File> files = null, int timeout = 30)
        {
            var content = new MultipartFormDataContent();
            foreach (var keyValuePair in query)
            {
                content.Add(new StringContent(keyValuePair.Value), keyValuePair.Key);
            }
            if (files != null)
            {
                foreach (var file in files.Values)
                {
                    var image = new StreamContent(file.data);
                    image.Headers.ContentType = new MediaTypeHeaderValue(MimeMapping.MimeUtility.GetMimeMapping(file.path));
                    content.Add(image, "file", file.path);
                }
            }
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            // Removes 'Content-Type' from the header
            var headers = this._headers.Where(h => h.Key != "Content-Type");

            foreach (var header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            request.Content = content;
            var r = await _http_client.SendAsync(request);

            if ((int)r.StatusCode < 300 || (int)r.StatusCode > 399)
            {
                return r;
            }
            else
            {
                return await _postFile(r.Headers.Location.ToString(), query, files, timeout: timeout);
            }
        }

        public Dictionary<string, List<Cookie>> get_cookies()
        {
            return this._session.GetAllCookies();
        }

        public List<FB_File> get_files_from_paths(Dictionary<string, Stream> file_paths)
        {
            var files = new List<FB_File>();
            foreach (var file_path in file_paths)
            {
                var file = new FB_File();
                file.data = file_path.Value;
                file.path = Path.GetFileName(file_path.Key);
                file.mimetype = MimeMapping.MimeUtility.GetMimeMapping(file.path);
                files.Add(file);
            }
            return files;
        }

        public async Task<List<FB_File>> get_files_from_urls(ISet<string> file_urls)
        {
            var files = new List<FB_File>();
            foreach (var file_url in file_urls)
            {
                var r = await this._cleanGet(file_url);
                // We could possibly use r.headers.get('Content-Disposition'), see
                // https://stackoverflow.com/a/37060758
                var file = new FB_File();
                file.data = await r.Content.ReadAsStreamAsync();
                file.path = Utils.GetFileNameFromUrl(file_url);
                file.mimetype = r.Content.Headers.ContentType.MediaType;
                files.Add(file);
            }
            return files;
        }

        public string get_user_id()
        {
            var cookies = (this._session.GetAllCookies().Values.SelectMany(c => c).Cast<Cookie>()
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(c => c.Key, c => c.First().Value, StringComparer.OrdinalIgnoreCase));
            return cookies.GetValueOrDefault("c_user");
        }

        public Dictionary<string, string> get_params()
        {
            this._counter += 1;
            var payload = new Dictionary<string, string>();
            payload["__a"] = 1.ToString();
            payload["__req"] = Utils.str_base(this._counter, 36);
            payload["__rev"] = this._revision;
            payload["fb_dtsg"] = this.fb_dtsg;
            return payload;
        }

        public async static Task<State> login(string email, string password, Func<Task<string>> on_2fa_callback, string user_agent = null)
        {
            var state = new State(user_agent);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                throw new Exception("Email and password not set.");
            }

            var r = await state._cleanGet("https://m.facebook.com/");
            var soup = state.find_input_fields(await State.check_request(r));

            var data = soup.Where(i => i.HasAttribute("name") && i.HasAttribute("value")).Select(i => new { Key = i.GetAttribute("name"), Value = i.GetAttribute("value") })
                .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(c => c.Key, c => c.First().Value, StringComparer.OrdinalIgnoreCase);

            data["email"] = email;
            data["pass"] = password;
            data["login"] = "Log In";

            r = await state._cleanPost("https://m.facebook.com/login.php?login_attempt=1", data);
            var content = (string)await State.check_request(r);

            if (r.RequestMessage.RequestUri.ToString().Contains("checkpoint") &&
                (content.ToLower().Contains("id=\"approvals_code\"")))
            {
                var code = await on_2fa_callback();
                r = await state._2fa_helper(r, code);
            }

            // Sometimes Facebook tries to show the user a "Save Device" dialog
            if (r.RequestMessage.RequestUri.ToString().Contains("save-device"))
            {
                r = await state._cleanGet("https://m.facebook.com/login/save-device/cancel/");
            }

            if (state.is_home(r.RequestMessage.RequestUri.ToString()))
            {
                return await State.from_session(state);
            }
            else
            {
                throw new FBchatUserError(
                    string.Format("Login failed. Check email/password. (Failed on url: {0})",
                        r.RequestMessage.RequestUri.ToString())
                );
            }
        }

        public async Task<bool> is_logged_in()
        {
            // Send a request to the login url, to see if we're directed to the home page
            var r = await this._cleanGet("https://m.facebook.com/login.php?login_attempt=1");
            return is_home(r.RequestMessage.RequestUri.ToString())
                || (r.Headers.Contains("Location") && is_home(r.Headers.Location.ToString()));
        }

        public async Task<bool> logout()
        {
            var url = Utils.prefix_url("/bluebar/modern_settings_menu/");

            if (this._logout_h == null)
            {
                var h_r = await this._cleanPost(url, query: new Dictionary<string, string>() { { "pmid", "4" } });
                Regex regex = new Regex("name=\\\"h\\\" value=\\\"(.*?)\\\"");
                this._logout_h = regex.Match(await State.check_request(h_r)).Groups[1].Value;
            }

            var data = new Dictionary<string, string>() {
                { "ref", "mb"},
                { "h", this._logout_h }
            };

            url = Utils.prefix_url("/logout.php");
            var r = await this._cleanGet(url, data);
            return r.IsSuccessStatusCode;
        }

        public async static Task<State> from_cookies(Dictionary<string, List<Cookie>> session_cookies, string user_agent)
        {
            var state = new State(user_agent: user_agent);

            try
            {
                // Load cookies into current session
                foreach (string rawurl in session_cookies.Keys)
                {
                    // Need this because of new Uri(...)
                    var url = string.Format("https://{0}/", rawurl[0] == '.' ? rawurl.Substring(1) : rawurl);

                    var current_cookies = state._session.GetCookies(new Uri(url)).Cast<Cookie>();

                    foreach (var cookie in session_cookies[rawurl])
                    {
                        if (!current_cookies.Any(c => c.Name.Equals(cookie.Name)))
                        {
                            // Check if this is a domain cookie
                            var domain = rawurl[0] == '.' ? rawurl.Substring(1) : rawurl;
                            if (domain.StartsWith("facebook.com"))
                            {
                                // Add cookie to every subdomain
                                state._session.Add(new Uri(string.Format("https://{0}/", domain)), new Cookie(cookie.Name, cookie.Value));
                                state._session.Add(new Uri(string.Format("https://www.{0}/", domain)), new Cookie(cookie.Name, cookie.Value));
                                state._session.Add(new Uri(string.Format("https://m.{0}/", domain)), new Cookie(cookie.Name, cookie.Value));
                                state._session.Add(new Uri(string.Format("https://upload.{0}/", domain)), new Cookie(cookie.Name, cookie.Value));
                                foreach (var i in Enumerable.Range(0, 10))
                                    state._session.Add(new Uri(string.Format("https://{0}-edge-chat.{1}/", i, domain)), new Cookie(cookie.Name, cookie.Value)); // yuck!!
                            }
                            else
                            {
                                state._session.Add(new Uri(url), new Cookie(cookie.Name, cookie.Value));
                            }
                        }
                    }
                }
                return await State.from_session(state);
            }
            catch (Exception)
            {
                return await State.from_session(state);
            }
        }
    }
}
