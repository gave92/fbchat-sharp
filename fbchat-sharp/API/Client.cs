using AngleSharp.Parser.Html;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http.Headers;

// [assembly: InternalsVisibleTo("FMessenger.Windows")]
// [assembly: InternalsVisibleTo("FMessenger.WindowsPhone")]
namespace fbchat_sharp.API
{
    /// <summary>
    /// Enum for messenger update events
    /// </summary>
    public enum UpdateStatus
    {
        /// <summary>
        /// A new message was received
        /// </summary>
        NEW_MESSAGE
    }

    /// <summary>
    /// 
    /// </summary>
    public class UpdateEventArgs : EventArgs
    {
        private UpdateStatus update_event;
        private object update;

        /// <param name="_update_event">UpdateStatus enum associated with this event</param>
        /// <param name="_data">object associated with this event, e.g. a FB_Message</param>
        public UpdateEventArgs(UpdateStatus _update_event, object _data)
        {
            this.update_event = _update_event;
            this.update = _data;
        }

        /// <returns>
        /// Returns the info associated with this event in a dynamic object
        /// event.Data.Type: UpdateStatus enum associated with this event
        /// event.Data.Update: object associated with this event, e.g. a FB_Message 
        /// </returns>
        public dynamic Data { get { return new { Type = update_event, Update = update }; } }
    }

    /// <summary>
    /// Messenger chat client implementation
    /// </summary>
    public class Client
    {
        /*
         * A client for the Facebook Chat (Messenger).
         * See https://fbchat.readthedocs.io for complete documentation of the API.
        */

        /// Whether the client is listening.Used when creating an external event loop to determine when to stop listening
        private bool listening = false;

        /// <summary>
        /// The ID of the client.
        /// Can be used as `thread_id`.
        /// Note: Modifying this results in undefined behaviour
        /// </summary>
        protected string uid = null;

        // Private variables
        private Dictionary<string, string> _header;
        private Dictionary<string, string> payloadDefault;
        private Dictionary<string, string> form;

        private CookieContainer _session
        {
            get { return HttpClientHandler?.CookieContainer; }
        }

        private HttpClientHandler HttpClientHandler;
        private HttpClient http_client;

        private long prev;
        private long tmp_prev;
        private long last_sync;

        private string email;
        private string password;
        private string seq;
        private int req_counter;
        private string client_id;
        private long start_time;
        private string user_channel;
        private string ttstamp;
        private string fb_dtsg;
        private string fb_h;

        private string default_thread_id;
        private ThreadType? default_thread_type;
        private string sticky;
        private string pool;
        private string client;

