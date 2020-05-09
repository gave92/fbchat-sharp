using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using Newtonsoft.Json.Linq;
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
using System.Threading;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Stores and manages state required for most Facebook requests.
    /// This is the main class, which is used to login to Facebook.
    /// </summary>
    public class Session
    {
        private const string SERVER_JS_DEFINE_REGEX = "require\\(\"ServerJSDefine\"\\)\\)?\\.handleDefines\\(";
        private const string facebookEncoding = "UTF-8";
        private HtmlParser _parser = null;
        private string _fb_dtsg = null;
        private string _revision = null;
        private int _counter = 0;
        private string _client_id = null;
        private Dictionary<string, string> _headers = null;

        private HttpClientHandler HttpClientHandler;
        private HttpClient _http_client;

        private CookieContainer _session
        {
            get { return HttpClientHandler?.CookieContainer; }
            set
            {
                if (HttpClientHandler != null)
                    HttpClientHandler.CookieContainer = value;
            }
        }

        /// <summary>
        /// Stores and manages state required for most Facebook requests.
        /// This is the main class, which is used to login to Facebook.
        /// </summary>
        internal Session(string user_agent = null)
        {
            this.HttpClientHandler = new HttpClientHandler() { UseCookies = true, CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
            this._http_client = new HttpClient(this.HttpClientHandler);
            this._parser = new HtmlParser();

            this._headers = new Dictionary<string, string>() {
                { "Referer", "https://www.messenger.com" },
                { "User-Agent", user_agent ?? Utils.USER_AGENTS[0] },
            };

            //this._client_id = ((int)(new Random().NextDouble() * Math.Pow(2, 31))).ToString("x4");
            this._client_id = Guid.NewGuid().ToString();
        }

        private static string get_decoded(byte[] content)
        {
            return Encoding.GetEncoding(facebookEncoding).GetString(content, 0, content.Length);
        }

        private JToken parse_server_js_define(string html)
        {
            // Parse ``ServerJSDefine`` entries from a HTML document.
            // Find points where we should start parsing
            var regex = new Regex(Session.SERVER_JS_DEFINE_REGEX);
            var define_splits = regex.Split(html);

            // Skip leading entry
            define_splits = define_splits.Skip(1).ToArray();

            var rtn = new JArray();
            if (define_splits == null || define_splits.Length == 0)
                throw new FBchatParseError("Could not find any ServerJSDefine", data: html);
            // Parse entries (should be two)
            foreach (var entry in define_splits)
            {
                var tmp = entry.Substring(0, entry.IndexOf(");"));
                JToken parsed = null;
                try
                {
                    parsed = JToken.Parse(tmp);
                }
                catch
                {
                    throw new FBchatParseError("Invalid ServerJSDefine", data: entry);
                }
                if (!(parsed.Type == JTokenType.Array))
                    throw new FBchatParseError("Invalid ServerJSDefine", data: parsed);
                rtn.Merge(parsed);
            }
            // Convert to a dict
            return Utils.get_jsmods_define(rtn);
        }

        private async static Task<string> check_request(HttpResponseMessage r, bool no_throw = false)
        {
            if (!r.IsSuccessStatusCode && !no_throw)
                throw new FBchatFacebookError(string.Format("Error when sending request: Got {0} response", r.StatusCode), request_status_code: (int)r.StatusCode);

            var buffer = await r.Content.ReadAsByteArrayAsync();
            if (buffer == null || buffer.Length == 0)
            {
                if (!no_throw)
                    throw new FBchatFacebookError("Error when sending request: Got empty response");
                return "";
            }
            else
            {
                string content = get_decoded(buffer);
                return content;
            }
        }

        private async Task<string> _2fa_helper(HttpResponseMessage r, string code)
        {
            (var url, var data) = this.find_form_request(await check_request(r));

            if (data.ContainsKey("approvals_code"))
            {
                data["approvals_code"] = code;
                r = await this._cleanPost(url, data, redirect: false);
                (url, data) = this.find_form_request(await check_request(r));
            }

            if (data.ContainsKey("name_action_selected"))
            {
                data["name_action_selected"] = "dont_save";
                r = await this._cleanPost(url, data, redirect: false);
                (url, data) = this.find_form_request(await check_request(r));
            }

            r = await this._cleanPost(url, data, redirect: false);
            (url, data) = this.find_form_request(await check_request(r));

            Debug.WriteLine("Starting Facebook checkup flow.");

            if (!data.ContainsKey("submit[This was me]") || !data.ContainsKey("submit[This wasn't me]"))
                throw new FBchatParseError("Could not fill out form properly (2)", data: data);

            data["submit[This was me]"] = "[any value]";
            data.Remove("submit[This wasn't me]");

            Debug.WriteLine("Verifying login attempt.");

            r = await this._cleanPost(url, data, redirect: false);
            (url, data) = this.find_form_request(await check_request(r));

            if (!data.ContainsKey("name_action_selected"))
                throw new FBchatParseError("Could not fill out form properly (3)", data: data);

            data["name_action_selected"] = "dont_save";
            Debug.WriteLine("Saving device again.");

            r = await this._cleanPost(url, data, redirect: false);
            return r.Headers.Location.ToString();
        }

        private string get_error_data(string html)
        {
            var document = _parser.Parse(html);
            var form = document.QuerySelector("#login_form");
            return form?.Text();
        }

        private (string url, Dictionary<string, string> data) find_form_request(string html)
        {
            // Only import when required
            var soup = _parser.Parse(html);
            var form = soup.QuerySelector("form");
            if (form == null)
                throw new FBchatParseError("Could not find form to submit", data: soup);

            var url = form.GetAttribute("action");
            if (url == null)
                throw new FBchatParseError("Could not find url to submit to", data: form);

            if (url.StartsWith("/"))
                url = "https://www.facebook.com" + url;

            // It's okay to set missing values to something crap, the values are localized, and
            // hence are not available in the raw HTML
            var data = form.QuerySelectorAll("input").Union(form.QuerySelectorAll("button"))
                .Select(x => new KeyValuePair<string, string>(x.GetAttribute("name"), x.GetAttribute("value") ?? "[missing]"))
                .Where(x => x.Key != null).GroupBy(x => x.Key)
                .Select(x => x.Any(v => v.Value != "[missing]") ? x.First(v => v.Value != "[missing]") : x.First())
                .ToDictionary(x => x.Key, x => x.Value);

            return (url, data);
        }

        private string get_fb_dtsg(JToken define)
        {
            if (define.get("DTSGInitData") != null)
                return define.get("DTSGInitData")?.get("token")?.Value<string>();
            else if (define.get("DTSGInitialData") != null)
                return define.get("DTSGInitialData").get("token")?.Value<string>();
            return null;
        }

        private static async Task<Session> from_session(Session session)
        {
            // TODO: Automatically set user_id when the cookie changes in the session
            var r = await session._cleanGet<string>(Utils.prefix_url("/"), redirect: false);
            string content = await Session.check_request(r);

            var define = session.parse_server_js_define(content);
            var fb_dtsg = session.get_fb_dtsg(define);

            if (fb_dtsg == null)
                throw new FBchatParseError("Could not find fb_dtsg", data: define);
            if (string.IsNullOrEmpty(fb_dtsg))
                // Happens when the client is not actually logged in
                throw new FBchatNotLoggedIn(
                    "Found empty fb_dtsg, the session was probably invalid."
                );

            var revision = define?.get("SiteData")?.get("client_revision")?.Value<string>();

            return new Session()
            {
                _fb_dtsg = fb_dtsg,
                _revision = revision,
                _session = session._session,
            };
        }

        private async Task _do_refresh()
        {
            // TODO: Raise the error instead, and make the user do the refresh manually
            // It may be a bad idea to do this in an exception handler, if you have a better method, please suggest it!
            Debug.WriteLine("Refreshing state and resending request");
            var new_state = await Session.from_session(session: this);
            this._fb_dtsg = new_state._fb_dtsg;
            this._revision = new_state._revision;
            this._counter = new_state._counter;
        }

        private async Task<HttpResponseMessage> _cleanGet<TValue>(string url, Dictionary<string, TValue> query = null, CancellationToken cancellationToken = default(CancellationToken), bool redirect = true)
        {
            HttpRequestMessage request = null;

            if (query != null)
            {
                var content = new FormUrlEncodedContent(query?.ToDictionary(k => k.Key, k => k.Value?.ToString()));
                var query_string = await content.ReadAsStringAsync();
                var builder = new UriBuilder(url) { Query = query_string };
                request = new HttpRequestMessage(HttpMethod.Get, builder.ToString());
            }
            else
            {
                request = new HttpRequestMessage(HttpMethod.Get, url);
            }
            foreach (var header in this._headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            var response = await this._http_client.SendAsync(request, cancellationToken);

            if (!redirect || (int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                return response;
            else
                return await _cleanGet(response.Headers.Location.ToString(), query, cancellationToken);
        }

        private async Task<HttpResponseMessage> _cleanPost<TValue>(string url, Dictionary<string, TValue> query = null, Dictionary<string, FB_File> files = null, CancellationToken cancellationToken = default(CancellationToken), bool redirect = true)
        {
            if (files != null)
            {
                return await this._postFile(url, query, files, cancellationToken);
            }
            var content = new FormUrlEncodedContent(query?.ToDictionary(k => k.Key, k => k.Value?.ToString()));
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            foreach (var header in this._headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            request.Content = content;
            var response = await _http_client.SendAsync(request, cancellationToken);

            if (!redirect || (int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                return response;
            else
                return await _cleanPost(response.Headers.Location.ToString(), query, files, cancellationToken);
        }

        private async Task<HttpResponseMessage> _postFile<TValue>(string url, Dictionary<string, TValue> query = null, Dictionary<string, FB_File> files = null, CancellationToken cancellationToken = default(CancellationToken), bool redirect = true)
        {
            var content = new MultipartFormDataContent();
            foreach (var keyValuePair in query)
            {
                content.Add(new StringContent(keyValuePair.Value?.ToString()), keyValuePair.Key);
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
            var r = await _http_client.SendAsync(request, cancellationToken);

            if (!redirect || (int)r.StatusCode < 300 || (int)r.StatusCode > 399)
            {
                return r;
            }
            else
            {
                return await _postFile(r.Headers.Location.ToString(), query, files, cancellationToken);
            }
        }

        /// <summary>
        /// Get session coookies
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, List<Cookie>> get_cookies()
        {
            return this._session.GetAllCookies();
        }

        internal List<FB_File> get_files_from_paths(Dictionary<string, Stream> file_paths)
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

        internal async Task<List<FB_File>> get_files_from_urls(ISet<string> file_urls)
        {
            var files = new List<FB_File>();
            foreach (var file_url in file_urls)
            {
                var r = await this._cleanGet<string>(file_url);
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

        /// <summary>
        /// The logged in user.
        /// </summary>
        public FB_User user
        {
            get
            {
                // TODO: Consider caching the result
                return new FB_User(user_id, this);
            }
        }

        /// <summary>
        /// Facebook id of logged user 
        /// </summary>
        private string user_id
        {
            get
            {
                var cookies = (this._session.GetAllCookies().Values.SelectMany(c => c).Cast<Cookie>()
                    .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(c => c.Key, c => c.First().Value, StringComparer.OrdinalIgnoreCase));
                var rtn = cookies.GetValueOrDefault("c_user");
                if (rtn == null)
                    throw new FBchatException("Could not find user id");
                return rtn;
            }
        }

        private Dictionary<string, string> get_params()
        {
            this._counter += 1;
            var payload = new Dictionary<string, string>();
            payload["__a"] = 1.ToString();
            payload["__req"] = Utils.str_base(this._counter, 36);
            payload["__rev"] = this._revision;
            payload["fb_dtsg"] = this._fb_dtsg;
            return payload;
        }

        /// <summary>
        /// Logins using username and password
        /// </summary>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <param name="on_2fa_callback"></param>
        /// <param name="user_agent"></param>
        /// <returns></returns>
        public async static Task<Session> login(string email, string password, Func<Task<string>> on_2fa_callback, string user_agent = null)
        {
            var session = new Session(user_agent);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                throw new Exception("Email and password not set.");
            }

            var data = new Dictionary<string, string>() {
                // "jazoest": "2754",
                // "lsd": "AVqqqRUa",
                { "initial_request_id", "x"},  // any, just has to be present
                // "timezone": "-120",
                // "lgndim": "eyJ3IjoxNDQwLCJoIjo5MDAsImF3IjoxNDQwLCJhaCI6ODc3LCJjIjoyNH0=",
                // "lgnrnd": "044039_RGm9",
                { "lgnjs", "n"},
                { "email", email},
                { "pass", password},
                { "login", "1"},
                { "persistent", "1"}, // Changes the cookie type to have a long "expires"
                { "default_persistent", "0"},
            };

            var r = await session._cleanPost("https://www.messenger.com/login/password/", data, redirect: false);
            var content = (string)await Session.check_request(r, no_throw: true);
            var url = r.Headers.Contains("Location") ? r.Headers.Location.ToString() : null;

            if (url == null)
            {
                var error = session.get_error_data(content);
                throw new FBchatNotLoggedIn(error);
            }

            if (url.Contains("checkpoint"))
            {
                url = Utils.get_url_parameter(url, "next");
                r = await session._cleanGet<string>(url, redirect: true);
                var code = await on_2fa_callback();
                url = await session._2fa_helper(r, code);

                if (!url.StartsWith("https://www.messenger.com/login/auth_token/"))
                    throw new FBchatParseError("Failed 2fa flow", data: url);
                r = await session._cleanGet<string>(url, redirect: false);
                url = r.Headers.Contains("Location") ? r.Headers.Location.ToString() : null;
            }

            if (url == "https://www.messenger.com/")
            {
                return await Session.from_session(session);
            }
            else
            {
                throw new FBchatUserError(
                    string.Format("Login failed. Check email/password. (Failed on url: {0})",
                        r.RequestMessage.RequestUri.ToString())
                );
            }
        }

        /// <summary>
        /// Returns logged in status
        /// </summary>
        /// <returns></returns>
        public async Task<bool> is_logged_in()
        {
            // Send a request to the login url, to see if we're directed to the home page
            var r = await this._cleanGet<string>(Utils.prefix_url("/login/"), redirect: false);
            return (r.Headers.Contains("Location") && r.Headers.Location.ToString() == "https://www.messenger.com/");
        }

        /// <summary>
        /// Logouts current user
        /// </summary>
        /// <returns></returns>
        public async Task<bool> logout()
        {
            var data = new Dictionary<string, string>() {
                { "fb_dtsg", this._fb_dtsg }
            };

            var url = Utils.prefix_url("/logout/");
            var r = await this._cleanPost(url, data, redirect: false);

            return (r.Headers.Contains("Location") && r.Headers.Location.ToString() == "https://www.messenger.com/login/");
        }

        /// <summary>
        /// Build session from saved cookies
        /// </summary>
        /// <param name="session_cookies"></param>
        /// <param name="user_agent"></param>
        /// <returns></returns>
        public async static Task<Session> from_cookies(Dictionary<string, List<Cookie>> session_cookies, string user_agent)
        {
            var session = new Session(user_agent: user_agent);

            try
            {
                // Load cookies into current session
                foreach (string rawurl in session_cookies.Keys)
                {
                    // Need this because of new Uri(...)
                    var url = string.Format("https://{0}/", rawurl[0] == '.' ? rawurl.Substring(1) : rawurl);

                    var current_cookies = session._session.GetCookies(new Uri(url)).Cast<Cookie>();

                    foreach (var cookie in session_cookies[rawurl])
                    {
                        if (!current_cookies.Any(c => c.Name.Equals(cookie.Name)))
                        {
                            // Check if this is a domain cookie
                            var domain = rawurl[0] == '.' ? rawurl.Substring(1) : rawurl;
                            if (domain.StartsWith("messenger.com"))
                            {
                                // Add cookie to every subdomain
                                session._session.Add(new Uri(string.Format("https://{0}/", domain)), new Cookie(cookie.Name, cookie.Value));
                                session._session.Add(new Uri(string.Format("https://www.{0}/", domain)), new Cookie(cookie.Name, cookie.Value));
                                session._session.Add(new Uri(string.Format("https://m.{0}/", domain)), new Cookie(cookie.Name, cookie.Value));
                                session._session.Add(new Uri(string.Format("https://upload.{0}/", domain)), new Cookie(cookie.Name, cookie.Value));
                                foreach (var i in Enumerable.Range(0, 10))
                                    session._session.Add(new Uri(string.Format("https://{0}-edge-chat.{1}/", i, domain)), new Cookie(cookie.Name, cookie.Value)); // yuck!!
                            }
                            else
                            {
                                session._session.Add(new Uri(url), new Cookie(cookie.Name, cookie.Value));
                            }
                        }
                    }
                }
                return await Session.from_session(session);
            }
            catch (Exception)
            {
                return await Session.from_session(session);
            }
        }

        internal async Task<JToken> _get(string url, Dictionary<string, object> query = null, CancellationToken cancellationToken = default(CancellationToken), bool retry = true)
        {
            query.update(get_params());
            var r = await this._cleanGet(Utils.prefix_url(url), query: query, cancellationToken: cancellationToken);
            var content = await Session.check_request(r);
            var j = Utils.to_json(content);
            try
            {
                Utils.handle_payload_error(j);
                return j;
            }
            catch (FBchatPleaseRefresh ex)
            {
                if (retry)
                {
                    await this._do_refresh();
                    return await _get(url, query, cancellationToken, false);
                }
                throw ex;
            }
        }

        internal async Task<object> _post(string url, Dictionary<string, object> query = null, Dictionary<string, FB_File> files = null, bool as_graphql = false, CancellationToken cancellationToken = default(CancellationToken), bool retry = true)
        {
            query.update(get_params());
            var r = await this._cleanPost(Utils.prefix_url(url), query: query, files: files, cancellationToken: cancellationToken);
            var content = await Session.check_request(r);
            try
            {
                if (as_graphql)
                {
                    return GraphQL.response_to_json(content);
                }
                else
                {
                    var j = Utils.to_json(content);
                    // TODO: Remove this, and move it to _payload_post instead
                    // We can't yet, since errors raised in here need to be caught below
                    Utils.handle_payload_error(j);
                    return j;
                }
            }
            catch (FBchatPleaseRefresh ex)
            {
                if (retry)
                {
                    await this._do_refresh();
                    return await _post(url, query, files, as_graphql, cancellationToken);
                }
                throw ex;
            }
        }

        internal async Task<JToken> _payload_post(string url, Dictionary<string, object> data = null, Dictionary<string, FB_File> files = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var j = (JToken)await this._post(url, data, files: files, cancellationToken: cancellationToken);

            // update fb_dtsg token if received in response
            if (j?.get("jsmods") != null)
            {
                var define = Utils.get_jsmods_define(j?.get("jsmods")?.get("define")?.Value<JArray>());
                var fb_dtsg = get_fb_dtsg(define);
                if (fb_dtsg != null)
                    this._fb_dtsg = fb_dtsg;
            }

            try
            {
                return ((JToken)j).get("payload");
            }
            catch (Exception)
            {
                throw new FBchatException(string.Format("Missing payload: {0}", j));
            }
        }

        internal async Task<List<JToken>> graphql_requests(List<GraphQL> queries)
        {
            /*
             * :param queries: Zero or more dictionaries
             * :type queries: dict
             * : raises: FBchatException if request failed
             * : return: A tuple containing json graphql queries
             * :rtype: tuple
             * */
            var data = new Dictionary<string, object>(){
                { "method", "GET"},
                { "response_format", "json"},
                { "queries", GraphQL.queries_to_json(queries)}
            };

            return (List<JToken>)await this._post("/api/graphqlbatch/", data, as_graphql: true);
        }

        internal async Task<JToken> graphql_request(GraphQL query)
        {
            /*
             * Shorthand for `graphql_requests(query)[0]`
             * :raises: Exception if request failed
             */
            return (await this.graphql_requests(new[] { query }.ToList()))[0];
        }

        internal async Task<List<(string mimeKey, string fileType)>> _upload(List<FB_File> files, bool voice_clip = false)
        {
            /*
             * Uploads files to Facebook
             * `files` should be a list of files that requests can upload, see:
             * http://docs.python-requests.org/en/master/api/#requests.request
             * Returns a list of tuples with a file's ID and mimetype
             * */
            var file_dict = new Dictionary<string, FB_File>();
            foreach (var obj in files.Select((x, index) => new { f = x, i = index }))
                file_dict.Add(string.Format("upload_{0}", obj.i), obj.f);

            var data = new Dictionary<string, object>() { { "voice_clip", voice_clip } };

            var j = await this._payload_post(
                "https://upload.messenger.com/ajax/mercury/upload.php", data, files: file_dict
            );

            if (j.get("metadata").Count() != files.Count)
                throw new FBchatException(
                    string.Format("Some files could not be uploaded: {0}", j));

            return j.get("metadata").Select(md =>
                (md[Utils.mimetype_to_key(md.get("filetype")?.Value<string>())]?.Value<string>(), md.get("filetype")?.Value<string>())).ToList();
        }

        internal async Task<dynamic> _do_send_request(Dictionary<string, object> data, bool get_thread_id = false)
        {
            /* Sends the data to `SendURL`, and returns the message ID or null on failure */
            string messageAndOTID = Utils.generateOfflineThreadingID();
            long timestamp = Utils.now();
            var date = DateTime.Now;
            data.update(new Dictionary<string, object> {
                { "client", "mercury" },
                { "author" , "fbid:" + this.user_id },
                { "timestamp" , timestamp },
                { "source" , "source:chat:web" },
                { "offline_threading_id", messageAndOTID },
                { "message_id" , messageAndOTID },
                { "threading_id", Utils.generateMessageID(this._client_id) },
                { "ephemeral_ttl_mode:", "0" },
            });

            var j = (JToken)(await this._post("/messaging/send/", data));

            var fb_dtsg = Utils.get_jsmods_require(j, 2)?.Value<string>();
            if (fb_dtsg != null)
                this._fb_dtsg = fb_dtsg;

            try
            {
                var message_ids = j.get("payload")?.get("actions").Where(action => action.get("message_id") != null).Select(
                    action => new { MSG = action.get("message_id").Value<string>(), THR = action.get("thread_fbid").Value<string>() }
                ).ToList();
                if (message_ids.Count != 1)
                {
                    Debug.WriteLine(string.Format("Got multiple message ids back: {0}", message_ids));
                }
                if (get_thread_id)
                    return message_ids[0];
                else
                    return message_ids[0].MSG;
            }
            catch
            {
                throw new FBchatException(string.Format("Error when sending message: No message IDs could be found: {0}", j));
            }
        }

        /// <returns>Pretty string representation of the session</returns>
        public override string ToString()
        {
            return this.__unicode__();
        }

        private string __unicode__()
        {
            return string.Format("<Session user_id={0}>", this.user_id);
        }
    }
}
