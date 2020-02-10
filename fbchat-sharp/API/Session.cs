using AngleSharp.Dom;
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
        private const string FB_DTSG_REGEX = "name=\"fb_dtsg\" value=\"(.*?)\"";
        private const string facebookEncoding = "UTF-8";
        private HtmlParser _parser = null;
        private string _fb_dtsg = null;
        private string _revision = null;
        private int _counter = 0;
        private string _client_id = null;
        private string _logout_h = null;
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
                { "Referer", "https://www.facebook.com" },
                { "User-Agent", user_agent ?? Utils.USER_AGENTS[0] },
            };

            //this._client_id = ((int)(new Random().NextDouble() * Math.Pow(2, 31))).ToString("x4");
            this._client_id = Guid.NewGuid().ToString();
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
            return (uri.AbsolutePath.Contains("home") 
                || uri.AbsolutePath.Contains("gettingstarted")
                || uri.AbsolutePath == "/");
        }

        private static string get_decoded(byte[] content)
        {
            return Encoding.GetEncoding(facebookEncoding).GetString(content, 0, content.Length);
        }

        private async static Task<string> check_request(HttpResponseMessage r)
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

        private static async Task<Session> from_session(Session session)
        {
            // TODO: Automatically set user_id when the cookie changes in the session
            var r = await session._cleanGet<string>(Utils.prefix_url("/"));
            string content = await Session.check_request(r);
            var soup = session.find_input_fields(content);
            var fb_dtsg = soup.Where(i => i.GetAttribute("name").Equals("fb_dtsg")).Select(i => i.GetAttribute("value")).FirstOrDefault();
            if (fb_dtsg == null)
            {
                // Fall back to searching with a regex
                var regex = new Regex(Session.FB_DTSG_REGEX);
                fb_dtsg = regex.Match(content).Groups[1].Value;
            }

            var client_revision = content.Split(new[] { "\"client_revision\":" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(',')[0];

            var logout_h = soup.Where(i => i.GetAttribute("name").Equals("h")).Select(i => i.GetAttribute("value")).FirstOrDefault();

            return new Session()
            {
                _fb_dtsg = fb_dtsg,
                _revision = client_revision,
                _session = session._session,
                _logout_h = logout_h
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
            this._logout_h = new_state._logout_h ?? this._logout_h;
        }

        private async Task<HttpResponseMessage> _cleanGet<TValue>(string url, Dictionary<string, TValue> query = null, CancellationToken cancellationToken = default(CancellationToken))
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

            if ((int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                return response;
            else
                return await _cleanGet(response.Headers.Location.ToString(), query, cancellationToken);
        }

        private async Task<HttpResponseMessage> _cleanPost<TValue>(string url, Dictionary<string, TValue> query = null, Dictionary<string, FB_File> files = null, CancellationToken cancellationToken = default(CancellationToken))
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

            if ((int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                return response;
            else
                return await _cleanPost(response.Headers.Location.ToString(), query, files, cancellationToken);
        }

        private async Task<HttpResponseMessage> _postFile<TValue>(string url, Dictionary<string, TValue> query = null, Dictionary<string, FB_File> files = null, CancellationToken cancellationToken = default(CancellationToken))
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

            if ((int)r.StatusCode < 300 || (int)r.StatusCode > 399)
            {
                return r;
            }
            else
            {
                return await _postFile(r.Headers.Location.ToString(), query, files, cancellationToken);
            }
        }

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

        public async static Task<Session> login(string email, string password, Func<Task<string>> on_2fa_callback, string user_agent = null)
        {
            var session = new Session(user_agent);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                throw new Exception("Email and password not set.");
            }

            var r = await session._cleanGet<string>("https://m.facebook.com/");
            var soup = session.find_input_fields(await Session.check_request(r));

            var data = soup.Where(i => i.HasAttribute("name") && i.HasAttribute("value")).Select(i => new { Key = i.GetAttribute("name"), Value = i.GetAttribute("value") })
                .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(c => c.Key, c => c.First().Value, StringComparer.OrdinalIgnoreCase);

            data["email"] = email;
            data["pass"] = password;
            data["login"] = "Log In";

            r = await session._cleanPost("https://m.facebook.com/login.php?login_attempt=1", data);
            var content = (string)await Session.check_request(r);

            if (r.RequestMessage.RequestUri.ToString().Contains("checkpoint") &&
                (content.ToLower().Contains("id=\"approvals_code\"")))
            {
                var code = await on_2fa_callback();
                r = await session._2fa_helper(r, code);
            }

            // Sometimes Facebook tries to show the user a "Save Device" dialog
            if (r.RequestMessage.RequestUri.ToString().Contains("save-device"))
            {
                r = await session._cleanGet<string>("https://m.facebook.com/login/save-device/cancel/");
            }

            if (session.is_home(r.RequestMessage.RequestUri.ToString()))
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

        public async Task<bool> is_logged_in()
        {
            // Send a request to the login url, to see if we're directed to the home page
            var r = await this._cleanGet<string>("https://m.facebook.com/login.php?login_attempt=1");
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
                this._logout_h = regex.Match(await Session.check_request(h_r)).Groups[1].Value;
            }

            var data = new Dictionary<string, string>() {
                { "ref", "mb"},
                { "h", this._logout_h }
            };

            url = Utils.prefix_url("/logout.php");
            var r = await this._cleanGet(url, data);
            return r.IsSuccessStatusCode;
        }

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
                            if (domain.StartsWith("facebook.com"))
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
            var j = await this._post(url, data, files: files, cancellationToken: cancellationToken);
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
                "https://upload.facebook.com/ajax/mercury/upload.php", data, files: file_dict
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