        /// <param name="user_agent">Optional custom user agent string</param>
        public Client(string user_agent = null)
        {
            /*
             * Initializes and logs in the client
             * :param email: Facebook `email`, `id` or `phone number`
             * :param password: Facebook account password
             * :param user_agent: Custom user agent to use when sending requests. If `null`, user agent will be chosen from a premade list(see: any:`utils.USER_AGENTS`)
             * :param max_tries: Maximum number of times to try logging in
             * :param session_cookies: Cookies from a previous session(Will default to login if these are invalid)
             * :type session_cookies: dict
             * :raises: Exception on failed login
            */

            this.HttpClientHandler = new HttpClientHandler() { UseCookies = true, CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
            this.http_client = new HttpClient(this.HttpClientHandler);

            this.sticky = null;
            this.pool = null;
            this.client = "mercury";
            this.default_thread_id = null;
            this.default_thread_type = null;
            this.req_counter = 1;
            this.seq = "0";
            this.payloadDefault = new Dictionary<string, string>();

            if (user_agent == null)
                user_agent = Utils.USER_AGENTS[0];

            this._header = new Dictionary<string, string>() {
                { "Content-Type", "application/x-www-form-urlencoded" },
                { "Referer", ReqUrl.BASE },
                { "Origin", ReqUrl.BASE },
                { "User-Agent", user_agent },
                // { "Connection", "keep-alive" },
            };
        }

        /// <summary>
        /// Tries to login using a list of provided cookies
        /// </summary>
        /// <param name="session_cookies">List of cookies</param>
        public async Task tryLogin(IEnumerable<Cookie> session_cookies = null)
        {
            // If session cookies aren't set, not properly loaded or gives us an invalid session, then do the login
            if (session_cookies == null || !await this.setSession(session_cookies) /*|| !await this.isLoggedIn()*/)
            {
                throw new Exception("Login failed");
            }
            else
            {
                return;
            }
        }

        /*
         * INTERNAL REQUEST METHODS
         */

        private Dictionary<string, string> _generatePayload(Dictionary<string, string> query = null)
        {
            /* Adds the following defaults to the payload:
             * __rev, __user, __a, ttstamp, fb_dtsg, __req
             */
            var payload = new Dictionary<string, string>(this.payloadDefault);
            if (query != null)
            {
                foreach (var entry in query)
                {
                    payload[entry.Key] = entry.Value;
                }
            }
            payload["__req"] = Utils.str_base(this.req_counter, 36);
            payload["seq"] = this.seq;
            this.req_counter += 1;
            return payload;
        }

        /// <summary>
        /// This fixes "Please try closing and re-opening your browser window" errors(1357004)
        /// This error usually happens after 1 - 2 days of inactivity
        /// It may be a bad idea to do this in an exception handler, if you have a better method, please suggest it!
        /// </summary>
        private async Task<bool> _fix_fb_errors(string error_code)
        {
            if (error_code == "1357004")
            {
                Debug.WriteLine("Got error #1357004. Doing a _postLogin, and resending request");
                await this._postLogin();
                return true;
            }
            return false;
        }

        private async Task<object> _get(string url, Dictionary<string, string> query = null, int timeout = 30, bool fix_request = false, bool as_json = false, int error_retries = 3)
        {
            var payload = this._generatePayload(query);
            var content = new FormUrlEncodedContent(payload);
            var query_string = await content.ReadAsStringAsync();
            var builder = new UriBuilder(url) { Query = query_string };
            var request = new HttpRequestMessage(HttpMethod.Get, builder.ToString());
            foreach (var header in this._header) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            // this.client.Timeout = TimeSpan.FromSeconds(timeout);
            var r = await http_client.SendAsync(request);
            // return this._session.get(url, headers: this._header, param: payload, timeout: timeout);

            if ((int)r.StatusCode < 300 || (int)r.StatusCode > 399)
            {
                if (!fix_request)
                    return r;
                try
                {
                    return await Utils.checkRequest(r, as_json: as_json);
                }
                catch (FBchatFacebookError e)
                {
                    if (error_retries > 0 && await this._fix_fb_errors(e.fb_error_code))
                    {
                        return this._get(url, query: query, timeout: timeout, fix_request: fix_request, as_json: as_json, error_retries: error_retries - 1);
                    }
                    throw e;
                }
            }
            else
            {
                return await _get(r.Headers.Location.ToString(), query: query, timeout: timeout, fix_request: fix_request, as_json: as_json, error_retries: error_retries);
            }
        }

        private async Task<object> _post(string url, Dictionary<string, string> query = null, int timeout = 30, bool fix_request = false, bool as_json = false, int error_retries = 3)
        {
            var payload = this._generatePayload(query);
            var content = new FormUrlEncodedContent(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            foreach (var header in this._header) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            request.Content = content;
            // this.client.Timeout = TimeSpan.FromSeconds(timeout);
            var r = await http_client.SendAsync(request);
            // return this._session.post(url, headers: this._header, data: payload, timeout: timeout);

            if ((int)r.StatusCode < 300 || (int)r.StatusCode > 399)
            {
                if (!fix_request)
                    return r;
                try
                {
                    return await Utils.checkRequest(r, as_json: as_json);
                }
                catch (FBchatFacebookError e)
                {
                    if (error_retries > 0 && await this._fix_fb_errors(e.fb_error_code))
                    {
                        return this._post(url, query: query, timeout: timeout, fix_request: fix_request, as_json: as_json, error_retries: error_retries - 1);
                    }
                    throw e;
                }
            }
            else
            {
                return await _post(r.Headers.Location.ToString(), query: query, timeout: timeout, fix_request: fix_request, as_json: as_json, error_retries: error_retries);
            }
        }

        private async Task<List<JToken>> _graphql(Dictionary<string, string> payload, int error_retries = 3)
        {
            try
            {
                var content = await this._post(ReqUrl.GRAPHQL, payload, fix_request: true, as_json: false);
                return GraphQL_JSON_Decoder.graphql_response_to_json((string)content);
            }
            catch (FBchatFacebookError e)
            {
                if (error_retries > 0 && await this._fix_fb_errors(e.fb_error_code))
                    return await this._graphql(payload, error_retries = error_retries - 1);
                throw e;
            }
        }

        private async Task<object> _cleanGet(string url, int timeout = 30)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var header in this._header) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            // this.client.Timeout = TimeSpan.FromSeconds(timeout);
            var response = await http_client.SendAsync(request);
            // return this._session.get(url, headers: this._header, param: query, timeout: timeout);

            if ((int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                return response;
            else
                return await _cleanGet(response.Headers.Location.ToString(), timeout);
        }

        private async Task<object> _cleanPost(string url, Dictionary<string, string> query = null, int timeout = 30)
        {
            this.req_counter += 1;
            var content = new FormUrlEncodedContent(query);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            foreach (var header in this._header) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            request.Content = content;
            // this.client.Timeout = TimeSpan.FromSeconds(timeout);
            var response = await http_client.SendAsync(request);
            // return this._session.post(url, headers: this._header, data: query, timeout: timeout);

            if ((int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                return response;
            else
                return await _cleanPost(response.Headers.Location.ToString(), query, timeout);
        }

        private async Task<object> _postFile(string url, FB_File[] files = null, Dictionary<string, string> query = null, int timeout = 30, bool fix_request = false, bool as_json = false, int error_retries = 3)
        {
            // return await Task.FromResult<HttpResponseMessage>(new HttpResponseMessage(HttpStatusCode.BadRequest));            
            var content = new MultipartFormDataContent();
            var payload = this._generatePayload(query);
            foreach (var keyValuePair in payload)
            {
                content.Add(new StringContent(keyValuePair.Value), keyValuePair.Key);
            }
            if (files != null)
            {
                foreach (var file in files)
                {
                    var image = new StreamContent(file.data);
                    image.Headers.ContentType = new MediaTypeHeaderValue(MimeMapping.MimeUtility.GetMimeMapping(file.path));
                    content.Add(image, "file", file.path);
                }
            }
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            // Removes 'Content-Type' from the header
            var headers = this._header.Where(h => h.Key != "Content-Type");

            foreach (var header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            request.Content = content;
            // this.client.Timeout = TimeSpan.FromSeconds(timeout);
            var r = await http_client.SendAsync(request);
            // var r = this._session.post(url, headers = headers, data = payload, timeout = timeout, files = files)

            if ((int)r.StatusCode < 300 || (int)r.StatusCode > 399)
            {
                if (!fix_request)
                    return r;
                try
                {
                    return await Utils.checkRequest(r, as_json: as_json);
                }
                catch (FBchatFacebookError e)
                {
                    if (error_retries > 0 && await this._fix_fb_errors(e.fb_error_code))
                    {
                        return this._postFile(url, files: files, query: query, timeout: timeout, fix_request: fix_request, as_json: as_json, error_retries: error_retries - 1);
                    }
                    throw e;
                }
            }
            else
            {
                return await _postFile(r.Headers.Location.ToString(), files, query: query, timeout: timeout, fix_request: fix_request, as_json: as_json, error_retries: error_retries);
            }
        }

        private async Task<List<JToken>> graphql_requests(List<GraphQL> queries)
        {
            /*
             * :raises: Exception if request failed
             */
            var payload = new Dictionary<string, string>(){
                { "method", "GET"},
                { "response_format", "json"},
                { "queries", GraphQL_JSON_Decoder.graphql_queries_to_json(queries)}
            };

            var j = await this._graphql(payload);

            return j;
        }

        private async Task<JToken> graphql_request(GraphQL query)
        {
            /*
             * Shorthand for `graphql_requests(query)[0]`
             * :raises: Exception if request failed
             */
            return (await this.graphql_requests(new[] { query }.ToList()))[0];
        }

        /*
         * END INTERNAL REQUEST METHODS
         */

        /*
         * LOGIN METHODS
         */

        private void _resetValues()
        {
            this.payloadDefault = new Dictionary<string, string>();
            this.HttpClientHandler = new HttpClientHandler()
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = false
            };
            this.http_client = new HttpClient(HttpClientHandler);
            this.req_counter = 1;
            this.seq = "0";
            this.uid = null;
        }

        private async Task _postLogin()
        {
            this.payloadDefault = new Dictionary<string, string>();
            this.client_id = ((int)(new Random().NextDouble() * 2147483648)).ToString("X4").Substring(2);
            this.start_time = Utils.now();
            var cookies = (this._session.GetCookies(new Uri(ReqUrl.BASE)).Cast<Cookie>()
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(c => c.Key, c => c.First().Value, StringComparer.OrdinalIgnoreCase));
            this.uid = cookies["c_user"];
            this.user_channel = "p_" + this.uid;
            this.ttstamp = "";

            var r = (HttpResponseMessage)(await this._get(ReqUrl.BASE));
            string soup = (string)await Utils.checkRequest(r, false);
            var parser = new HtmlParser();
            var document = parser.Parse(soup);
            this.fb_dtsg = document.QuerySelectorAll("input").Where(i => i.GetAttribute("name").Equals("fb_dtsg")).Select(i => i.GetAttribute("value")).First();
            this.fb_h = document.QuerySelectorAll("input").Where(i => i.GetAttribute("name").Equals("h")).Select(i => i.GetAttribute("value")).First();
            foreach (var i in this.fb_dtsg)
            {
                this.ttstamp += ((int)i).ToString();
            }
            this.ttstamp += "2";
            // Set default payload
            this.payloadDefault["__rev"] = soup.Split(new[] { "\"client_revision\":" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(',')[0];
            this.payloadDefault["__user"] = this.uid;
            this.payloadDefault["__a"] = "1";
            this.payloadDefault["ttstamp"] = this.ttstamp;
            this.payloadDefault["fb_dtsg"] = this.fb_dtsg;

            this.form = new Dictionary<string, string>() {
                { "channel", this.user_channel },
                { "partition", "-2" },
                { "clientid", this.client_id },
                { "viewer_uid", this.uid },
                { "uid", this.uid },
                { "state", "active" },
                { "format", "json" },
                { "idle", "\0" },
                { "cap", "8" }
            };

            this.prev = Utils.now();
            this.tmp_prev = Utils.now();
            this.last_sync = Utils.now();
        }

        private async Task<Tuple<bool, string>> _login()
        {
            if (string.IsNullOrEmpty(this.email) || string.IsNullOrEmpty(this.password))
            {
                throw new Exception("Email and password not found.");
            }

            var r = (HttpResponseMessage)(await this._get(ReqUrl.MOBILE));
            string soup = (string)await Utils.checkRequest(r, false);
            var parser = new HtmlParser();
            var document = parser.Parse(soup);
            var data = document.QuerySelectorAll("input").Where(i => i.HasAttribute("name") && i.HasAttribute("value")).Select(i => new { Key = i.GetAttribute("name"), Value = i.GetAttribute("value") })
                .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(c => c.Key, c => c.First().Value, StringComparer.OrdinalIgnoreCase);

            data["email"] = this.email;
            data["pass"] = this.password;
            data["login"] = "Log In";

            r = (HttpResponseMessage)(await this._cleanPost(ReqUrl.LOGIN, data));
            soup = (string)await Utils.checkRequest(r, false);

            if (r.RequestMessage.RequestUri.ToString().Contains("checkpoint") &&
                (soup.Contains("Enter Security Code to Continue") || soup.Contains("Enter Login Code to Continue")))
            {
                r = await this._2FA(r);
            }

            // Sometimes Facebook tries to show the user a "Save Device" dialog
            if (r.RequestMessage.RequestUri.ToString().Contains("save-device"))
            {
                r = (HttpResponseMessage)(await this._cleanGet(ReqUrl.SAVE_DEVICE));
            }

            if (r.RequestMessage.RequestUri.ToString().Contains("home"))
            {
                await this._postLogin();
                return Tuple.Create(true, r.RequestMessage.RequestUri.ToString());
            }
            else
            {
                return Tuple.Create(false, r.RequestMessage.RequestUri.ToString());
            }
        }

        private async Task<HttpResponseMessage> _2FA(HttpResponseMessage r)
        {
            return await Task.FromResult<HttpResponseMessage>(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }

        /// <summary>
        /// Sends a request to Facebook to check the login status
        /// </summary>
        /// <returns>Returns true if the client is still logged in</returns>
        public async Task<bool> isLoggedIn()
        {
            /*
             * Sends a request to Facebook to check the login status
             * :return: true if the client is still logged in
             * :rtype: bool
             */
            // Send a request to the login url, to see if we"re directed to the home page
            var r = (HttpResponseMessage)(await this._cleanGet(ReqUrl.LOGIN));
            return (r.RequestMessage.RequestUri.ToString().Contains("home"));
        }

        /// <summary>
        /// Retrieves session cookies
        /// </summary>
        /// <param name="url">Specify the url for which retrieve the cookies (optional)</param>
        /// <returns>Returns a list containing client session cookies</returns>
        public IEnumerable<Cookie> getSession(string url = null)
        {
            /*
             * Retrieves session cookies
             * :return: A list containing session cookies
             * : rtype: IEnumerable
             */
            return this._session.GetCookies(new Uri(url ?? ReqUrl.BASE)).Cast<Cookie>();
        }

        /// <summary>
        /// Sets client's session cookies
        /// </summary>
        /// <param name="session_cookies">A list of cookies</param>
        /// <returns>Returns false if `session_cookies` does not contain proper cookies</returns>
        public async Task<bool> setSession(IEnumerable<Cookie> session_cookies = null)
        {
            /*
             * Loads session cookies
             * :param session_cookies: A dictionay containing session cookies
             * : type session_cookies: dict
             * : return: false if `session_cookies` does not contain proper cookies
             * : rtype: bool
             */

            // Quick check to see if session_cookies is formatted properly
            if (session_cookies == null ||
                !(session_cookies.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(c => c.Key, c => c.First().Value, StringComparer.OrdinalIgnoreCase)).ContainsKey("c_user"))
            {
                return false;
            }

            try
            {
                // Load cookies into current session
                foreach (string url in new[] { ReqUrl.LISTEN, ReqUrl.BASE })
                {
                    var current_cookies = this._session.GetCookies(new Uri(url)).Cast<Cookie>();

                    foreach (var cookie in session_cookies)
                    {
                        if (!current_cookies.Any(c => c.Name.Equals(cookie.Name)))
                            this._session.Add(new Uri(url), new Cookie(cookie.Name, cookie.Value));
                    }
                }
                await this._postLogin();
            }
            catch (Exception)
            {
                this._resetValues();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to login using provided email and password
        /// </summary>
        /// <param name="email">User email address</param>
        /// <param name="password">User password</param>
        /// <param name="max_tries">Optional maximum number of retries</param>
        public async Task doLogin(string email, string password, int max_tries = 5)
        {
            /*
             * Uses `email` and `password` to login the user (If the user is already logged in, this will do a re-login)
             * :param email: Facebook `email` or `id` or `phone number`
             * :param password: Facebook account password
             * : param max_tries: Maximum number of times to try logging in
             * :type max_tries: int
             * :raises: Exception on failed login
             */
            if (max_tries < 1)
            {
                throw new Exception("Cannot login: max_tries should be at least one");
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                throw new Exception("Email and password not set");
            }

            this.email = email;
            this.password = password;

            // Holds result of last login
            Tuple<bool, string> tuple_login = null;

            foreach (int i in Enumerable.Range(1, max_tries + 1))
            {
                tuple_login = await this._login();
                if (!tuple_login.Item1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    this._resetValues();
                    continue;
                }
                else
                {
                    return;
                }
            }

            throw new Exception(string.Format("Login failed. Check email/password. (Failed on url: {0})", tuple_login.Item2));
        }

        /// <summary>
        /// Logs out and resets the client
        /// </summary>
        public async Task doLogout()
        {
            /*
             * Safely logs out the client
             * :param timeout: See `requests timeout < http://docs.python-requests.org/en/master/user/advanced/#timeouts>`_
             * :return: true if the action was successful
             * : rtype: bool
             */
            var data = new Dictionary<string, string>() {
                { "ref", "mb"},
                { "h", this.fb_h }
            };

            var r = (HttpResponseMessage)(await this._get(ReqUrl.LOGOUT, data));

            this._resetValues();

            if (r.IsSuccessStatusCode)
            {
                return;
            }
            else
            {
                throw new Exception("Logout failed");
            }
        }

        /*
         * END LOGIN METHODS
         */

        /*
         * DEFAULT THREAD METHODS
         */

        private Tuple<string, ThreadType?> _getThread(string given_thread_id = null, ThreadType? given_thread_type = ThreadType.USER)
        {
            /*
             * Checks if thread ID is given, checks if default is set and returns correct values
             * :raises ValueError: If thread ID is not given and there is no default
             * :return: Thread ID and thread type
             * : rtype: tuple
             */

            if (given_thread_id == null)
            {
                if (this.default_thread_id != null)
                {
                    return Tuple.Create(this.default_thread_id, this.default_thread_type);
                }
                else
                {
                    throw new ArgumentException("Thread ID is not set");
                }
            }
            else
            {
                return Tuple.Create(given_thread_id, given_thread_type);
            }
        }

        private void setDefaultThread(string thread_id, ThreadType? thread_type)
        {
            /*
             * Sets default thread to send messages to
             * :param thread_id: User / FGroup ID to default to.See :ref:`intro_threads`
             * :param thread_type: See:ref:`intro_threads`
             * :type thread_type: models.ThreadType
            */
            this.default_thread_id = thread_id;
            this.default_thread_type = thread_type;
        }

        private void resetDefaultThread()
        {
            /*Resets default thread*/
            this.setDefaultThread(null, null);
        }

        /*
        END DEFAULT THREAD METHODS
        */

        /*
         * FETCH METHODS
         */

        /// <summary>
        /// Gets all users the client is currently chatting with
        /// </summary>
        public async Task<List<FB_User>> fetchAllUsers()
        {
            /*
             * Gets all users the client is currently chatting with
             * :return: :class:`models.User` objects
             * :rtype: list
             * :raises: Exception if request failed
             */

            var data = new Dictionary<string, string>() {
                { "viewer", this.uid },
            };
            var j = (JToken)(await this._post(ReqUrl.ALL_USERS, query: data, fix_request: true, as_json: true));
            if (j["payload"] == null)
            {
                throw new Exception("Missing payload");
            }

            var users = new List<FB_User>();

            foreach (var u in j["payload"].Value<JObject>().Properties())
            {
                var k = u.Children().FirstOrDefault()?.Value<JObject>();
                if (k != null && new[] { "user", "friend" }.Contains(k["type"].Value<string>()))
                {
                    if (new[] { "0", "\0" }.Contains(k["id"].Value<string>()))
                    {
                        // Skip invalid users
                        continue;
                    }
                    users.Add(new FB_User(k["id"].Value<string>(), first_name: k["firstName"].Value<string>(), url: k["uri"].Value<string>(), photo: k["thumbSrc"].Value<string>(), name: k["name"].Value<string>(), is_friend: k["is_friend"].Value<bool>(), gender: GENDER.standard_GENDERS[k["gender"].Value<int>()]));
                }
            }

            return users;
        }

        /// <summary>
        /// Find and get user by his/her name
        /// </summary>
        /// <param name="name">Name of the user</param>
        /// <param name="limit">The max. amount of users to fetch</param>
        public async Task<List<FB_User>> searchForUsers(string name, int limit = 1)
        {
            /*
             * Find and get user by his/ her name
             : param name: Name of the user
             :param limit: The max. amount of users to fetch
             : return: :class:`models.User` objects, ordered by relevance
             :rtype: list
             :raises: Exception if request failed
             */

            var j = await this.graphql_request(new GraphQL(query: GraphQL.SEARCH_USER, param: new Dictionary<string, object>() {
                { "search", name }, { "limit", limit.ToString() }
             }));

            return j[name]["users"]["nodes"].Select(node => GraphQL_JSON_Decoder.graphql_to_user(node)).ToList();
        }

        /// <summary>
        /// Find and get page by its name
        /// </summary>
        /// <param name="name">Name of the page</param>
        /// <param name="limit">The max. amount of pages to fetch</param>
        public async Task<List<FB_Page>> searchForPages(string name, int limit = 1)
        {
            /*
             * Find and get page by its name
             * : param name: Name of the page
             * :return: :class:`models.Page` objects, ordered by relevance
             * :rtype: list
             * :raises: Exception if request failed
             */

            var j = await this.graphql_request(new GraphQL(query: GraphQL.SEARCH_PAGE, param: new Dictionary<string, object>() {
                { "search", name }, { "limit", limit.ToString() }
            }));

            return j[name]["pages"]["nodes"].Select(node => GraphQL_JSON_Decoder.graphql_to_page(node)).ToList();
        }

        /// <summary>
        /// Find and get group thread by its name
        /// </summary>
        /// <param name="name">Name of the group</param>
        /// <param name="limit">The max. amount of groups to fetch</param>
        public async Task<List<FB_Group>> searchForGroups(string name, int limit = 1)
        {
            /*
             * Find and get group thread by its name
             * :param name: Name of the group thread
             * : param limit: The max. amount of groups to fetch
             * : return: :class:`models.FGroup` objects, ordered by relevance
             * :rtype: list
             * :raises: Exception if request failed
             * */

            var j = await this.graphql_request(new GraphQL(query: GraphQL.SEARCH_GROUP, param: new Dictionary<string, object>() {
              { "search", name }, {"limit", limit.ToString() }
            }));

            return j["viewer"]["groups"]["nodes"].Select(node => GraphQL_JSON_Decoder.graphql_to_group(node)).ToList();
        }

        /// <summary>
        /// Find and get a thread by its name
        /// </summary>
        /// <param name="name">Name of the thread</param>
        /// <param name="limit">The max. amount of threads to fetch</param>
        public async Task<List<FB_Thread>> searchForThreads(string name, int limit = 1)
        {
            /*
             * Find and get a thread by its name
             * :param name: Name of the thread
             * :param limit: The max. amount of groups to fetch
             * : return: :class:`models.User`, :class:`models.FGroup` and :class:`models.Page` objects, ordered by relevance
             * :rtype: list
             * :raises: Exception if request failed
             */

            var j = await this.graphql_request(new GraphQL(query: GraphQL.SEARCH_THREAD, param: new Dictionary<string, object>(){
                { "search", name }, {"limit", limit.ToString() }
            }));

            List<FB_Thread> rtn = new List<FB_Thread>();

            foreach (var node in j[name]["threads"]["nodes"])
            {
                if (node["__typename"].Value<string>().Equals("User"))
                {
                    rtn.Add(GraphQL_JSON_Decoder.graphql_to_user(node));
                }
                else if (node["__typename"].Value<string>().Equals("MessageThread"))
                {
                    // MessageThread => FGroup thread
                    rtn.Add(GraphQL_JSON_Decoder.graphql_to_group(node));
                }
                else if (node["__typename"].Value<string>().Equals("Page"))
                {
                    rtn.Add(GraphQL_JSON_Decoder.graphql_to_page(node));
                }
                else if (node["__typename"].Value<string>().Equals("Group"))
                {
                    // We don"t handle Facebook "Groups"
                    continue;
                }
                // TODO Add Rooms
                else
                {
                    Debug.WriteLine(string.Format("Unknown __typename: {0} in {1}", node["__typename"].Value<string>(), node));
                }
            }

            return rtn;
        }

        private async Task<JObject> _fetchInfo(string[] ids)
        {
            var data = new Dictionary<string, string>();
            foreach (var obj in ids.Select((x, index) => new { _id = x, i = index }))
                data.Add(string.Format("ids[{0}]", obj.i), obj._id);

            var j = (JToken)(await this._post(ReqUrl.INFO, data, fix_request: true, as_json: true));

            if (j["payload"]["profiles"] == null)
            {
                throw new FBchatException("No users/pages returned");
            }

            var entries = new JObject();

            foreach (var k in j["payload"]["profiles"].Value<JObject>().Properties())
            {
                if (new[] { "user", "friend" }.Contains(k.Value["type"].Value<string>()))
                {
                    entries[k.Name] = new JObject() {
                        { "id", k.Name },
                        {"type", (int)ThreadType.USER },
                        {"url", k.Value["uri"].Value<string>() },
                        {"first_name", k.Value["firstName"].Value<string>() },
                        {"is_viewer_friend", k.Value["is_friend"].Value<bool>() },
                        {"gender", k.Value["gender"].Value<string>() },
                        {"profile_picture", new JObject() { { "uri", k.Value["thumbSrc"].Value<string>() } } },
                        { "name", k.Value["name"].Value<string>() }
                    };
                }
                else if (k.Value["type"].Value<string>().Equals("page"))
                {
                    entries[k.Name] = new JObject() {
                        { "id", k.Name},
                        { "type", (int)ThreadType.PAGE},
                        {"url", k.Value["uri"].Value<string>()},
                        {"profile_picture", new JObject() { { "uri", k.Value["thumbSrc"].Value<string>() } } },
                        { "name", k.Value["name"].Value<string>() }
                    };
                }
                else
                {
                    throw new FBchatException(string.Format("{0} had an unknown thread type: {1}", k.Name, k.Value));
                }
            }

            return entries;
        }

        /// <summary>
        /// Get users' info from IDs, unordered
        /// </summary>
        /// <param name="user_ids">One or more user ID(s) to query</param>
        /// <returns>A dictionary of FB_User objects, labeled by their ID</returns>
        public async Task<Dictionary<string, FB_User>> fetchUserInfo(string[] user_ids)
        {
            /*
             * Get users' info from IDs, unordered
             * ..warning::
             * Sends two requests, to fetch all available info!
             * :param user_ids: One or more user ID(s) to query
             * :return: :class:`models.User` objects, labeled by their ID
             * :rtype: dict
             * :raises: Exception if request failed
             */

            var threads = await this.fetchThreadInfo(user_ids);
            var users = new Dictionary<string, FB_User>();

            foreach (var k in threads.Keys)
            {
                if (threads[k].type == ThreadType.USER)
                {
                    users[k] = (FB_User)threads[k];
                }
                else
                {
                    throw new FBchatUserError(string.Format("Thread {0} was not a user", threads[k]));
                }
            }

            return users;
        }

        /// <summary>
        /// Get pages' info from IDs, unordered
        /// </summary>
        /// <param name="page_ids">One or more page ID(s) to query</param>
        /// <returns>A dictionary of FB_Page objects, labeled by their ID</returns>
        public async Task<Dictionary<string, FB_Page>> fetchPageInfo(string[] page_ids)
        {
            /*
             * Get pages" info from IDs, unordered
             * ..warning::
             * Sends two requests, to fetch all available info!
             * :param page_ids: One or more page ID(s) to query
             * :return: :class:`models.Page` objects, labeled by their ID
             * :rtype: dict
             * :raises: Exception if request failed
             */

            var threads = await this.fetchThreadInfo(page_ids);
            var pages = new Dictionary<string, FB_Page>();

            foreach (var k in threads.Keys)
            {
                if (threads[k].type == ThreadType.PAGE)
                {
                    pages[k] = (FB_Page)threads[k];
                }
                else
                {
                    throw new FBchatUserError(string.Format("Thread {0} was not a page", threads[k]));
                }
            }

            return pages;
        }

        /// <summary>
        /// Get groups' info from IDs, unordered
        /// </summary>
        /// <param name="group_ids">One or more group ID(s) to query</param>
        /// <returns>A dictionary of FB_Group objects, labeled by their ID</returns>
        public async Task<Dictionary<string, FB_Group>> fetchGroupInfo(string[] group_ids)
        {
            /*
             * Get groups" info from IDs, unordered
             * :param group_ids: One or more group ID(s) to query
             * :return: :class:`models.FGroup` objects, labeled by their ID
             * :rtype: dict
             * :raises: Exception if request failed
             */

            var threads = await this.fetchThreadInfo(group_ids);
            var groups = new Dictionary<string, FB_Group>();

            foreach (var k in threads.Keys)
            {
                if (threads[k].type == ThreadType.GROUP)
                {
                    groups[k] = (FB_Group)threads[k];
                }
                else
                {
                    throw new FBchatUserError(string.Format("Thread {0} was not a group", threads[k]));
                }
            }

            return groups;
        }

        /// <summary>
        /// Get threads' info from IDs, unordered
        /// </summary>
        /// <param name="thread_ids">One or more thread ID(s) to query</param>
        /// <returns>A dictionary of FB_Thread objects, labeled by their ID</returns>
        public async Task<Dictionary<string, FB_Thread>> fetchThreadInfo(string[] thread_ids)
        {
            /*
             * Get threads" info from IDs, unordered
             * ..warning::
             * Sends two requests if users or pages are present, to fetch all available info!
             * :param thread_ids: One or more thread ID(s) to query
             * :return: :class:`models.Thread` objects, labeled by their ID
             * :rtype: dict
             * :raises: Exception if request failed
             */

            var queries = new List<GraphQL>();
            foreach (var thread_id in thread_ids)
            {
                queries.Add(new GraphQL(doc_id: "1386147188135407", param: new Dictionary<string, object>() {
                    { "id", thread_id },
                    { "message_limit", 0.ToString() },
                    { "load_messages", false.ToString() },
                    { "load_read_receipts", false.ToString() },
                    { "before", null }
                }));
            }

            var j = await this.graphql_requests(queries);

            foreach (var obj in j.Select((x, index) => new { entry = x, i = index }))
            {
                if (obj.entry["message_thread"] == null || obj.entry["message_thread"].Type == JTokenType.Null)
                {
                    // If you don"t have an existing thread with this person, attempt to retrieve user data anyways
                    j[obj.i]["message_thread"] = new JObject(
                                                    new JProperty("thread_key",
                                                        new JObject(
                                                            new JProperty("other_user_id", thread_ids[obj.i]))),
                                                    new JProperty("thread_type", "ONE_TO_ONE"));
                }
            }

            var pages_and_user_ids = j.Where(k => k["message_thread"]["thread_type"].Value<string>().Equals("ONE_TO_ONE"))
                .Select(k => k["message_thread"]["thread_key"]["other_user_id"].Value<string>());
            JObject pages_and_users = null;
            if (pages_and_user_ids.Count() != 0)
            {
                pages_and_users = await this._fetchInfo(pages_and_user_ids.ToArray());
            }

            var rtn = new Dictionary<string, FB_Thread>();
            foreach (var obj in j.Select((x, index) => new { entry = x, i = index }))
            {
                var entry = obj.entry["message_thread"];
                if (entry["thread_type"].Value<string>().Equals("GROUP"))
                {
                    var _id = entry["thread_key"]["thread_fbid"].Value<string>();
                    rtn[_id] = GraphQL_JSON_Decoder.graphql_to_group(entry);
                }
                else if (entry["thread_type"].Value<string>().Equals("ROOM"))
                {
                    var _id = entry["thread_key"]["thread_fbid"].Value<string>();
                    rtn[_id] = GraphQL_JSON_Decoder.graphql_to_room(entry);
                }
                else if (entry["thread_type"].Value<string>().Equals("ONE_TO_ONE"))
                {
                    var _id = entry["thread_key"]["other_user_id"].Value<string>();
                    if (pages_and_users[_id] == null)
                    {
                        throw new FBchatException(string.Format("Could not fetch thread {0}", _id));
                    }
                    foreach (var elem in pages_and_users[_id])
                    {
                        entry[((JProperty)elem).Name] = ((JProperty)elem).Value;
                    }
                    if (entry["type"].Value<int>() == (int)ThreadType.USER)
                    {
                        rtn[_id] = GraphQL_JSON_Decoder.graphql_to_user(entry);
                    }
                    else
                    {
                        rtn[_id] = GraphQL_JSON_Decoder.graphql_to_page(entry);
                    }
                }
                else
                {
                    throw new FBchatException(string.Format("{0} had an unknown thread type: {1}", thread_ids[obj.i], entry));
                }
            }

            return rtn;
        }

        /// <summary>
        /// Get the last messages in a thread
        /// </summary>
        /// <param name="thread_id">User / Group ID from which to retrieve the messages</param>
        /// <param name="limit">Max.number of messages to retrieve</param>
        /// <param name="before">A unix timestamp, indicating from which point to retrieve messages</param>
        /// <returns></returns>
        public async Task<List<FB_Message>> fetchThreadMessages(string thread_id = null, int limit = 20, string before = null)
        {
            /*
             * Get the last messages in a thread
             * :param thread_id: User / Group ID to default to.See :ref:`intro_threads`
             * :param limit: Max.number of messages to retrieve
             * : param before: A timestamp, indicating from which point to retrieve messages
             * :type limit: int
             * :type before: int
             * :return: :class:`models.Message` objects
             * :rtype: list
             * :raises: Exception if request failed
             */

            var thread = this._getThread(thread_id, null);
            thread_id = thread.Item1;

            var dict = new Dictionary<string, object>() {
                { "id", thread_id},
                { "message_limit", limit.ToString()},
                { "load_messages", true.ToString()},
                { "load_read_receipts", false.ToString()},
                { "before", before }
            };

            var j = await this.graphql_request(new GraphQL(doc_id: "1386147188135407", param: dict));

            if (j["message_thread"] == null || j["message_thread"].Type == JTokenType.Null)
            {
                throw new FBchatException(string.Format("Could not fetch thread {0}", thread_id));
            }

            return j["message_thread"]["messages"]["nodes"].Select(message => GraphQL_JSON_Decoder.graphql_to_message(thread_id, message)).Reverse().ToList();
        }

        /// <summary>
        /// Get thread list of your facebook account
        /// </summary>
        /// <param name="offset">The offset, from where in the list to recieve threads from</param>
        /// <param name="limit">Max.number of threads to retrieve. Capped at 20</param>
        /// <param name="thread_location">models.ThreadLocation: INBOX, PENDING, ARCHIVED or OTHER</param>
        /// <param name="before">A unix timestamp, indicating from which point to retrieve messages</param>
        public async Task<List<FB_Thread>> fetchThreadListQL(int offset = 0, int limit = 20, string thread_location = ThreadLocation.INBOX, string before = null)
        {
            /*
             * Get thread list of your facebook account
             * :param offset: The offset, from where in the list to recieve threads from
             * :param limit: Max.number of threads to retrieve.Capped at 20
             * :type offset: int
             * :type limit: int
             * :return: :class:`models.Thread` objects
             * :rtype: list
             * :raises: Exception if request failed
             */

            if (limit > 20 || limit < 1)
            {
                throw new FBchatUserError("`limit` should be between 1 and 20");
            }

            var dict = new Dictionary<string, object>() {
                { "limit", limit.ToString() },
                { "tags", new string[] { thread_location } },
                { "before", before },
                { "includeDeliveryReceipts", true.ToString() },
                { "includeSeqID", false.ToString() }
            };

            var j = await this.graphql_request(new GraphQL(doc_id: "1349387578499440", param: dict));

            return j["viewer"]["message_threads"]["nodes"].Select(node => GraphQL_JSON_Decoder.graphql_to_thread(node)).ToList();
        }

        /// <summary>
        /// Get thread list of your facebook account
        /// </summary>
        /// <param name="offset">The offset, from where in the list to recieve threads from</param>
        /// <param name="limit">Max.number of threads to retrieve. Capped at 20</param>
        /// <param name="thread_location">models.ThreadLocation: INBOX, PENDING, ARCHIVED or OTHER</param>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.fetchThreadListQL` instead")]
        public async Task<List<FB_Thread>> fetchThreadList(int offset = 0, int limit = 20, string thread_location = ThreadLocation.INBOX)
        {
            /*
             * Get thread list of your facebook account
             * :param offset: The offset, from where in the list to recieve threads from
             * :param limit: Max.number of threads to retrieve.Capped at 20
             * :type offset: int
             * :type limit: int
             * :return: :class:`models.Thread` objects
             * :rtype: list
             * :raises: Exception if request failed
             */

            if (limit > 20 || limit < 1)
            {
                throw new FBchatUserError("`limit` should be between 1 and 20");
            }

            var data = new Dictionary<string, string>() {
                { "client", this.client},
                { $"{thread_location}[offset]", offset.ToString()},
                { $"{thread_location}[limit]", limit.ToString()},
            };

            var j = (JToken)(await this._post(ReqUrl.THREADS, data, fix_request: true, as_json: true));
            if (j["payload"] == null || !j["payload"].Children().Any())
            {
                throw new FBchatException(string.Format("Missing payload: {0}, with data: {1}", j, data));
            }

            var participants = new Dictionary<string, FB_Thread>();
            if (j["payload"]["participants"] != null)
            {
                foreach (var p in j["payload"]["participants"])
                {
                    if (p["type"].Value<string>() == "page")
                    {
                        participants[p["fbid"].Value<string>()] = new FB_Page(p["fbid"].Value<string>(), url: p["href"].Value<string>(), photo: p["image_src"].Value<string>(), name: p["name"].Value<string>());
                    }
                    else if (p["type"].Value<string>() == "user")
                    {
                        participants[p["fbid"].Value<string>()] = new FB_User(p["fbid"].Value<string>(), url: p["href"].Value<string>(), first_name: p["short_name"].Value<string>(), is_friend: p["is_friend"].Value<bool>(), gender: GENDER.standard_GENDERS[p["gender"].Value<int>()], photo: p["image_src"].Value<string>(), name: p["name"].Value<string>());
                    }
                    else
                    {
                        throw new FBchatException(string.Format("A participant had an unknown type {0}: {1}", p["type"].Value<string>(), p));
                    }
                }
            }

            var entries = new List<FB_Thread>();
            if (j["payload"]["threads"] != null)
            {
                foreach (var k in j["payload"]["threads"])
                {
                    if (k["thread_type"].Value<int>() == 1)
                    {
                        if (!participants.ContainsKey(k["other_user_fbid"].Value<string>()))
                        {
                            throw new FBchatException(string.Format("A thread was not in participants: {0}", j["payload"]));
                        }
                        participants[k["other_user_fbid"].Value<string>()].message_count = k["message_count"].Value<int>();
                        entries.Add(participants[k["other_user_fbid"].Value<string>()]);
                    }
                    else if (k["thread_type"].Value<int>() == 2)
                    {
                        var part = new HashSet<string>(k["participants"].Select(p => p.Value<string>().Replace("fbid:", "")));
                        entries.Add(new FB_Group(k["thread_fbid"].Value<string>(), participants: part, photo: k["image_src"].Value<string>(), name: k["name"].Value<string>(), message_count: k["message_count"].Value<int>()));
                    }
                    else if (k["thread_type"].Value<int>() == 3)
                    {
                        var part = new HashSet<string>(k["participants"].Select(p => p.Value<string>().Replace("fbid:", "")));
                        var adm = new HashSet<string>(k["admin_ids"].Select(p => p.Value<string>().Replace("fbid:", "")));
                        var req = new HashSet<string>(k["approval_queue_ids"].Select(p => p.Value<string>().Replace("fbid:", "")));
                        entries.Add(new FB_Room(k["thread_fbid"].Value<string>(),
                            participants: part,
                            photo: k["image_src"].Value<string>(),
                            name: k["name"].Value<string>(),
                            message_count: k["message_count"].Value<int>(),
                            admins: adm,
                            approval_mode: k["approval_mode"].Value<bool>(),
                            approval_requests: req,
                            join_link: k["joinable_mode"]["link"].Value<string>()));
                    }
                    else
                    {
                        throw new FBchatException(string.Format("A thread had an unknown thread type: {0}", k));
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Get unread user messages
        /// </summary>
        /// <returns>Returns unread message ids and count</returns>
        public async Task<Dictionary<string, object>> fetchUnread()
        {
            /*
             * ..todo::
             * Documenting this
             * :raises: Exception if request failed
             */

            var form = new Dictionary<string, string>() {
                { "client", "mercury_sync"},
                { "folders[0]", "inbox"},
                { "last_action_timestamp", (Utils.now() - 60 * 1000).ToString()},
                { "last_action_timestamp", 0.ToString()}
            };

            var j = (JToken)(await this._post(ReqUrl.THREAD_SYNC, form, fix_request: true, as_json: true));

            return new Dictionary<string, object>() {
                { "message_counts", j["payload"]["message_counts"].Value<int>() },
                { "unseen_threads", j["payload"]["unseen_thread_ids"] }
            };
        }

        /// <summary>
        /// Fetches the url to the original image from an image attachment ID
        /// </summary>
        /// <returns>An url where you can download the original image</returns>
        public async Task<string> fetchImageUrl(string image_id)
        {
            /*
             * Fetches the url to the original image from an image attachment ID
             * :param image_id: The image you want to fethc
             * :type image_id: str
             * : return: An url where you can download the original image
             * : rtype: str
             * : raises: FBChatException if request failed
             */

            var form = new Dictionary<string, string>() {
                { "photo_id", image_id},
            };

            var j = (JToken)await Utils.checkRequest((HttpResponseMessage)(await this._post(ReqUrl.ATTACHMENT_PHOTO, form)));

            var url = Utils.get_jsmods_require(j, 3);
            if (url == null)
                throw new FBchatException(string.Format("Could not fetch image url from: {0}", j));
            return url.Value<string>();
        }

        /*
        END FETCH METHODS
        */

        /*
         * SEND METHODS
         */

        private FB_Message _oldMessage(object message)
        {
            return message is FB_Message ? (FB_Message)message : new FB_Message((string)message);
        }

        private Dictionary<string, string> _getSendData(FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*Returns the data needed to send a request to `SendURL`*/
            string messageAndOTID = Utils.generateOfflineThreadingID();
            long timestamp = Utils.now();
            var date = DateTime.Now;
            var data = new Dictionary<string, string> {
                { "client", this.client },
                { "author" , "fbid:" + this.uid },
                { "timestamp" , timestamp.ToString() },
                { "source" , "source:chat:web" },
                { "offline_threading_id", messageAndOTID },
                { "message_id" , messageAndOTID },
                { "threading_id", Utils.generateMessageID(this.client_id) },
                { "ephemeral_ttl_mode:", "0" },
            };

            // Set recipient
            if (new[] { ThreadType.USER, ThreadType.PAGE }.Contains(thread_type))
            {
                data["other_user_fbid"] = thread_id;
            }
            else if (thread_type == ThreadType.GROUP)
            {
                data["thread_fbid"] = thread_id;
            }

            if (message == null)
                message = new FB_Message();

            if (message.text != null || message.sticker != null || message.emoji_size != null)
                data["action_type"] = "ma-type:user-generated-message";

            if (message.text != null)
                data["body"] = message.text;

            foreach (var item in message.mentions.Select((mention, i) => new { i, mention }))
            {
                data[string.Format("profile_xmd[{0}][id]", item.i)] = item.mention.thread_id;
                data[string.Format("profile_xmd[{0}][offset]", item.i)] = item.mention.offset.ToString();
                data[string.Format("profile_xmd[{0}][length]", item.i)] = item.mention.length.ToString();
                data[string.Format("profile_xmd[{0}][type]", item.i)] = "p";
            }

            if (message.emoji_size != null)
            {
                if (message.text != null)
                    data["tags[0]"] = "hot_emoji_size:" + Enum.GetName(typeof(EmojiSize), message.emoji_size).ToLower();
                else
                    data["sticker_id"] = message.emoji_size?.GetEnumDescriptionAttribute();
            }

            if (message.sticker != null)
            {
                data["sticker_id"] = message.sticker.uid;
            }

            return data;
        }

        private async Task<string> _doSendRequest(Dictionary<string, string> data)
        {
            /*Sends the data to `SendURL`, and returns the message ID or null on failure*/
            var j = (JToken)(await this._post(ReqUrl.SEND, data, fix_request: true, as_json: true));
            string message_id = null;

            try
            {
                var message_ids = j["payload"]["actions"].Where(action => action["message_id"] != null).Select(action => action["message_id"].Value<string>()).ToList();
                if (message_ids.Count != 1)
                {
                    Debug.WriteLine(string.Format("Got multiple message ids back: {0}", message_ids));
                }
                message_id = message_ids[0];
            }
            catch
            {
                throw new FBchatException(string.Format("Error when sending message: No message IDs could be found: {0}", j));
            }

            fb_dtsg = Utils.get_jsmods_require(j, 2)?.Value<string>();
            if (fb_dtsg != null)
                this.payloadDefault["fb_dtsg"] = fb_dtsg;

            return message_id;
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="thread_id">User / Group ID to send to</param>
        /// <param name="thread_type">ThreadType enum</param>
        /// <returns>Message ID of the sent message</returns>
        public async Task<string> send(FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*
             * Sends a message to a thread
             * :param message: Message to send
             * :param thread_id: User/Group ID to send to. See :ref:`intro_threads`
             * :param thread_type: See :ref:`intro_threads`
             * :type message: models.Message
             * :type thread_type: models.ThreadType
             * :return: :ref:`Message ID <intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             */

            var thread = this._getThread(thread_id, thread_type);
            var data = this._getSendData(message: message, thread_id: thread_id, thread_type: thread_type);

            return await this._doSendRequest(data);
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.send` instead")]
        public async Task<string> sendMessage(string message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            return await this.send(new FB_Message(text: message), thread_id: thread_id, thread_type: thread_type);
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.send` instead")]
        public async Task<string> sendMessage(string emoji = null, EmojiSize size = EmojiSize.SMALL, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            return await this.send(new FB_Message(text: emoji), thread_id: thread_id, thread_type: thread_type);
        }

        /// <summary>
        /// Upload an image and get the image_id for sending in a message
        /// </summary>
        /// <param name="image_path"></param>
        /// <param name="data"></param>
        /// <param name="mimetype"></param>
        /// <returns></returns>
        public async Task<string> _uploadImage(string image_path = null, Stream data = null, string mimetype = null)
        {
            var j = (JToken)await this._postFile(ReqUrl.UPLOAD, new FB_File[] { new FB_File(data, image_path) }, fix_request: true, as_json: true);
            // Return the image_id
            if (mimetype != "image/gif")
                return j["payload"]["metadata"][0]["image_id"].Value<string>();
            else
                return j["payload"]["metadata"][0]["gif_id"].Value<string>();
        }

        /// <summary>
        /// Sends an image to a thread
        /// </summary>
        [Obsolete("Deprecated.Use :func:`fbchat.Client.send` instead")]
        public async Task<string> sendImage(string image_id = null, object message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER, bool is_gif = false)
        {
            var thread = this._getThread(thread_id, thread_type);
            var data = this._getSendData(message: this._oldMessage(message), thread_id: thread.Item1, thread_type: (ThreadType)thread.Item2);

            data["action_type"] = "ma-type:user-generated-message";
            data["has_attachment"] = true.ToString();

            if (!is_gif)
                data["image_ids[0]"] = image_id;
            else
                data["gif_ids[0]"] = image_id;

            return await this._doSendRequest(data);
        }

        /// <summary>
        /// Sends an image from a URL to a thread
        /// </summary>
        /// <param name="image_url"></param>
        /// <param name="message"></param>
        /// <param name="thread_id"></param>
        /// <param name="thread_type"></param>
        /// <returns></returns>
        public async Task<string> sendRemoteImage(string image_url = null, object message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*
             * Sends an image from a URL to a thread
             * : param image_url: URL of an image to upload and send
             * :param message: Additional message
             * :param thread_id: User / Group ID to send to.See: ref:`intro_threads`
             * :param thread_type: See: ref:`intro_threads`
             * :type thread_type: models.ThreadType
             * :return: :ref:`Message ID<intro_message_ids>` of the sent image
             * :raises: FBchatException if request failed
             */

            var thread = this._getThread(thread_id, thread_type);
            var r = (HttpResponseMessage)(await this._cleanGet(image_url));
            var mimetype = r.Content.Headers.ContentType.MediaType;
            bool is_gif = (mimetype == "image/gif");
            var image_id = await this._uploadImage(image_url, await r.Content.ReadAsStreamAsync(), mimetype);
            return await this.sendImage(image_id: image_id, message: message, thread_id: thread_id, thread_type: thread_type, is_gif: is_gif);
        }

        /// <summary>
        /// Sends a local image to a thread
        /// </summary>
        /// <param name="image_path"></param>
        /// <param name="data"></param>
        /// <param name="message"></param>
        /// <param name="thread_id"></param>
        /// <param name="thread_type"></param>
        /// <returns></returns>
        public async Task<string> sendLocalImage(string image_path = null, Stream data = null, object message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*
             * Sends a local image to a thread
             * : param image_path: Path of an image to upload and send
             * :param message: Additional message
             * :param thread_id: User / Group ID to send to. See: ref:`intro_threads`
             * :param thread_type: See: ref:`intro_threads`
             * :type thread_type: models.ThreadType
             * :return: :ref:`Message ID<intro_message_ids>` of the sent image
             * :raises: FBchatException if request failed
             */

            var thread = this._getThread(thread_id, thread_type);
            // var mimetype = guess_type(image_path)[0];
            var mimetype = "image/png";
            var is_gif = (mimetype == "image/gif");
            var image_id = await this._uploadImage(image_path, data, mimetype);
            return await this.sendImage(image_id: image_id, message: message, thread_id: thread_id, thread_type: thread_type, is_gif: is_gif);
        }

        /* MISSING METHODS! */

        /*
         * END SEND METHODS
         */

        /*
         * LISTEN METHODS
         */

        private async Task _ping(string sticky, string pool)
        {
            var data = new Dictionary<string, string>() {
                { "channel", this.user_channel },
                {"clientid", this.client_id },
                {"partition", (-2).ToString() },
                {"cap", 0.ToString() },
                {"uid", this.uid },
                {"sticky_token", sticky },
                {"sticky_pool", pool },
                {"viewer_uid", this.uid },
                {"state", "active" }
            };
            await this._get(ReqUrl.PING, data, fix_request: true, as_json: false);
        }

        private async Task<Tuple<string, string>> _fetchSticky()
        {
            /*Call pull api to get sticky and pool parameter, newer api needs these parameters to work*/
            var data = new Dictionary<string, string>() {
                { "msgs_recv", 0.ToString()},
                {"channel", this.user_channel},
                {"clientid", this.client_id }
            };

            var j = (JToken)(await this._get(ReqUrl.STICKY, data, fix_request: true, as_json: true));

            if (j["lb_info"] == null)
            {
                throw new FBchatException("Missing lb_info");
            }

            return Tuple.Create(j["lb_info"]["sticky"].Value<string>(), j["lb_info"]["pool"].Value<string>());
        }

        private async Task<JToken> _pullMessage(string sticky, string pool)
        {
            /*Call pull api with seq value to get message data.*/
            var data = new Dictionary<string, string>() {
                { "msgs_recv", 0.ToString() },
                {"sticky_token", sticky },
                {"sticky_pool", pool },
                {"clientid", this.client_id },
            };

            var j = (JToken)(await this._get(ReqUrl.STICKY, data, fix_request: true, as_json: true));

            this.seq = j["seq"] != null ? j["seq"].Value<string>() : "0";
            return j;
        }

        private Tuple<string, ThreadType> getThreadIdAndThreadType(JToken msg_metadata)
        {
            /*Returns a tuple consisting of thread ID and thread type*/
            string id_thread = null;
            ThreadType type_thread = ThreadType.USER;
            if (msg_metadata["threadKey"]["threadFbId"] != null)
            {
                id_thread = (msg_metadata["threadKey"]["threadFbId"].Value<string>());
                type_thread = ThreadType.GROUP;
            }
            else if (msg_metadata["threadKey"]["otherUserFbId"] != null)
            {
                id_thread = (msg_metadata["threadKey"]["otherUserFbId"].Value<string>());
                type_thread = ThreadType.USER;
            }
            return Tuple.Create(id_thread, type_thread);
        }

        private void _parseMessage(JToken content)
        {
            /*Get message and author name from content. May contain multiple messages in the content.*/

            if (content["ms"] == null) return;

            foreach (var m in content["ms"])
            {
                var mtype = m["type"].Value<string>();
                try
                {
                    // Things that directly change chat
                    if (mtype == "delta")
                    {
                        var delta = m["delta"];
                        var delta_type = m["type"].Value<string>();
                        var metadata = delta["messageMetadata"];

                        var mid = metadata?["messageId"].Value<string>();
                        var author_id = metadata?["actorFbId"].Value<string>();
                        var ts = metadata?["timestamp"].Value<string>();

                        // Added participants
                        if (delta["addedParticipants"] != null)
                        {
                            // added_ids = [str(x['userFbId']) for x in delta['addedParticipants']];
                            // thread_id = str(metadata['threadKey']['threadFbId']);
                            // this.onPeopleAdded(mid = mid, added_ids = added_ids, author_id = author_id, thread_id = thread_id, ts = ts, msg = m);
                        }

                        // Left/removed participants
                        else if (delta["leftParticipantFbId"] != null)
                        {
                            // removed_id = str(delta['leftParticipantFbId']);
                            // thread_id = str(metadata['threadKey']['threadFbId']);
                            // this.onPersonRemoved(mid = mid, removed_id = removed_id, author_id = author_id, thread_id = thread_id, ts = ts, msg = m);
                        }

                        // Color change
                        else if (delta_type == "change_thread_theme")
                        {
                            // new_color = graphql_color_to_enum(delta["untypedData"]["theme_color"]);
                            // thread_id, thread_type = getThreadIdAndThreadType(metadata);
                            // this.onColorChange(mid = mid, author_id = author_id, new_color = new_color, thread_id = thread_id, thread_type = thread_type, ts = ts, metadata = metadata, msg = m);
                        }

                        // Emoji change
                        else if (delta_type == "change_thread_icon")
                        {
                            // new_emoji = delta["untypedData"]["thread_icon"];
                            // thread_id, thread_type = getThreadIdAndThreadType(metadata);
                            // this.onEmojiChange(mid = mid, author_id = author_id, new_emoji = new_emoji, thread_id = thread_id, thread_type = thread_type, ts = ts, metadata = metadata, msg = m);
                        }

                        // Thread title change
                        else if (delta["class"].Value<string>() == "ThreadName")
                        {
                            // new_title = delta["name"];
                            // thread_id, thread_type = getThreadIdAndThreadType(metadata);
                            // this.onTitleChange(mid = mid, author_id = author_id, new_title = new_title, thread_id = thread_id, thread_type = thread_type, ts = ts, metadata = metadata, msg = m);
                        }

                        // Nickname change
                        else if (delta_type == "change_thread_nickname")
                        {
                            // changed_for = str(delta["untypedData"]["participant_id"]);
                            // new_nickname = delta["untypedData"]["nickname"];
                            // thread_id, thread_type = getThreadIdAndThreadType(metadata);
                            // this.onNicknameChange(mid = mid, author_id = author_id, changed_for = changed_for, new_nickname = new_nickname, thread_id = thread_id, thread_type = thread_type, ts = ts, metadata = metadata, msg = m);
                        }

                        // Message delivered
                        else if (delta["class"].Value<string>() == "DeliveryReceipt")
                        {
                            // message_ids = delta["messageIds"];
                            // delivered_for = str(delta.get("actorFbId") or delta["threadKey"]["otherUserFbId"]);
                            // ts = int(delta["deliveredWatermarkTimestampMs"]);
                            // thread_id, thread_type = getThreadIdAndThreadType(delta);
                            // this.onMessageDelivered(msg_ids = message_ids, delivered_for = delivered_for, thread_id = thread_id, thread_type = thread_type, ts = ts, metadata = metadata, msg = m);
                        }

                        // Message seen
                        else if (delta["class"].Value<string>() == "ReadReceipt")
                        {
                            // seen_by = str(delta.get("actorFbId") or delta["threadKey"]["otherUserFbId"]);
                            // seen_ts = int(delta["actionTimestampMs"]);
                            // delivered_ts = int(delta["watermarkTimestampMs"]);
                            // thread_id, thread_type = getThreadIdAndThreadType(delta);
                            // this.onMessageSeen(seen_by = seen_by, thread_id = thread_id, thread_type = thread_type, seen_ts = seen_ts, ts = delivered_ts, metadata = metadata, msg = m);
                        }

                        // Messages marked as seen
                        else if (delta["class"].Value<string>() == "MarkRead")
                        {
                            // seen_ts = int(delta.get("actionTimestampMs") or delta.get("actionTimestamp"));
                            // delivered_ts = int(delta.get("watermarkTimestampMs") or delta.get("watermarkTimestamp"));

                            // threads = [];
                            // if ("folders" not in delta)
                            // {
                            // threads = [getThreadIdAndThreadType({ "threadKey": thr}) for thr in delta.get("threadKeys")];
                            // }

                            // thread_id, thread_type = getThreadIdAndThreadType(delta)
                            // this.onMarkedSeen(threads = threads, seen_ts = seen_ts, ts = delivered_ts, metadata = delta, msg = m);
                        }

                        // New message
                        else if (delta["class"].Value<string>() == "NewMessage")
                        {
                            var mentions = new List<FB_Mention>();
                            if (delta["data"] != null && delta["data"]["prng"] != null)
                            {
                                try
                                {
                                    foreach (var mention in delta["data"]["prng"].Value<JArray>().Children())
                                    {
                                        mentions.Add(new FB_Mention(mention["i"].Value<string>(), offset: mention["o"].Value<int>(), length: mention["l"].Value<int>()));
                                    }
                                }
                                catch (Exception)
                                {
                                    Debug.WriteLine("An exception occured while reading attachments");
                                }

                                FB_Sticker sticker = null;
                                var attachments = new List<FB_Attachment>();

                                if (delta["attachments"] != null)
                                {
                                    try
                                    {
                                        foreach (var a in delta["attachments"])
                                        {
                                            var mercury = a["mercury"];
                                            if (mercury["blob_attachment"] != null)
                                            {
                                                var image_metadata = a["imageMetadata"];
                                                var attach_type = mercury["blob_attachment"]["__typename"];
                                                var attachment = GraphQL_JSON_Decoder.graphql_to_attachment(mercury["blob_attachment"]);

                                                if (new string[] { "MessageFile", "MessageVideo", "MessageAudio" }.Contains(attach_type.Value<string>()))
                                                {
                                                    // TODO: Add more data here for audio files
                                                    // attachment.size = a["fileSize"].Value<int>();
                                                    attachments.Add(attachment);
                                                }
                                                else if (mercury["sticker_attachment"] != null)
                                                {
                                                    sticker = GraphQL_JSON_Decoder.graphql_to_sticker(a["mercury"]["sticker_attachment"]);
                                                }
                                                else if (mercury["extensible_attachment"] != null)
                                                {
                                                    // TODO: Add more data here for shared stuff (URLs, events and so on)
                                                    continue;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        Debug.WriteLine(string.Format("An exception occured while reading attachments: {0}", delta["attachments"]));
                                    }

                                    EmojiSize? emoji_size = null;
                                    if (metadata != null && metadata["tags"] != null)
                                        emoji_size = Utils.get_emojisize_from_tags(metadata["tags"]);

                                    var message = new FB_Message(text: delta["body"]?.Value<string>(),
                                        mentions: mentions,
                                        emoji_size: emoji_size,
                                        sticker: sticker,
                                        attachments: attachments);

                                    message.uid = mid;
                                    message.author = author_id;
                                    message.timestamp = ts;
                                    // message.reactions = {};

                                    var id_type = getThreadIdAndThreadType(metadata);
                                    this.onMessage(mid: mid, author_id: author_id, message: delta["body"]?.Value<string>(), message_object: message,
                                        thread_id: id_type.Item1, thread_type: id_type.Item2, ts: ts, metadata: metadata, msg: m);
                                }
                                // Unknown message type
                                else
                                {
                                    this.onUnknownMesssageType(msg: m);
                                }
                            }
                        }
                    }

                    // Inbox
                    else if (mtype == "inbox")
                    {
                        this.onInbox(unseen: m["unseen"].Value<int>(), unread: m["unread"].Value<int>(), recent_unread: m["recent_unread"].Value<int>(), msg: m);
                    }

                    // Typing
                    // elif mtype == "typ":
                    //     author_id = str(m.get("from"))
                    //     typing_status = TypingStatus(m.get("st"))
                    //     this.onTyping(author_id=author_id, typing_status=typing_status)

                    // Delivered

                    // Seen
                    // elif mtype == "m_read_receipt":

                    //     this.onSeen(m["realtime_viewer_fbid'), m["reader'), m["time'))

                    // elif mtype in ['jewel_requests_add']:
                    //         from_id = m['from']
                    //         this.on_friend_request(from_id)

                    // Happens on every login
                    else if (mtype == "qprimer")
                    {
                        // this.onQprimer(ts: m.get("made"), msg: m);
                    }

                    // Is sent before any other message
                    else if (mtype == "deltaflow")
                    {

                    }

                    // Chat timestamp
                    else if (mtype == "chatproxy-presence")
                    {
                        var buddylist = new Dictionary<string, string>();
                        if (m["buddyList"] != null)
                        {
                            foreach (var payload in m["buddyList"].Value<JObject>().Properties())
                            {
                                buddylist[payload.Name] = payload.Value["lat"].Value<string>();
                            }
                            this.onChatTimestamp(buddylist: buddylist, msg: m);
                        }
                    }
                    // Unknown message type
                    else
                    {
                        this.onUnknownMesssageType(msg: m);
                    }
                }
                catch (Exception e)
                {
                    this.onMessageError(exception: e, msg: m);
                }
            }
        }

        /// <summary>
        /// Start listening from an external event loop
        /// </summary>
        public async Task startListening()
        {
            /*
             * Start listening from an external event loop
             * :raises: Exception if request failed
             */
            this.listening = true;
            var sticky_pool = await this._fetchSticky();
            this.sticky = sticky_pool.Item1;
            this.pool = sticky_pool.Item2;
        }

        /// <summary>
        /// Does one cycle of the listening loop.
        /// This method is useful if you want to control fbchat from an external event loop
        /// </summary>
        /// <param name="markAlive">Whether this should ping the Facebook server before running</param>
        /// <returns>Whether the loop should keep running</returns>
        public async Task<bool> doOneListen(bool markAlive = true)
        {
            /*
             * Does one cycle of the listening loop.
             * This method is useful if you want to control fbchat from an external event loop
             * :param markAlive: Whether this should ping the Facebook server before running
             * :type markAlive: bool
             * :return: Whether the loop should keep running
             * :rtype: bool
             */

            try
            {
                if (markAlive) await this._ping(this.sticky, this.pool);
                try
                {
                    var content = await this._pullMessage(this.sticky, this.pool);
                    if (content != null) this._parseMessage(content);
                }
                // catch (requests.exceptions.RequestException)
                // {
                // pass;
                // }
                catch (Exception e)
                {
                    this.onListenError(exception: e);
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cleans up the variables from startListening
        /// </summary>
        public void stopListening()
        {
            /*Cleans up the variables from startListening*/
            this.listening = false;
            this.sticky = null;
            this.pool = null;
        }

        /*
        END LISTEN METHODS
        */

        /*
         * EVENTS
         */

        /// <summary>
        /// Subscribe to this event to get chat updates (e.g. a new message)
        /// </summary>
        public event EventHandler<UpdateEventArgs> UpdateEvent;

        /// <summary>
        /// Calls UpdateEvent event handler
        /// </summary>
        protected void OnUpdateEvent(UpdateEventArgs e)
        {
            UpdateEvent?.Invoke(this, e);
        }

        /// <summary>
        /// Called when the client is listening
        /// </summary>
        protected void onListening()
        {
            /*Called when the client is listening*/
            Debug.WriteLine("Listening...");
        }


        /// <summary>
        /// Called when an error was encountered while listening
        /// </summary>
        /// <param name="exception">The exception that was encountered</param>
        protected void onListenError(Exception exception = null)
        {
            /*
             * Called when an error was encountered while listening
             * :param exception: The exception that was encountered
             */
            Debug.WriteLine(string.Format("Got exception while listening: {0}", exception));
        }

        /// <summary>
        /// Called when the client receives chat online presence update
        /// </summary>
        /// <param name="buddylist">A list of dicts with friend id and last seen timestamp</param>
        /// <param name="msg">A full set of the data received</param>
        protected void onChatTimestamp(Dictionary<string, string> buddylist = null, JToken msg = null)
        {
            /*
             * Called when the client receives chat online presence update
             * :param buddylist: A list of dicts with friend id and last seen timestamp
             * :param msg: A full set of the data received
             */
            Debug.WriteLine(string.Format("Chat Timestamps received: {0}", buddylist));
        }

        private void onInbox(int unseen, int unread, int recent_unread, JToken msg)
        {
            Debug.WriteLine(string.Format("Inbox event: {0}, {1}, {2}", unseen, unread, recent_unread));
        }

        /// <summary>
        /// Called when the client is listening, and somebody sends a message
        /// </summary>
        /// <param name="mid">The message ID</param>
        /// <param name="author_id">The ID of the author</param>
        /// <param name="message">The message content</param>
        /// <param name="message_object">The message object</param>
        /// <param name="thread_id">Thread ID that the message was sent to</param>
        /// <param name="thread_type">Type of thread that the message was sent to</param>
        /// <param name="ts">The timestamp of the message</param>
        /// <param name="metadata">Extra metadata about the message</param>
        /// <param name="msg">A full set of the data received</param>
        protected void onMessage(string mid = null, string author_id = null, string message = null, FB_Message message_object = null, string thread_id = null, ThreadType thread_type = ThreadType.USER, string ts = null, JToken metadata = null, JToken msg = null)
        {
            /*
            Called when the client is listening, and somebody sends a message
            :param mid: The message ID
            :param author_id: The ID of the author
            :param message: (deprecated. Use `message_object.text` instead)
            :param message_object: The message (As a `Message` object)
            :param thread_id: Thread ID that the message was sent to.See :ref:`intro_threads`
            :param thread_type: Type of thread that the message was sent to.See :ref:`intro_threads`
            :param ts: The timestamp of the message
            :param metadata: Extra metadata about the message
            :param msg: A full set of the data received
            :type thread_type: models.ThreadType
            */
            UpdateEvent(this, new UpdateEventArgs(UpdateStatus.NEW_MESSAGE, message_object));
            Debug.WriteLine(string.Format("Message from {0} in {1} ({2}): {3}", author_id, thread_id, thread_type.ToString(), message));
        }

        /// <summary>
        /// Called when the client is listening, and some unknown data was received
        /// </summary>
        /// <param name="msg">A full set of the data received</param>
        protected void onUnknownMesssageType(JToken msg = null)
        {
            /*
             * Called when the client is listening, and some unknown data was received
             * :param msg: A full set of the data received
             */
            Debug.WriteLine(string.Format("Unknown message received: {0}", msg));
        }

        /// <summary>
        /// Called when an error was encountered while parsing received data
        /// </summary>
        /// <param name="exception">The exception that was encountered</param>
        /// <param name="msg">A full set of the data received</param>
        protected void onMessageError(Exception exception = null, JToken msg = null)
        {
            /*
             * Called when an error was encountered while parsing received data
             * :param exception: The exception that was encountered
             * :param msg: A full set of the data received
             */
            Debug.WriteLine(string.Format("Exception in parsing of {0}", msg));
        }

        /*
         * END EVENTS
         */

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns true is the request was successful</returns>
        public async Task<bool> markAsDelivered(string userID, string threadID)
        {
            /*
             * ..todo::
             * Documenting this
             */
            var data = new Dictionary<string, string>() {
                { "message_ids[0]", threadID },
                { string.Format("thread_ids[{0}][0]", userID), threadID}
            };

            var r = (HttpResponseMessage)(await this._post(ReqUrl.DELIVERED, data));
            return r.IsSuccessStatusCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns true is the request was successful</returns>
        public async Task<bool> markAsRead(string userID)
        {
            /*
             * ..todo::
             * Documenting this
             */
            var data = new Dictionary<string, string>() {
                { "watermarkTimestamp", Utils.now().ToString() },
                { "shouldSendReadReceipt", true.ToString()},
                { string.Format("ids[{0}]", userID), true.ToString()}
            };

            var r = (HttpResponseMessage)(await this._post(ReqUrl.READ_STATUS, data));
            return r.IsSuccessStatusCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns true is the request was successful</returns>
        public async Task<bool> markAsSeen()
        {
            /*
             * ..todo::
             * Documenting this
             */
            var data = new Dictionary<string, string>()
            {
                { "seen_timestamp", 0.ToString()}
            };

            var r = (HttpResponseMessage)(await this._post(ReqUrl.MARK_SEEN, data));
            return r.IsSuccessStatusCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns true is the request was successful</returns>
        public async Task<bool> friendConnect(string friend_id)
        {
            /*
             * ..todo::
             * Documenting this
             */
            var data = new Dictionary<string, string>()
            {
                { "to_friend", friend_id },
                {"action", "confirm" }
            };

            var r = (HttpResponseMessage)(await this._post(ReqUrl.CONNECT, data));
            return r.IsSuccessStatusCode;
        }
    }
}
