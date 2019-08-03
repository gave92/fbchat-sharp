﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace fbchat_sharp.API.Models
{
    /// <summary>
    /// 
    /// </summary>
    public class Client_Constants
    {
        public static readonly Dictionary<string, object> ACONTEXT = new Dictionary<string, object>()
        {
            { "action_history", new List<Dictionary<string,object>>() {
                new Dictionary<string,object>() { { "surface", "messenger_chat_tab" }, {"mechanism", "messenger_composer"} } }
            }
        };
    }

    /// <summary>
    /// A client for the Facebook Chat (Messenger).
    /// This is the main class of `fbchat-sharp`, which contains all the methods you use to
    /// interact with Facebook.You can extend this class, and overwrite the `on` methods,
    /// to provide custom event handling (mainly useful while listening).
    /// </summary>
    public class Client
    {
        /// Whether the client is listening. Used when creating an external event loop to determine when to stop listening.
        private bool listening { get; set; }
        /// Stores and manages state required for most Facebook requests.
        private State _state { get; set; }

        /// <summary>
        /// The ID of the client.
        /// Can be used as `thread_id`.
        /// Note: Modifying this results in undefined behaviour
        /// </summary>
        protected string uid { get; set; }

        private Client()
        {
            this.sticky = null;
            this.pool = null;
            this.seq = "0";
            this._client_id = ((int)(new Random().NextDouble() * (2 ^ 31))).ToString("X4").Substring(2);
            this.default_thread_id = null;
            this.default_thread_type = null;
            this._pull_channel = 0;
            this._markAlive = true;
            this._buddylist = new Dictionary<string, string>();
        }

        /// <summary>
        /// Initialize and log in the client.
        /// </summary>
        /// <param name="email">Facebook `email`, `id` or `phone number`</param>
        /// <param name="password">Facebook account password</param>
        /// <param name="user_agent">Optional custom user agent string</param>
        /// <param name="max_tries">Maximum number of times to try logging in</param>
        /// <param name="session_cookies">Optional Cookies from a previous session</param>
        public static async Task<Client> Create(string email, string password, string user_agent = null, int max_tries = 5, Dictionary<object, CookieCollection> session_cookies = null)
        {
            Client client = new Client();

            // If session cookies aren't set, not properly loaded or gives us an invalid session, then do the login
            if (
                session_cookies == null ||
                !await client.setSession(session_cookies, user_agent: user_agent) ||
                !await client.isLoggedIn()
                )
            {
                await client.login(email, password, max_tries, user_agent: user_agent);
            }

            return client;
        }

        #region INTERNAL REQUEST METHODS

        private Dictionary<string, object> _generatePayload(Dictionary<string, object> query = null)
        {
            var payload = this._state.get_params();
            if (query != null)
            {
                foreach (var entry in query)
                {
                    payload[entry.Key] = entry.Value;
                }
            }
            return payload;
        }

        private async Task _do_refresh()
        {
            // TODO: Raise the error instead, and make the user do the refresh manually
            // It may be a bad idea to do this in an exception handler, if you have a better method, please suggest it!
            Debug.WriteLine("Refreshing state and resending request");
            this._state = await State.from_session(session: this._state._session);
        }

        private async Task<object> _get(string url, Dictionary<string, object> query = null, int error_retries = 3)
        {
            var payload = this._generatePayload(query);
            var r = await this._state._session.get(prefix_url(url), param: payload);
            var content = await Utils.check_request(r);
            var j = Utils.to_json(content);
            try
            {
                Utils.handle_payload_error(j);
            }
            catch (FBchatPleaseRefresh ex)
            {
                if (error_retries > 0)
                {
                    await this._do_refresh();
                    return await this._get(url, query: query, error_retries: error_retries - 1);
                }
                throw ex;
            }
            return j;
        }

        private async Task<object> _post(string url, Dictionary<string, object> query = null, Dictionary<string, FB_File> files = null, bool as_graphql = false, int error_retries = 3)
        {
            var payload = this._generatePayload(query);
            var r = await this._state._session.post(prefix_url(url), data: payload, files: files);
            var content = await Utils.check_request(r);
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
                if (error_retries > 0)
                {
                    await this._do_refresh();
                    return await this._post(
                        url,
                        query: query,
                        files: files,
                        as_graphql: as_graphql,
                        error_retries: error_retries - 1);
                }
                throw ex;
            }
        }

        private async Task<JToken> _payload_post(string url, Dictionary<string, object> data = null, Dictionary<string, FB_File> files = null)
        {
            var j = await this._post(url, data, files: files);
            try
            {
                return ((JToken)j)["payload"];
            }
            catch (Exception)
            {
                throw new FBchatException(string.Format("Missing payload: {0}", j));
            }
        }

        private async Task<List<JToken>> graphql_requests(List<GraphQL> queries)
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

        private async Task<JToken> graphql_request(GraphQL query)
        {
            /*
             * Shorthand for `graphql_requests(query)[0]`
             * :raises: Exception if request failed
             */
            return (await this.graphql_requests(new[] { query }.ToList()))[0];
        }

        #endregion

        #region LOGIN METHODS

        /// <summary>
        /// Sends a request to Facebook to check the login status
        /// </summary>
        /// <returns>true if the client is still logged in</returns>
        public async Task<bool> isLoggedIn()
        {
            return await this._state.is_logged_in();
        }

        /// <summary>
        /// Retrieves session cookies
        /// </summary>
        /// <returns>A dictionay containing session cookies</returns>
        public Dictionary<object, CookieCollection> getSession()
        {
            /*
             * Retrieves session cookies
             * :return: A list containing session cookies
             * : rtype: IEnumerable
             */
            return this._session.GetAllCookies();
        }

        /// <summary>
        /// Loads session cookies
        /// </summary>
        /// <param name="session_cookies"></param>
        /// <param name="user_agent"></param>
        /// <returns>false if ``session_cookies`` does not contain proper cookies</returns>
        public async Task<bool> setSession(Dictionary<object, CookieCollection> session_cookies, string user_agent = null)
        {
            State state = null;
            try
            {
                // Load cookies into current session
                state = await State.from_cookies(session_cookies, user_agent: user_agent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed loading session");
                return false;
            }
            var uid = state.get_user_id();
            if (uid == null)
            {
                Debug.WriteLine("Could not find c_user cookie");
                return false;
            }
            this._state = state;
            this._uid = uid;
            return true;
        }

        /// <summary>
        /// Uses ``email`` and ``password`` to login the user (If the user is already logged in, this will do a re-login)
        /// </summary>
        /// <param name="email">Facebook ``email`` or ``id`` or ``phone number``</param>
        /// <param name="password">Facebook account password</param>
        /// <param name="max_tries">Maximum number of times to try logging in</param>
        /// <param name="user_agent"></param>
        public async Task login(string email, string password, int max_tries = 5, string user_agent = null)
        {
            this.onLoggingIn(email: email);

            if (max_tries < 1)
                throw new FBchatUserError("Cannot login: max_tries should be at least one");
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                throw new FBchatUserError("Email and password not set");

            foreach (int i in Enumerable.Range(1, max_tries))
            {
                try
                {
                    var state = State.login(
                        email,
                        password,
                        on_2fa_callback: this.on2FACode,
                        user_agent: user_agent);
                    var uid = state.get_user_id();
                    if (uid == null)
                        throw new FBchatException("Could not find user id");

                    this._state = state;
                    this._uid = uid;
                    this.onLoggedIn(email: email);
                    break;
                }
                catch (Exception ex)
                {
                    if (i >= max_tries)
                        throw ex;
                    Debug.WriteLine(string.Format("Attempt #{0} failed, retrying", i));
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }

        /// <summary>
        /// Safely logs out the client
        /// </summary>
        /// <returns>true if the action was successful</returns>
        public async Task<bool> logout()
        {
            if (await this._state.logout())
            {
                this._state = null;
                this._uid = null;
                return true;
            }
            return false;
        }
        #endregion

        #region DEFAULT THREAD METHODS

        /// <summary>
        /// Checks if thread ID is given, checks if default is set and returns correct values
        /// </summary>
        /// <param name="given_thread_id"></param>
        /// <param name="given_thread_type"></param>
        /// <returns>Thread ID and thread type</returns>
        private Tuple<string, ThreadType> _getThread(string given_thread_id = null, ThreadType? given_thread_type = ThreadType.USER)
        {
            if (given_thread_id == null)
            {
                if (this._default_thread_id != null)
                {
                    return Tuple.Create(this._default_thread_id, this._default_thread_type);
                }
                else
                {
                    throw new ArgumentException("Thread ID is not set");
                }
            }
            else
            {
                return Tuple.Create(given_thread_id, given_thread_type ?? ThreadType.USER);
            }
        }

        /// <summary>
        /// Sets default thread to send messages to
        /// </summary>
        /// <param name="thread_id">User / FGroup ID to default to.See :ref:`intro_threads`</param>
        /// <param name="thread_type"></param>
        private void setDefaultThread(string thread_id, ThreadType? thread_type)
        {
            this._default_thread_id = thread_id;
            this._default_thread_type = thread_type;
        }

        /// <summary>
        /// Resets default thread
        /// </summary>
        private void resetDefaultThread()
        {
            this.setDefaultThread(null, null);
        }

        #endregion

        #region FETCH METHODS
        private async Task<JToken> _forcedFetch(string thread_id, string mid)
        {
            var param = new Dictionary<string, object>() { { "thread_and_message_id", new Dictionary<string, object>() { { "thread_id", thread_id }, { "message_id", mid } } } };
            return await this.graphql_request(GraphQL.from_doc_id("1768656253222505", param));
        }

        /// <summary>
        /// Get all threads in thread_location.
        /// Threads will be sorted from newest to oldest.
        /// </summary>
        /// <param name="thread_location">ThreadLocation: INBOX, PENDING, ARCHIVED or OTHER</param>
        /// <param name="before">Fetch only thread before this epoch (in ms) (default all threads)</param>
        /// <param name="after">Fetch only thread after this epoch (in ms) (default all threads)</param>
        /// <param name="limit">The max. amount of threads to fetch (default all threads)</param>
        /// <returns></returns>
        public async Task<List<FB_Thread>> fetchThreads(string thread_location, int? before = null, int? after = null, int? limit = null)
        {
            /*
             * Get all threads in thread_location.
             * Threads will be sorted from newest to oldest.
             * :param thread_location: ThreadLocation: INBOX, PENDING, ARCHIVED or OTHER
             * : param before: Fetch only thread before this epoch(in ms)(default all threads)
             * :param after: Fetch only thread after this epoch(in ms)(default all threads)
             * :param limit: The max. amount of threads to fetch(default all threads)
             * :return: :class:`Thread` objects
             * :rtype: list:raises: FBchatException if request failed
             * */
            List<FB_Thread> threads = new List<FB_Thread>();
            string last_thread_timestamp = null;
            while (true)
            {
                // break if limit is exceeded
                if (limit != null && threads.Count >= limit)
                    break;
                // fetchThreadList returns at max 20 threads before last_thread_timestamp (included)
                var candidates = await this.fetchThreadList(
                    before: last_thread_timestamp, thread_location: thread_location
                );

                if (candidates.Count > 1)
                    threads.AddRange(candidates.Skip(1).ToList());
                else  // End of threads
                    break;

                last_thread_timestamp = threads.LastOrDefault()?.last_message_timestamp;

                // FB returns a sorted list of threads
                if ((before != null && int.Parse(last_thread_timestamp) > before) ||
                    (after != null && int.Parse(last_thread_timestamp) < after))
                    break;
            }

            // Return only threads between before and after (if set)
            if (after != null || before != null)
            {
                foreach (var t in threads)
                {
                    var last_message_timestamp = int.Parse(t.last_message_timestamp);
                    if ((before != null && last_message_timestamp > before) ||
                        (after != null && last_message_timestamp < after))
                        threads.Remove(t);
                }
            }

            if (limit != null && threads.Count > limit)
                return threads.Take((int)limit).ToList();

            return threads;
        }

        /// <summary>
        /// Get all users involved in threads.
        /// </summary>
        /// <param name="threads">threads: Thread: List of threads to check for users</param>
        /// <returns>:class:`FB_User` objects</returns>
        public async Task<List<FB_User>> fetchAllUsersFromThreads(List<FB_Thread> threads)
        {
            /*
             * Get all users involved in threads.
             * :param threads: Thread: List of threads to check for users
             * :return: :class:`User` objects
             * :rtype: list
             * :raises: FBchatException if request failed
             * */
            List<FB_User> users = new List<FB_User>();
            List<string> users_to_fetch = new List<string>();  // It's more efficient to fetch all users in one request

            foreach (var thread in threads)
            {
                if (thread.type == ThreadType.USER)
                {
                    if (!users.Select((u) => u.uid).Contains(thread.uid))
                        users.Add((FB_User)thread);
                }
                else if (thread.type == ThreadType.GROUP)
                {
                    foreach (var user_id in ((FB_Group)thread).participants)
                    {
                        if (!users.Select((u) => u.uid).Contains(user_id) &&
                            !users_to_fetch.Contains(user_id))
                            users_to_fetch.Add(user_id);
                    }
                }
            }

            foreach (KeyValuePair<string, FB_User> entry in await this.fetchUserInfo(users_to_fetch))
                users.Add(entry.Value);
            return users;
        }

        /// <summary>
        /// Gets all users the client is currently chatting with
        /// </summary>
        /// <returns>:class:`FB_User` objects</returns>
        public async Task<List<FB_User>> fetchAllUsers()
        {
            /*
             * Gets all users the client is currently chatting with
             * : return: :class:`User` objects
             * :rtype: list
             * :raises: FBchatException if request failed
             * */

            var data = new Dictionary<string, object>() {
                { "viewer", this.uid },
            };
            var j = await this._payload_post("/chat/user_info_all", data: data);

            var users = new List<FB_User>();
            foreach (var u in j.Value<JObject>().Properties())
            {
                var k = u.Value;
                if (k != null && new[] { "user", "friend" }.Contains(k["type"].Value<string>()))
                {
                    if (new[] { "0", "\0" }.Contains(k["id"].Value<string>()))
                    {
                        // Skip invalid users
                        continue;
                    }
                    users.Add(FB_User._from_all_fetch(k));
                }
            }

            return users;
        }

        /// <summary>
        /// Find and get user by his/her name
        /// </summary>
        /// <param name="name">Name of the user</param>
        /// <param name="limit">The max. amount of users to fetch</param>
        /// <returns>:class:`FB_User` objects, ordered by relevance</returns>
        public async Task<List<FB_User>> searchForUsers(string name, int limit = 10)
        {
            /*
             * Find and get user by his/ her name
             * : param name: Name of the user
             * :param limit: The max. amount of users to fetch
             * : return: :class:`User` objects, ordered by relevance
             * :rtype: list
             * :raises: FBchatException if request failed
             * */

            var param = new Dictionary<string, object>() {
                { "search", name }, { "limit", limit.ToString() }
             };
            var j = await this.graphql_request(GraphQL.from_query(GraphQL.SEARCH_USER, param));

            return j[name]["users"]["nodes"].Select(node => FB_User._from_graphql(node)).ToList();
        }

        /// <summary>
        /// Find and get page by its name
        /// </summary>
        /// <param name="name">Name of the page</param>
        /// <param name="limit">The max. amount of pages to fetch</param>
        /// <returns>:class:`FB_Page` objects, ordered by relevance</returns>
        public async Task<List<FB_Page>> searchForPages(string name, int limit = 1)
        {
            /*
             * Find and get page by its name
             * : param name: Name of the page
             * :return: :class:`Page` objects, ordered by relevance
             * :rtype: list
             * :raises: FBchatException if request failed
             * */

            var param = new Dictionary<string, object>() {
                { "search", name }, { "limit", limit.ToString() }
            };
            var j = await this.graphql_request(GraphQL.from_query(GraphQL.SEARCH_PAGE, param));

            return j[name]["pages"]["nodes"].Select(node => FB_Page._from_graphql(node)).ToList();
        }

        /// <summary>
        /// Find and get group thread by its name
        /// </summary>
        /// <param name="name">Name of the group</param>
        /// <param name="limit">The max. amount of groups to fetch</param>
        /// <returns>:class:`FB_Group` objects, ordered by relevance</returns>
        public async Task<List<FB_Group>> searchForGroups(string name, int limit = 1)
        {
            /*
             * Find and get group thread by its name
             * :param name: Name of the group thread
             * :param limit: The max. amount of groups to fetch
             * :return: :class:`Group` objects, ordered by relevance
             * :rtype: list
             * :raises: FBchatException if request failed
             * */

            var param = new Dictionary<string, object>() {
              { "search", name }, {"limit", limit.ToString() }
            };
            var j = await this.graphql_request(GraphQL.from_query(GraphQL.SEARCH_GROUP, param));

            return j["viewer"]["groups"]["nodes"].Select(node => FB_Group._from_graphql(node)).ToList();
        }

        /// <summary>
        /// Find and get a thread by its name
        /// </summary>
        /// <param name="name">Name of the thread</param>
        /// <param name="limit">The max. amount of threads to fetch</param>
        /// <returns>:class:`FB_User`, :class:`FB_Group` and :class:`FB_Page` objects, ordered by relevance</returns>
        public async Task<List<FB_Thread>> searchForThreads(string name, int limit = 1)
        {
            /*
             * Find and get a thread by its name
             * :param name: Name of the thread
             * :param limit: The max. amount of groups to fetch
             * : return: :class:`User`, :class:`Group` and :class:`Page` objects, ordered by relevance
             * :rtype: list
             * :raises: FBchatException if request failed
             * */

            var param = new Dictionary<string, object>(){
                { "search", name }, {"limit", limit.ToString() }
            };
            var j = await this.graphql_request(GraphQL.from_query(GraphQL.SEARCH_THREAD, param));

            List<FB_Thread> rtn = new List<FB_Thread>();
            foreach (var node in j[name]["threads"]["nodes"])
            {
                if (node["__typename"].Value<string>().Equals("User"))
                {
                    rtn.Add(FB_User._from_graphql(node));
                }
                else if (node["__typename"].Value<string>().Equals("MessageThread"))
                {
                    // MessageThread => Group thread
                    rtn.Add(FB_Group._from_graphql(node));
                }
                else if (node["__typename"].Value<string>().Equals("Page"))
                {
                    rtn.Add(FB_Page._from_graphql(node));
                }
                else if (node["__typename"].Value<string>().Equals("Group"))
                {
                    // We don"t handle Facebook "Groups"
                    continue;
                }
                else
                {
                    Debug.WriteLine(string.Format("Unknown __typename: {0} in {1}", node["__typename"].Value<string>(), node));
                }
            }

            return rtn;
        }

        /// <summary>
        /// Find and get message IDs by query
        /// </summary>
        /// <param name="query">Text to search for</param>
        /// <param name="offset">Number of messages to skip</param>
        /// <param name="limit">Max. number of messages to retrieve</param>
        /// <param name="thread_id">User/Group ID to search in. See :ref:`intro_threads`</param>
        /// <returns>Found Message IDs</returns>
        public IAsyncEnumerable<string> searchForMessageIDs(string query, int offset = 0, int limit = 5, string thread_id = null)
        {
            return new AsyncEnumerable<string>(async yield =>
            {
                var thread = this._getThread(thread_id, null);
                thread_id = thread.Item1;

                var data = new Dictionary<string, object>() {
                    { "query", query },
                    { "snippetOffset", offset.ToString() },
                    { "snippetLimit", limit.ToString() },
                    { "identifier", "thread_fbid"},
                    { "thread_fbid", thread_id} };
                var j = await this._payload_post("/ajax/mercury/search_snippets.php?dpr=1", data);

                var result = j["search_snippets"][query];
                foreach (var snippet in result[thread_id]?["snippets"])
                    await yield.ReturnAsync(snippet["message_id"]?.Value<string>());
            });
        }

        /// <summary>
        /// Find and get:class:`FB_Message` objects by query
        /// </summary>
        /// <param name="query">Text to search for</param>
        /// <param name="offset">Number of messages to skip</param>
        /// <param name="limit">Max.number of messages to retrieve</param>
        /// <param name="thread_id">User/Group ID to search in. See :ref:`intro_threads`</param>
        /// <returns>Found :class:`FB_Message` objects</returns>
        public IAsyncEnumerable<FB_Message> searchForMessages(string query, int offset = 0, int limit = 5, string thread_id = null)
        {
            /*
             * Find and get:class:`Message` objects by query
             * ..warning::
             * This method sends request for every found message ID.
             * :param query: Text to search for
             * :param offset: Number of messages to skip
             * :param limit: Max.number of messages to retrieve
             * :param thread_id: User/Group ID to search in. See :ref:`intro_threads`
             * :type offset: int
             * :type limit: int
             * :return: Found :class:`Message` objects
             * :rtype: typing.Iterable
             * :raises: FBchatException if request failed
             * */

            return new AsyncEnumerable<FB_Message>(async yield =>
            {
                var message_ids = this.searchForMessageIDs(
                    query, offset: offset, limit: limit, thread_id: thread_id
                );
                await message_ids.ForEachAsync(async (mid) =>
                    await yield.ReturnAsync(await this.fetchMessageInfo(mid, thread_id)));
            });
        }

        /// <summary>
        /// Searches for messages in all threads
        /// </summary>
        /// <param name="query">Text to search for</param>
        /// <param name="fetch_messages">Whether to fetch :class:`Message` objects or IDs only</param>
        /// <param name="thread_limit">Max. number of threads to retrieve</param>
        /// <param name="message_limit">Max. number of messages to retrieve</param>
        /// <returns>Dictionary with thread IDs as keys and iterables to get messages as values</returns>
        public async Task<Dictionary<string, object>> search(string query, bool fetch_messages = false, int thread_limit = 5, int message_limit = 5)
        {
            var data = new Dictionary<string, object>() {
                { "query", query },
                { "snippetLimit", thread_limit.ToString() }
            };
            var j = await this._payload_post("/ajax/mercury/search_snippets.php?dpr=1", data);
            var result = j["search_snippets"][query].Value<List<string>>();

            if (result == null)
                return null;

            if (fetch_messages)
            {
                var rtn = new Dictionary<string, object>();
                foreach (var thread_id in result)
                    rtn.Add(thread_id, this.searchForMessages(query, limit: message_limit, thread_id: thread_id));
                return rtn;
            }
            else
            {
                var rtn = new Dictionary<string, object>();
                foreach (var thread_id in result)
                    rtn.Add(thread_id, this.searchForMessageIDs(query, limit: message_limit, thread_id: thread_id));
                return rtn;
            }
        }

        private async Task<JObject> _fetchInfo(List<string> ids)
        {
            var data = new Dictionary<string, object>();
            foreach (var obj in ids.Select((x, index) => new { _id = x, i = index }))
                data.Add(string.Format("ids[{0}]", obj.i), obj._id);

            var j = await this._payload_post("/chat/user_info/", data);

            if (j["profiles"] == null)
                throw new FBchatException("No users/pages returned");

            var entries = new JObject();
            foreach (var k in j["profiles"]?.Value<JObject>()?.Properties())
            {
                if (new[] { "user", "friend" }.Contains(k.Value["type"]?.Value<string>()))
                {
                    entries[k.Name] = new JObject() {
                        { "id", k.Name },
                        {"type", (int)ThreadType.USER },
                        {"url", k.Value["uri"]?.Value<string>() },
                        {"first_name", k.Value["firstName"]?.Value<string>() },
                        {"is_viewer_friend", k.Value["is_friend"]?.Value<bool>() ?? false },
                        {"gender", k.Value["gender"]?.Value<string>() },
                        {"profile_picture", new JObject() { { "uri", k.Value["thumbSrc"]?.Value<string>() } } },
                        { "name", k.Value["name"]?.Value<string>() }
                    };
                }
                else if (k.Value["type"].Value<string>().Equals("page"))
                {
                    entries[k.Name] = new JObject() {
                        { "id", k.Name},
                        { "type", (int)ThreadType.PAGE},
                        { "url", k.Value["uri"]?.Value<string>() },
                        { "profile_picture", new JObject() { { "uri", k.Value["thumbSrc"]?.Value<string>() } } },
                        { "name", k.Value["name"]?.Value<string>() }
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
        public async Task<Dictionary<string, FB_User>> fetchUserInfo(List<string> user_ids)
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
        public async Task<Dictionary<string, FB_Page>> fetchPageInfo(List<string> page_ids)
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
        public async Task<Dictionary<string, FB_Group>> fetchGroupInfo(List<string> group_ids)
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
                queries.Add(GraphQL.from_doc_id(doc_id: "1386147188135407", param: new Dictionary<string, object>() {
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
                    // If you don't have an existing thread with this person, attempt to retrieve user data anyways
                    j[obj.i]["message_thread"] = new JObject(
                                                    new JProperty("thread_key",
                                                        new JObject(
                                                            new JProperty("other_user_id", thread_ids[obj.i]))),
                                                    new JProperty("thread_type", "ONE_TO_ONE"));
                }
            }

            var pages_and_user_ids = j.Where(k => k["message_thread"]?["thread_type"]?.Value<string>()?.Equals("ONE_TO_ONE") ?? false)
                .Select(k => k["message_thread"]?["thread_key"]?["other_user_id"]?.Value<string>());
            JObject pages_and_users = null;
            if (pages_and_user_ids.Count() != 0)
            {
                pages_and_users = await this._fetchInfo(pages_and_user_ids.ToList());
            }

            var rtn = new Dictionary<string, FB_Thread>();
            foreach (var obj in j.Select((x, index) => new { entry = x, i = index }))
            {
                var entry = obj.entry["message_thread"];
                if (entry["thread_type"]?.Value<string>()?.Equals("GROUP") ?? false)
                {
                    var _id = entry["thread_key"]["thread_fbid"].Value<string>();
                    rtn[_id] = FB_Group._from_graphql(entry);
                }
                else if (entry["thread_type"]?.Value<string>()?.Equals("ONE_TO_ONE") ?? false)
                {
                    var _id = entry["thread_key"]?["other_user_id"]?.Value<string>();
                    if (pages_and_users[_id] == null)
                    {
                        throw new FBchatException(string.Format("Could not fetch thread {0}", _id));
                    }
                    foreach (var elem in pages_and_users[_id])
                    {
                        entry[((JProperty)elem).Name] = ((JProperty)elem).Value;
                    }
                    if (entry["type"]?.Value<int>() == (int)ThreadType.USER)
                    {
                        rtn[_id] = FB_User._from_graphql(entry);
                    }
                    else
                    {
                        rtn[_id] = FB_Page._from_graphql(entry);
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

            var j = await this.graphql_request(GraphQL.from_doc_id(doc_id: "1386147188135407", param: dict));

            if (j["message_thread"] == null || j["message_thread"].Type == JTokenType.Null)
            {
                throw new FBchatException(string.Format("Could not fetch thread {0}", thread_id));
            }

            var messages = j?["message_thread"]?["messages"]?["nodes"]?.Select(message => FB_Message._from_graphql(message))?.Reverse()?.ToList();

            var read_receipts = j?["message_thread"]?["read_receipts"]?["nodes"];
            foreach (var message in messages)
            {
                foreach (var receipt in read_receipts)
                {
                    if (int.Parse(receipt["watermark"]?.Value<string>()) >= int.Parse(message.timestamp))
                        message.read_by.Add(receipt["actor"]["id"]?.Value<string>());
                }
            }

            return messages;
        }

        /// <summary>
        /// Get thread list of your facebook account
        /// </summary>
        /// <param name="limit">Max.number of threads to retrieve. Capped at 20</param>
        /// <param name="thread_location">models.ThreadLocation: INBOX, PENDING, ARCHIVED or OTHER</param>
        /// <param name="before">A unix timestamp, indicating from which point to retrieve messages</param>
        public async Task<List<FB_Thread>> fetchThreadList(int limit = 20, string thread_location = ThreadLocation.INBOX, string before = null)
        {
            /*
             * Get thread list of your facebook account             
             * :param limit: Max.number of threads to retrieve.Capped at 20
             * :param thread_location: models.ThreadLocation: INBOX, PENDING, ARCHIVED or OTHER
             * :param before: A timestamp (in milliseconds), indicating from which point to retrieve threads
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

            var j = await this.graphql_request(GraphQL.from_doc_id(doc_id: "1349387578499440", param: dict));

            var rtn = new List<FB_Thread>();
            foreach (var node in j["viewer"]?["message_threads"]?["nodes"])
            {
                var _type = node["thread_type"]?.Value<string>();
                if (_type == "GROUP")
                    rtn.Add(FB_Group._from_graphql(node));
                else if (_type == "ONE_TO_ONE")
                    rtn.Add(FB_User._from_thread_fetch(node));
                else
                    throw new FBchatException(string.Format("Unknown thread type: {0}", _type));
            }
            return rtn;
        }

        /// <summary>
        /// Get unread user threads
        /// </summary>
        /// <returns>Returns unread thread ids</returns>
        public async Task<List<string>> fetchUnread()
        {
            /*
             * Get the unread thread list
             * :return: List of unread thread ids
             * :rtype: list
             * :raises: FBchatException if request failed
             */

            var form = new Dictionary<string, object>() {
                { "folders[0]", "inbox"},
                { "client", "mercury"},
                { "last_action_timestamp", (Utils.now() - 60 * 1000).ToString()},
                //{ "last_action_timestamp", 0.ToString()}
            };

            var j = await this._payload_post("/ajax/mercury/unread_threads.php", form);

            var result = j["unread_thread_fbids"]?[0];
            var rtn = new List<string>();
            rtn.AddRange(result?["thread_fbids"]?.Value<List<string>>());
            rtn.AddRange(result?["other_user_fbids"]?.Value<List<string>>());
            return rtn;
        }

        /// <summary>
        /// Get unseen user threads
        /// </summary>
        /// <returns>Returns unseen message ids</returns>
        public async Task<List<string>> fetchUnseen()
        {
            /*
             * Get the unseeen thread list
             * :return: List of unseeen thread ids
             * :rtype: list
             * :raises: FBchatException if request failed
             */

            var j = await this._payload_post("/mercury/unseen_thread_ids/", null);

            var result = j["unseen_thread_fbids"]?[0];
            var rtn = new List<string>();
            rtn.AddRange(result?["thread_fbids"]?.Value<List<string>>());
            rtn.AddRange(result?["other_user_fbids"]?.Value<List<string>>());
            return rtn;
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

            var data = new Dictionary<string, object>() {
                { "photo_id", image_id},
            };
            var j = (JToken)await this._post("/mercury/attachments/photo/", data);

            var url = Utils.get_jsmods_require(j, 3);
            if (url == null)
                throw new FBchatException(string.Format("Could not fetch image url from: {0}", j));
            return url.Value<string>();
        }

        /// <summary>
        /// Fetches:class:`Message` object from the message id
        /// </summary>
        /// <param name="mid">Message ID to fetch from</param>
        /// <param name="thread_id">User/Group ID to get message info from.See :ref:`intro_threads`</param>
        /// <returns>:class:`FB_Message` object</returns>
        public async Task<FB_Message> fetchMessageInfo(string mid, string thread_id = null)
        {
            /*
             * Fetches:class:`Message` object from the message id
             * :param mid: Message ID to fetch from
             * :param thread_id: User/Group ID to get message info from.See :ref:`intro_threads`
             * :return: :class:`Message` object
             * :rtype: Message
             * :raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);
            thread_id = thread.Item1;
            var message_info = ((JToken)await this._forcedFetch(thread_id, mid))?["message"];
            return FB_Message._from_graphql(message_info);
        }

        /// <summary>
        /// Fetches list of:class:`PollOption` objects from the poll id
        /// </summary>
        /// <param name="poll_id">Poll ID to fetch from</param>
        /// <returns></returns>
        public async Task<List<FB_PollOption>> fetchPollOptions(string poll_id)
        {
            /*
             * Fetches list of:class:`PollOption` objects from the poll id
             * :param poll_id: Poll ID to fetch from
             * :rtype: list
             * :raises: FBchatException if request failed
             * */

            var data = new Dictionary<string, object>()
            {
                { "question_id", poll_id }
            };
            var j = await this._payload_post("/ajax/mercury/get_poll_options", data);
            return j.Select((m) => FB_PollOption._from_graphql(m)).ToList();
        }

        /// <summary>
        /// Fetches a :class:`Plan` object from the plan id
        /// </summary>
        /// <param name="plan_id">Plan ID to fetch from</param>
        /// <returns></returns>
        public async Task<FB_Plan> fetchPlanInfo(string plan_id)
        {
            /*
             * Fetches a :class:`Plan` object from the plan id
             * :param plan_id: Plan ID to fetch from
             * :return: :class:`Plan` object
             * :rtype: Plan
             * :raises: FBchatException if request failed
             * */

            var data = new Dictionary<string, object>()
            {
                { "event_reminder_id", plan_id }
            };
            var j = await this._payload_post("/ajax/eventreminder", data);
            return FB_Plan._from_fetch(j);
        }

        private async Task<JToken> _getPrivateData()
        {
            var j = await this.graphql_request(GraphQL.from_doc_id("1868889766468115", new Dictionary<string, object>()));
            return j["viewer"];
        }

        /// <summary>
        /// Fetches a list of user phone numbers.
        /// </summary>
        /// <returns>List of phone numbers</returns>
        public async Task<List<string>> getPhoneNumbers()
        {
            /*
             * Fetches a list of user phone numbers.
             * :return: List of phone numbers
             * :rtype: list
             * */
            var data = await this._getPrivateData();
            return data?["user"]?["all_phones"]?.Select((j) =>
                j["phone_number"]?["universal_number"]?.Value<string>()).ToList();
        }

        /// <summary>
        /// Fetches a list of user emails.
        /// </summary>
        /// <returns>List of emails</returns>
        public async Task<List<string>> getEmails()
        {
            /*
             * Fetches a list of user emails.
             * :return: List of emails
             * :rtype: list
             * */
            var data = await this._getPrivateData();
            return data?["all_emails"]?.Select((j) =>
                j["display_email"]?.Value<string>()).ToList();
        }

        /// <summary>
        /// Gets friend active status as an :class:`ActiveStatus` object.
        /// Returns ``null`` if status isn't known.
        /// .. warning::
        /// Only works when listening.
        /// </summary>
        /// <param name="user_id">ID of the user</param>
        /// <returns>Given user active status</returns>
        public FB_ActiveStatus getUserActiveStatus(string user_id)
        {
            /*
             * Gets friend active status as an :class:`ActiveStatus` object.
             * Returns ``null`` if status isn't known.
             * .. warning::
             * Only works when listening.
             * :param user_id: ID of the user
             * :return: Given user active status
             * :rtype: ActiveStatus
             * */
            return this._buddylist.GetValueOrDefault(user_id);
        }

        #endregion

        #region SEND METHODS

        private FB_Message _oldMessage(object message)
        {
            return message is FB_Message ? (FB_Message)message : new FB_Message((string)message);
        }

        private Dictionary<string, object> _getSendData(FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /* Returns the data needed to send a request to `SendURL` */
            string messageAndOTID = Utils.generateOfflineThreadingID();
            long timestamp = Utils.now();
            var date = DateTime.Now;
            var data = new Dictionary<string, object> {
                { "client", "mercury" },
                { "author" , "fbid:" + this.uid },
                { "timestamp" , timestamp.ToString() },
                { "source" , "source:chat:web" },
                { "offline_threading_id", messageAndOTID },
                { "message_id" , messageAndOTID },
                { "threading_id", Utils.generateMessageID(this._client_id) },
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

            if (message.quick_replies != null)
            {
                var xmd = new Dictionary<string, object>() { { "quick_replies", new List<Dictionary<string, object>>() } };
                foreach (var quick_reply in message.quick_replies)
                {
                    var q = new Dictionary<string, object>();
                    q["content_type"] = quick_reply._type;
                    q["payload"] = quick_reply.payload;
                    q["external_payload"] = quick_reply.external_payload;
                    q["data"] = quick_reply.data;
                    if (quick_reply.is_response)
                        q["ignore_for_webhook"] = false;
                    if (quick_reply is FB_QuickReplyText)
                        q["title"] = ((FB_QuickReplyText)quick_reply).title;
                    if (!(quick_reply is FB_QuickReplyLocation))
                        q["image_url"] = quick_reply.GetType().GetRuntimeProperty("image_url").GetValue(quick_reply, null);
                    ((List<Dictionary<string, object>>)xmd["quick_replies"]).Add(q);
                }
                if (message.quick_replies.Count == 1 && message.quick_replies[0].is_response)
                    xmd["quick_replies"] = (((List<Dictionary<string, object>>)xmd["quick_replies"])[0];
                data["platform_xmd"] = JsonConvert.SerializeObject(xmd);
            }
            if (message.reply_to_id != null)
                data["replied_to_message_id"] = message.reply_to_id;

            return data;
        }

        private async Task<dynamic> _doSendRequest(Dictionary<string, object> data, bool get_thread_id = false)
        {
            /* Sends the data to `SendURL`, and returns the message ID or null on failure */
            var j = (JToken)(await this._post("/messaging/send/", data));

            var fb_dtsg = Utils.get_jsmods_require(j, 2)?.Value<string>();
            if (fb_dtsg != null)
                this._state.fb_dtsg = fb_dtsg;

            try
            {
                var message_ids = j["payload"]["actions"].Where(action => action["message_id"] != null).Select(
                    action => new { MSG = action["message_id"].Value<string>(), THR = action["thread_fbid"].Value<string>() }
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

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="thread_id">User / Group ID to send to</param>
        /// <param name="thread_type">ThreadType enum</param>
        /// <returns>Message ID of the sent message</returns>
        public async Task<string> send(FB_Message message = null, string thread_id = null, ThreadType? thread_type = null)
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
            var data = this._getSendData(message: message, thread_id: thread.Item1, thread_type: thread.Item2);
            return await this._doSendRequest(data);
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.send` instead")]
        public async Task<string> sendMessage(string message = null, string thread_id = null, ThreadType? thread_type = null)
        {
            return await this.send(new FB_Message(text: message), thread_id: thread_id, thread_type: thread_type);
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.send` instead")]
        public async Task<string> sendEmoji(string emoji = null, EmojiSize size = EmojiSize.SMALL, string thread_id = null, ThreadType? thread_type = null)
        {
            return await this.send(new FB_Message(text: emoji, emoji_size: size), thread_id: thread_id, thread_type: thread_type);
        }

        /// <summary>
        /// :ref:`Message ID<intro_message_ids>` of the sent message
        /// </summary>
        /// <param name="wave_first">Whether to wave first or wave back</param>
        /// <param name="thread_id">User/Group ID to send to.See :ref:`intro_threads`</param>
        /// <param name="thread_type">See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task<string> wave(bool wave_first = true, string thread_id = null, ThreadType? thread_type = null)
        {
            /*
             * Says hello with a wave to a thread!
             * :param wave_first: Whether to wave first or wave back
             * :param thread_id: User/Group ID to send to.See :ref:`intro_threads`
             * :param thread_type: See :ref:`intro_threads`
             * :type thread_type: ThreadType
             * :return: :ref:`Message ID<intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, thread_type);
            var data = this._getSendData(thread_id: thread.Item1, thread_type: thread.Item2);
            data["action_type"] = "ma-type:user-generated-message";
            data["lightweight_action_attachment[lwa_state]"] = new string[] { wave_first ? "INITIATED" : "RECIPROCATED" };
            data["lightweight_action_attachment[lwa_type]"] = "WAVE";
            if (thread_type == ThreadType.USER)
                data["specific_to_list[0]"] = string.Format("fbid:{0}", thread.Item1);
            return await this._doSendRequest(data);
        }


        /// <summary>
        /// Replies to a chosen quick reply
        /// </summary>
        /// <param name="quick_reply">Quick reply to reply to</param>
        /// <param name="payload">Optional answer to the quick reply</param>
        /// <param name="thread_id">User/Group ID to send to.See :ref:`intro_threads`</param>
        /// <param name="thread_type">See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task<string> quickReply(FB_QuickReply quick_reply, dynamic payload = null, string thread_id = null, ThreadType? thread_type = null)
        {
            /*
             * Replies to a chosen quick reply
             * :param quick_reply: Quick reply to reply to
             * :param payload: Optional answer to the quick reply
             * :param thread_id: User/Group ID to send to.See :ref:`intro_threads`
             * :param thread_type: See :ref:`intro_threads`
             * :type quick_reply: QuickReply
             * :type thread_type: ThreadType
             * :return: :ref:`Message ID<intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             * */
            quick_reply.is_response = true;
            if (quick_reply is FB_QuickReplyText)
            {
                return await this.send(
                    new FB_Message(text: ((FB_QuickReplyText)quick_reply).title, quick_replies: new List<FB_QuickReply>() { quick_reply })
                );
            }
            else if (quick_reply is FB_QuickReplyLocation)
            {
                if (!(payload is FB_LocationAttachment))
                    throw new ArgumentException(
                        "Payload must be an instance of `fbchat-sharp.LocationAttachment`"
                    );
                return await this.sendLocation(
                    payload, thread_id: thread_id, thread_type: thread_type
                );
            }
            else if (quick_reply is FB_QuickReplyEmail)
            {
                if (payload == null)
                    payload = (await this.getEmails())[0];
                quick_reply.external_payload = quick_reply.payload;
                quick_reply.payload = payload;
                return await this.send(new FB_Message(text: payload, quick_replies: new List<FB_QuickReply>() { quick_reply }));
            }
            else if (quick_reply is FB_QuickReplyPhoneNumber)
            {
                if (payload == null)
                    payload = (await this.getPhoneNumbers())[0];
                quick_reply.external_payload = quick_reply.payload;
                quick_reply.payload = payload;
                return await this.send(new FB_Message(text: payload, quick_replies: new List<FB_QuickReply>() { quick_reply }));
            }
            return null;
        }

        /// <summary>
        /// Unsends a message(removes for everyone)
        /// </summary>
        /// <param name="mid">:ref:`Message ID<intro_message_ids>` of the message to unsend</param>
        /// <returns></returns>
        public async Task unsend(string mid)
        {
            /*
             * Unsends a message(removes for everyone)
             * :param mid: :ref:`Message ID<intro_message_ids>` of the message to unsend
             * */
            var data = new Dictionary<string, object>() { { "message_id", mid } };
            var j = await this._payload_post("/messaging/unsend_message/?dpr=1", data);
        }

        private async Task<dynamic> _sendLocation(
            FB_LocationAttachment location, bool current = true, FB_Message message = null, string thread_id = null, ThreadType? thread_type = null
        )
        {
            var thread = this._getThread(thread_id, thread_type);
            var data = this._getSendData(
                message: message, thread_id: thread.Item1, thread_type: thread.Item2
            );
            data["action_type"] = "ma-type:user-generated-message";
            data["location_attachment[coordinates][latitude]"] = location.latitude;
            data["location_attachment[coordinates][longitude]"] = location.longitude;
            data["location_attachment[is_current_location]"] = current;
            return await this._doSendRequest(data);
        }

        /// <summary>
        /// Sends a given location to a thread as the user's current location
        /// </summary>
        /// <param name="location">Location to send</param>
        /// <param name="message">Additional message</param>
        /// <param name="thread_id">User/Group ID to send to.See :ref:`intro_threads`</param>
        /// <param name="thread_type">See :ref:`intro_threads`</param>
        /// <returns>:ref:`Message ID<intro_message_ids>` of the sent message</returns>
        public async Task<string> sendLocation(FB_LocationAttachment location, FB_Message message = null, string thread_id = null, ThreadType? thread_type = null)
        {
            /*
             * Sends a given location to a thread as the user's current location
             * :param location: Location to send
             * :param message: Additional message
             * :param thread_id: User/Group ID to send to.See :ref:`intro_threads`
             * :param thread_type: See :ref:`intro_threads`
             * :type location: LocationAttachment
             * :type message: Message
             * :type thread_type: ThreadType
             * :return: :ref:`Message ID<intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             * */
            return await this._sendLocation(
                location: location,
                current: true,
                message: message,
                thread_id: thread_id,
                thread_type: thread_type
            );
        }

        /// <summary>
        /// Sends a given location to a thread as a pinned location
        /// </summary>
        /// <param name="location">Location to send</param>
        /// <param name="message">Additional message</param>
        /// <param name="thread_id">User/Group ID to send to.See :ref:`intro_threads`</param>
        /// <param name="thread_type">See :ref:`intro_threads`</param>
        /// <returns>:ref:`Message ID` of the sent message</returns>
        public async Task<string> sendPinnedLocation(FB_LocationAttachment location, FB_Message message = null, string thread_id = null, ThreadType? thread_type = null)
        {
            /*
             * Sends a given location to a thread as a pinned location
             * :param location: Location to send
             * :param message: Additional message
             * :param thread_id: User/Group ID to send to.See :ref:`intro_threads`
             * :param thread_type: See :ref:`intro_threads`
             * :type location: LocationAttachment
             * :type message: Message
             * :type thread_type: ThreadType
             * :return: :ref:`Message ID<intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             * */
            return await this._sendLocation(
                location: location,
                current: false,
                message: message,
                thread_id: thread_id,
                thread_type: thread_type
            );
        }

        private async Task<List<Tuple<string, string>>> _upload(List<FB_File> files, bool voice_clip = false)
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

            if (j["metadata"].Count() != files.Count)
                throw new FBchatException(
                    string.Format("Some files could not be uploaded: {0}", j));

            return j["metadata"].Select(md =>
                new Tuple<string, string>(md[Utils.mimetype_to_key(md["filetype"]?.Value<string>())]?.Value<string>(), md["filetype"]?.Value<string>())).ToList();
        }

        private async Task<dynamic> _sendFiles(
            List<Tuple<string, string>> files, FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*
             * Sends files from file IDs to a thread
             * `files` should be a list of tuples, with a file's ID and mimetype
             * */
            var thread = this._getThread(thread_id, thread_type);
            var data = this._getSendData(
                message: this._oldMessage(message),
                thread_id: thread.Item1,
                thread_type: thread.Item2
            );

            data["action_type"] = "ma-type:user-generated-message";
            data["has_attachment"] = true;

            foreach (var obj in files.Select((x, index) => new { f = x, i = index }))
                data[string.Format("{0}s[{1}]", Utils.mimetype_to_key(obj.f.Item2), obj.i)] = obj.f.Item1;

            return await this._doSendRequest(data);
        }

        /// <summary>
        /// Sends files from URLs to a thread
        /// </summary>
        /// <param name="file_urls">URLs of files to upload and send</param>
        /// <param name="message">Additional message</param>
        /// <param name="thread_id">User/Group ID to send to.See :ref:`intro_threads`</param>
        /// <param name="thread_type">See :ref:`intro_threads`</param>
        /// <returns>`Message ID of the sent files</returns>
        public async Task<dynamic> sendRemoteFiles(
            List<string> file_urls, FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*
             * Sends files from URLs to a thread
             * :param file_urls: URLs of files to upload and send
             * :param message: Additional message
             * :param thread_id: User/Group ID to send to.See :ref:`intro_threads`
             * :param thread_type: See :ref:`intro_threads`
             * :type thread_type: ThreadType
             * :return: :ref:`Message ID<intro_message_ids>` of the sent files
             * :raises: FBchatException if request failed
             * */
            var ufile_urls = Utils.require_list<string>(file_urls);
            var files = await this._upload(await this._state.get_files_from_urls(ufile_urls));
            return await this._sendFiles(
                files: files, message: message, thread_id: thread_id, thread_type: thread_type
            );
        }

        /// <summary>
        /// Sends local files to a thread
        /// </summary>
        /// <param name="file_paths">Paths of files to upload and send</param>
        /// <param name="message">Additional message</param>
        /// <param name="thread_id">User/Group ID to send to. See :ref:`intro_threads`</param>
        /// <param name="thread_type">See :ref:`intro_threads`</param>
        /// <returns>:ref:`Message ID` of the sent files</returns>
        public async Task<string> sendLocalFiles(Dictionary<string, Stream> file_paths = null, FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*
             * Sends local files to a thread
             * :param file_paths: Paths of files to upload and send
             * :param message: Additional message
             * :param thread_id: User/Group ID to send to. See :ref:`intro_threads`
             * :param thread_type: See :ref:`intro_threads`
             * :type thread_type: ThreadType
             * :return: :ref:`Message ID <intro_message_ids>` of the sent files
             * :raises: FBchatException if request failed
             */

            var files = await this._upload(this._state.get_files_from_paths(file_paths));
            return await this._sendFiles(files: files, message: message, thread_id: thread_id, thread_type: thread_type);
        }

        /// <summary>
        /// Sends voice clips from URLs to a thread
        /// </summary>
        /// <param name="clip_urls">URLs of voice clips to upload and send</param>
        /// <param name="message">Additional message</param>
        /// <param name="thread_id">User/Group ID to send to.See :ref:`intro_threads`</param>
        /// <param name="thread_type">See :ref:`intro_threads`</param>
        /// <returns>`Message ID of the sent files</returns>
        public async Task<dynamic> sendRemoteVoiceClips(
            List<string> clip_urls, FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*
             * Sends voice clips from URLs to a thread
             * :param clip_urls: URLs of clips to upload and send
             * :param message: Additional message
             * :param thread_id: User/Group ID to send to.See :ref:`intro_threads`
             * :param thread_type: See :ref:`intro_threads`
             * :type thread_type: ThreadType
             * :return: :ref:`Message ID<intro_message_ids>` of the sent files
             * :raises: FBchatException if request failed
             * */
            var uclip_urls = Utils.require_list<string>(clip_urls);
            var files = await this._upload(await this._state.get_files_from_urls(uclip_urls), voice_clip: true);
            return await this._sendFiles(
                files: files, message: message, thread_id: thread_id, thread_type: thread_type
            );
        }

        /// <summary>
        /// Sends local voice clips to a thread
        /// </summary>
        /// <param name="clip_paths">Paths of voice clips to upload and send</param>
        /// <param name="message">Additional message</param>
        /// <param name="thread_id">User/Group ID to send to. See :ref:`intro_threads`</param>
        /// <param name="thread_type">See :ref:`intro_threads`</param>
        /// <returns>:ref:`Message ID` of the sent files</returns>
        public async Task<string> sendLocalVoiceClips(Dictionary<string, Stream> clip_paths = null, FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*
             * Sends local voice clips to a thread
             * :param file_paths: Paths of files to upload and send
             * :param message: Additional message
             * :param thread_id: User/Group ID to send to. See :ref:`intro_threads`
             * :param thread_type: See :ref:`intro_threads`
             * :type thread_type: ThreadType
             * :return: :ref:`Message ID <intro_message_ids>` of the sent files
             * :raises: FBchatException if request failed
             */

            var files = await this._upload(this._state.get_files_from_paths(clip_paths), voice_clip: true);
            return await this._sendFiles(files: files, message: message, thread_id: thread_id, thread_type: thread_type);
        }

        /// <summary>
        /// Sends an image to a thread
        /// </summary>
        [Obsolete("Deprecated.")]
        public async Task<string> sendImage(string image_id = null, FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER, bool is_gif = false)
        {
            string mimetype = null;
            if (!is_gif)
                mimetype = "image/png";
            else
                mimetype = "image/gif";

            return await this._sendFiles(
                files: new List<Tuple<string, string>>() { new Tuple<string, string>(image_id, mimetype) },
                message: message,
                thread_id: thread_id,
                thread_type: thread_type);
        }

        /// <summary>
        /// Sends an image from a URL to a thread
        /// </summary>
        /// <param name="image_url"></param>
        /// <param name="message"></param>
        /// <param name="thread_id"></param>
        /// <param name="thread_type"></param>
        /// <returns></returns>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.sendRemoteImage` instead")]
        public async Task<string> sendRemoteImage(string image_url = null, FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
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

            return await this.sendRemoteFiles(
                file_urls: new List<string>() { image_url },
                message: message,
                thread_id: thread_id,
                thread_type: thread_type);
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
        [Obsolete("Deprecated. Use :func:`fbchat.Client.sendLocalFiles` instead")]
        public async Task<string> sendLocalImage(string image_path = null, Stream data = null, FB_Message message = null, string thread_id = null, ThreadType thread_type = ThreadType.USER)
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

            return await this.sendLocalFiles(
                file_paths: new Dictionary<string, Stream>() { { image_path, data } },
                message: message,
                thread_id: thread_id,
                thread_type: thread_type);
        }

        /// <summary>
        /// Forwards an attachment
        /// </summary>
        /// <param name="attachment_id">Attachment ID to forward</param>
        /// <param name="thread_id">User/Group ID to send to.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task forwardAttachment(string attachment_id, string thread_id = null)
        {
            /*
             * Forwards an attachment
             * :param attachment_id: Attachment ID to forward
             * :param thread_id: User/Group ID to send to.See :ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);
            var data = new Dictionary<string, object>(){
                { "attachment_id", attachment_id },
                { string.Format("recipient_map[{0]",Utils.generateOfflineThreadingID()), thread.Item1 }
            };
            var j = await this._payload_post("/mercury/attachments/forward/", data);
            if (j["success"] == null)
                throw new FBchatFacebookError(
                string.Format("Failed forwarding attachment: {0}", j["error"]),
                fb_error_message: j["error"]?.Value<string>()
            );
        }

        /// <summary>
        /// Creates a group with the given ids
        /// </summary>
        /// <param name="message">The initial message</param>
        /// <param name="user_ids">A list of users to create the group with.</param>
        /// <returns>ID of the new group</returns>
        public async Task<string> createGroup(string message, List<string> user_ids)
        {
            /*
             * Creates a group with the given ids
             * :param message: The initial message
             * :param user_ids: A list of users to create the group with.
             * :return: ID of the new group
             * :raises: FBchatException if request failed
             * */
            var data = this._getSendData(message: this._oldMessage(message));

            if (user_ids.Count < 2)
                throw new FBchatUserError("Error when creating group: Not enough participants");

            foreach (var obj in user_ids.Concat(new string[] { this._uid }).Select((x, index) => new { user_id = x, i = index }))
                data[string.Format("specific_to_list[{0}]", obj.i)] = string.Format("fbid:{0}", obj.user_id);

            var req = await this._doSendRequest(data, get_thread_id: true);
            if (req.THR == null)
                throw new FBchatException(
                    "Error when creating group: No thread_id could be found"
                );
            return req.THR;
        }

        /// <summary>
        /// Adds users to a group.
        /// </summary>
        /// <param name="user_ids">One or more user IDs to add</param>
        /// <param name="thread_id">Group ID to add people to.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task<dynamic> addUsersToGroup(List<string> user_ids, string thread_id = null)
        {
            /*
             * Adds users to a group.
             * :param user_ids: One or more user IDs to add
             * :param thread_id: Group ID to add people to.See :ref:`intro_threads`
             * :type user_ids: list
             * :raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);
            var data = this._getSendData(thread_id: thread.Item1, thread_type: ThreadType.GROUP);

            data["action_type"] = "ma-type:log-message";
            data["log_message_type"] = "log:subscribe";

            var uuser_ids = Utils.require_list<string>(user_ids);

            foreach (var obj in user_ids.Select((x, index) => new { user_id = x, i = index }))
            {
                if (obj.user_id == this._uid)
                    throw new FBchatUserError(
                            "Error when adding users: Cannot add self to group thread"
                    );
                else
                    data[
                        string.Format("log_message_data[added_participants][{0}]", obj.i)
                    ] = string.Format("fbid:{0}", user_id);
            }
            return await this._doSendRequest(data);
        }

        /// <summary>
        /// Removes users from a group.
        /// </summary>
        /// <param name="user_id">User ID to remove</param>
        /// <param name="thread_id">Group ID to remove people from</param>
        /// <returns></returns>
        public async Task removeUserFromGroup(string user_id, string thread_id = null)
        {
            /*
             * Removes users from a group.
             * :param user_id: User ID to remove
             * :param thread_id: Group ID to remove people from.See: ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);

            var data = new Dictionary<string, object>() { { "uid", user_id }, { "tid", thread.Item1 } };
            var j = await this._payload_post("/chat/remove_participants/", data);
        }

        private async Task _adminStatus(List<string> admin_ids, bool admin, string thread_id = null)
        {
            var thread = this._getThread(thread_id, null);

            var data = new Dictionary<string, object>() { { "add", admin.ToString() }, { "thread_fbid", thread.Item1 } };

            var uadmin_ids = Utils.require_list<string>(admin_ids);

            foreach (var obj in admin_ids.Select((x, index) => new { admin_id = x, i = index }))
                data[string.Format("admin_ids[{0}]", obj.i)] = obj.admin_id;

            var j = await this._payload_post("/messaging/save_admins/?dpr=1", data);
        }


        /// <summary>
        /// Sets specifed users as group admins.
        /// </summary>
        /// <param name="admin_ids">One or more user IDs to set admin</param>
        /// <param name="thread_id">Group ID to remove people from</param>
        /// <returns></returns>
        public async Task addGroupAdmins(List<string> admin_ids, string thread_id = null)
        {
            /*
             * Sets specifed users as group admins.
             * :param admin_ids: One or more user IDs to set admin
             * :param thread_id: Group ID to remove people from. See: ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            await this._adminStatus(admin_ids, true, thread_id);
        }

        /// <summary>
        /// Removes admin status from specifed users.
        /// </summary>
        /// <param name="admin_ids">One or more user IDs to remove admin</param>
        /// <param name="thread_id">Group ID to remove people from</param>
        /// <returns></returns>
        public async Task removeGroupAdmins(List<string> admin_ids, string thread_id = null)
        {
            /*
             * Removes admin status from specifed users.
             * :param admin_ids: One or more user IDs to remove admin
             * :param thread_id: Group ID to remove people from. See: ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            await this._adminStatus(admin_ids, false, thread_id);
        }

        /// <summary>
        /// Changes group's approval mode
        /// </summary>
        /// <param name="require_admin_approval">true or false</param>
        /// <param name="thread_id">Group ID</param>
        /// <returns></returns>
        public async Task changeGroupApprovalMode(bool require_admin_approval, string thread_id = null)
        {
            /*
             * Changes group's approval mode
             * :param require_admin_approval: true or false
             * : param thread_id: Group ID to remove people from.See: ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);
            var data = new Dictionary<string, object>() { { "set_mode", require_admin_approval ? 1 : 0 }, { "thread_fbid", thread.Item1 } };
            var j = await this._payload_post("/messaging/set_approval_mode/?dpr=1", data);
        }

        private async Task _usersApproval(List<string> user_ids, bool approve, string thread_id = null)
        {
            var thread = this._getThread(thread_id, null);
            var uuser_ids = Utils.require_list<string>(user_ids).ToList();

            var data = new Dictionary<string, object>(){
                { "client_mutation_id", "0"},
                {"actor_id", this._uid },
                { "thread_fbid", thread.Item1 },
                { "user_ids", user_ids },
                { "response", approve ? "ACCEPT" : "DENY"},
                { "surface", "ADMIN_MODEL_APPROVAL_CENTER"}
            };
            var j = await this.graphql_request(
                GraphQL.from_doc_id("1574519202665847", new Dictionary<string, object>(){
                { "data", data} })
            );
        }

        /// <summary>
        /// Accepts users to the group from the group's approval
        /// </summary>
        /// <param name="user_ids">One or more user IDs to accept</param>
        /// <param name="thread_id">Group ID</param>
        /// <returns></returns>
        public async Task acceptUsersToGroup(List<string> user_ids, string thread_id = null)
        {
            /*
             * Accepts users to the group from the group's approval
             * :param user_ids: One or more user IDs to accept
             * : param thread_id: Group ID to accept users to.See: ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            await this._usersApproval(user_ids, true, thread_id);
        }

        /// <summary>
        /// Denies users from the group 's approval
        /// </summary>
        /// <param name="user_ids">One or more user IDs to deny</param>
        /// <param name="thread_id">Group ID</param>
        /// <returns></returns>
        public async Task denyUsersFromGroup(List<string> user_ids, string thread_id = null)
        {
            /*
             * Denies users from the group 's approval
             * :param user_ids: One or more user IDs to deny
             * : param thread_id: Group ID to deny users from.See: ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            await this._usersApproval(user_ids, false, thread_id);
        }

        /// <summary>
        /// Changes a thread image from an image id
        /// </summary>
        /// <param name="image_id">ID of uploaded image</param>
        /// <param name="thread_id">User / Group ID to change image.See: ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task<string> _changeGroupImage(string image_id, string thread_id = null)
        {
            /*
             * Changes a thread image from an image id
             * :param image_id: ID of uploaded image
             * :param thread_id: User / Group ID to change image.See: ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);

            var data = new Dictionary<string, object>() { { "thread_image_id", image_id }, { "thread_id", thread.Item1 } };

            var j = await this._payload_post("/messaging/set_thread_image/?dpr=1", data);
            return image_id;
        }

        /// <summary>
        /// Changes a thread image from a URL
        /// </summary>
        /// <param name="image_url">URL of an image to upload and change</param>
        /// <param name="thread_id">User / Group ID to change image.</param>
        /// <returns></returns>
        public async Task<string> changeGroupImageRemote(string image_url, string thread_id = null)
        {
            /*
             * Changes a thread image from a URL
             * :param image_url: URL of an image to upload and change
             * :param thread_id: User / Group ID to change image.See: ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            var upl = await this._upload(await this._state.get_files_from_urls(new HashSet<string>() { image_url }));
            return await this._changeGroupImage(upl[0].Item1, thread_id);
        }

        /// <summary>
        /// Changes a thread image from a local path
        /// </summary>
        /// <param name="image_path">Path of an image to upload and change</param>
        /// <param name="image_stream"></param>
        /// <param name="thread_id">User / Group ID to change image.</param>
        /// <returns></returns>
        public async Task<string> changeGroupImageLocal(string image_path, Stream image_stream, string thread_id = null)
        {
            /*
             * Changes a thread image from a local path
             * :param image_path: Path of an image to upload and change
             * :param thread_id: User / Group ID to change image.See: ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            var files = this._state.get_files_from_paths(new Dictionary<string, Stream>() { { image_path, image_stream } });
            var upl = await this._upload(files);
            return await this._changeGroupImage(upl[0].Item1, thread_id);
        }

        /// <summary>
        /// Changes title of a thread.
        /// If this is executed on a user thread, this will change the nickname of that user, effectively changing the title
        /// </summary>
        /// <param name="title">New group thread title</param>
        /// <param name="thread_id">Group ID to change title of. See: ref:`intro_threads`</param>
        /// <param name="thread_type"></param>
        /// <returns></returns>
        public async Task changeThreadTitle(string title, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            /*
             * Changes title of a thread.
             * If this is executed on a user thread, this will change the nickname of that user, effectively changing the title
             * :param title: New group thread title
             * :param thread_id: Group ID to change title of. See: ref:`intro_threads`
             * :param thread_type: See: ref:`intro_threads`
             * :type thread_type: ThreadType
             * : raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, thread_type);

            if (thread_type == ThreadType.USER)
                // The thread is a user, so we change the user's nickname
                await this.changeNickname(
                    title, thread.Item1, thread_id: thread.Item1, thread_type: thread.Item2
                );

            var data = new Dictionary<string, object>() { { "thread_name", title }, { "thread_id", thread.Item1 } };
            var j = await this._payload_post("/messaging/set_thread_name/?dpr=1", data);
        }

        /// <summary>
        /// Changes the nickname of a user in a thread
        /// </summary>
        /// <param name="nickname">New nickname</param>
        /// <param name="user_id">User that will have their nickname changed</param>
        /// <param name="thread_id">User / Group ID to change color of</param>
        /// <param name="thread_type"></param>
        /// <returns></returns>
        public async Task changeNickname(
            string nickname, string user_id, string thread_id = null, ThreadType thread_type = ThreadType.USER
        )
        {
            /*
             * Changes the nickname of a user in a thread
             * :param nickname: New nickname
             * :param user_id: User that will have their nickname changed
             * : param thread_id: User / Group ID to change color of.See :ref:`intro_threads`
             * :param thread_type: See: ref:`intro_threads`
             * :type thread_type: ThreadType
             * : raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, thread_type);

            var data = new Dictionary<string, object>() {
                {    "nickname", nickname },
                { "participant_id", user_id },
                { "thread_or_other_fbid", thread.Item1 }
            };
            var j = await this._payload_post(
                "/messaging/save_thread_nickname/?source=thread_settings&dpr=1", data);
        }

        /// <summary>
        /// Changes thread color
        /// </summary>
        /// <param name="color">New thread color</param>
        /// <param name="thread_id">User / Group ID to change color of.</param>
        /// <returns></returns>
        public async Task changeThreadColor(string color, string thread_id = null)
        {
            /*
             * Changes thread color
             * : param color: New thread color
             * : param thread_id: User / Group ID to change color of.See :ref:`intro_threads`
             * :type color: ThreadColor
             * : raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);

            var data = new Dictionary<string, object>() {
                { "color_choice", color != ThreadColor.MESSENGER_BLUE ? color : ""},
                { "thread_or_other_fbid", thread.Item1}
            };
            var j = await this._payload_post(
                "/messaging/save_thread_color/?source=thread_settings&dpr=1", data);
        }

        /// <summary>
        /// Changes thread color
        /// </summary>
        /// <param name="emoji">While changing the emoji, the Facebook web client actually sends multiple different requests, though only this one is required to make the change</param>
        /// <param name="thread_id">User / Group ID to change emoji of.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task changeThreadEmoji(string emoji, string thread_id = null)
        {
            /*
             * Changes thread color
             * Trivia: While changing the emoji, the Facebook web client actually sends multiple different requests, though only this one is required to make the change
             * : param emoji: New thread emoji
             * : param thread_id: User / Group ID to change emoji of.See :ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);

            var data = new Dictionary<string, object>() { { "emoji_choice", emoji }, { "thread_or_other_fbid", thread.Item1 } };
            var j = await this._payload_post(
                "/messaging/save_thread_emoji/?source=thread_settings&dpr=1", data);
        }

        /// <summary>
        /// Reacts to a message, or removes reaction
        /// </summary>
        /// <param name="message_id">`Message ID` to react to</param>
        /// <param name="reaction">Reaction emoji to use, if null removes reaction</param>
        /// <returns></returns>
        public async Task reactToMessage(string message_id, MessageReaction? reaction = null)
        {
            /*
             * Reacts to a message, or removes reaction
             * :param message_id: :ref:`Message ID<intro_message_ids>` to react to
             * :param reaction: Reaction emoji to use, if null removes reaction
             :type reaction: MessageReaction or null
             : raises: FBchatException if request failed
             */
            var data = new Dictionary<string, object>() {
                { "action", reaction != null ? "ADD_REACTION", "REMOVE_REACTION"},
                {"client_mutation_id", "1"},
                {"actor_id", this._uid},
                {"message_id", message_id},
                {"reaction", reaction != null ? reaction.Value.GetEnumDescriptionAttribute() : null}
            };

            var payl = new Dictionary<string, object>() { { "doc_id", 1491398900900362 }, { "variables", JsonConvert.SerializeObject(new Dictionary<string, object>() { { "data", data } }) } };
            var j = await this._payload_post("/webgraphql/mutation", payl);
            Utils.handle_graphql_errors(j);
        }

        /// <summary>
        /// Sets a plan
        /// </summary>
        /// <param name="plan">Plan to set</param>
        /// <param name="thread_id">User / Group ID to send plan to.</param>
        /// <returns></returns>
        public async Task createPlan(FB_Plan plan, string thread_id = null)
        {
            /*
             * Sets a plan
             * : param plan: Plan to set
             * : param thread_id: User / Group ID to send plan to.See :ref:`intro_threads`
             * :type plan: Plan
             * : raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);

            var data = new Dictionary<string, object>() {
                { "event_type", "EVENT" },
                {"event_time", plan.time},
                { "title", plan.title},
                {"thread_id", thread.Item1},
                {"location_id", plan.location_id ?? ""},
                {"location_name", plan.location ?? ""},
                {"acontext", Client_Constants.ACONTEXT},
            };

            var j = await this._payload_post("/ajax/eventreminder/create", data);
            if (j["error"] != null)
                throw new FBchatFacebookError(
                        string.Format("Failed creating plan: {0}", j["error"]),
                        fb_error_message: j["error"]?.Value<string>());
        }

        /// <summary>
        /// Edits a plan
        /// </summary>
        /// <param name="plan">Plan to edit</param>
        /// <param name="new_plan">New plan</param>
        /// <returns></returns>
        public async Task editPlan(FB_Plan plan, FB_Plan new_plan)
        {
            /*
             * Edits a plan
             * : param plan: Plan to edit
             * : param new_plan: New plan
             * :type plan: Plan
             * : raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() {
                { "event_reminder_id", plan.uid},
                {"delete", "false"},
                {"date", new_plan.time},
                {"location_name", new_plan.location ?? ""},
                {"location_id", new_plan.location_id ?? ""},
                {"title", new_plan.title},
                {"acontext", Client_Constants.ACONTEXT },
            };
            var j = await this._payload_post("/ajax/eventreminder/submit", data);
        }

        /// <summary>
        /// Deletes a plan
        /// </summary>
        /// <param name="plan">Plan to delete.</param>
        /// <returns></returns>
        public async Task deletePlan(FB_Plan plan)
        {
            /*
             * Deletes a plan
             * : param plan: Plan to delete
             * : raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() { { "event_reminder_id", plan.uid }, { "delete", "true" }, { "acontext", Client_Constants.ACONTEXT } };
            var j = await this._payload_post("/ajax/eventreminder/submit", data);
        }

        /// <summary>
        /// Changes participation in a plan
        /// </summary>
        /// <param name="plan">Plan to take part in or not</param>
        /// <param name="take_part">Whether to take part in the plan</param>
        /// <returns></returns>
        public async Task changePlanParticipation(FB_Plan plan, bool take_part = true)
        {
            /*
             * Changes participation in a plan
             * :param plan: Plan to take part in or not
             * :param take_part: Whether to take part in the plan
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() {
                { "event_reminder_id", plan.uid },
                {"guest_state", take_part ? "GOING" : "DECLINED"},
                { "acontext", Client_Constants.ACONTEXT },
            };
            var j = await this._payload_post("/ajax/eventreminder/rsvp", data);
        }

        [Obsolete("Deprecated. Use :func:`fbchat.Client.createPlan` instead")]
        public async Task eventReminder(string thread_id, string time, string title, string location = "", string location_id = "")
        {
            /*
             * Deprecated.Use :func:`fbchat.Client.createPlan` instead
             * */
            var plan = new FB_Plan(time: time, title: title, location: location, location_id: location_id);
            await this.createPlan(plan: plan, thread_id: thread_id);
        }

        /// <summary>
        /// Creates poll in a group thread
        /// </summary>
        /// <param name="poll">Poll to create</param>
        /// <param name="thread_id">User / Group ID to create poll in.</param>
        /// <returns></returns>
        public async Task createPoll(FB_Poll poll, string thread_id = null)
        {
            /*
             * Creates poll in a group thread
             * : param poll: Poll to create
             * : param thread_id: User / Group ID to create poll in. See: ref:`intro_threads`
             * :type poll: Poll
             * : raises: FBchatException if request failed
             * */

            var thread = this._getThread(thread_id, null);

            // We're using ordered dicts, because the Facebook endpoint that parses the POST
            // parameters is badly implemented, and deals with ordering the options wrongly.
            // If you can find a way to fix this for the endpoint, or if you find another
            // endpoint, please do suggest it ;)
            var data = new Dictionary<string, object>(){
                { "question_text", poll.title }, {"target_id", thread.Item1 }};

            foreach (var obj in poll.options.Select((x, index) => new { option = x, i = index }))
            {
                data[string.Format("option_text_array[{0}]", obj.i)] = obj.option.text;
                data[string.Format("option_is_selected_array[{0}]", obj.i)] = (obj.option.vote ? 1 : 0).ToString();

                var j = await this._payload_post("/messaging/group_polling/create_poll/?dpr=1", data);
                if (j["status"]?.Value<string>() != "success")
                    throw new FBchatFacebookError(
                        string.Format("Failed creating poll: {0}", j["errorTitle"]),
                        fb_error_message: j["errorMessage"]?.Value<string>()
                );
            }
        }

        /// <summary>
        /// Updates a poll vote
        /// </summary>
        /// <param name="poll_id">ID of the poll to update vote</param>
        /// <param name="option_ids">List of the option IDs to vote</param>
        /// <param name="new_options">List of the new option names</param>
        /// <returns></returns>
        public async Task updatePollVote(string poll_id, List<string> option_ids = null, List<string> new_options = null)
        {
            /*
             * Updates a poll vote
             * :param poll_id: ID of the poll to update vote
             * : param option_ids: List of the option IDs to vote
             * : param new_options: List of the new option names
             * : param thread_id: User / Group ID to change status in. See: ref:`intro_threads`
             * :param thread_type: See: ref:`intro_threads`
             * :type thread_type: ThreadType
             * : raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() { { "question_id", poll_id } };

            if (option_ids != null)
            {
                foreach (var obj in option_ids.Select((x, index) => new { option_id = x, i = index }))
                    data[string.Format("selected_options[{0}]", obj.i)] = obj.option_id;
            }

            if (new_options != null)
            {
                foreach (var obj in new_options.Select((x, index) => new { option_text = x, i = index }))
                    data[string.Format("new_options[{0}]", obj.i)] = obj.option_text;
            }

            var j = await this._payload_post("/messaging/group_polling/update_vote/?dpr=1", data);
            if (j["status"]?.Value<string>() != "success")
                throw new FBchatFacebookError(
                    string.Format("Failed updating poll vote: {0}", j["errorTitle"]),
                    fb_error_message: j["errorMessage"]?.Value<string>()
                );
        }

        /// <summary>
        /// Sets users typing status in a thread
        /// </summary>
        /// <param name="status">Specify the typing status</param>
        /// <param name="thread_id">User / Group ID to change status in. See: ref:`intro_threads`</param>
        /// <param name="thread_type">See: ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task setTypingStatus(TypingStatus status, string thread_id = null, ThreadType? thread_type = null)
        {
            /*
             * Sets users typing status in a thread
             * :param status: Specify the typing status
             * :param thread_id: User / Group ID to change status in. See: ref:`intro_threads`
             * :param thread_type: See: ref:`intro_threads`
             * :type status: TypingStatus
             * : type thread_type: ThreadType
             * : raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, thread_type);

            var data = new Dictionary<string, object>() {
                { "typ", (int)status },
                { "thread", thread.Item1 },
                { "to", thread_type == ThreadType.USER ? thread.Item1 : ""},
                {"source", "mercury-chat"}
            };
            var j = await this._payload_post("/ajax/messaging/typ.php", data);
        }

        #endregion

        #region MARK METHODS
        /// <summary>
        /// Mark a message as delivered
        /// </summary>
        /// <param name="thread_id">User/Group ID to which the message belongs.See :ref:`intro_threads`</param>
        /// <param name="message_id">Message ID to set as delivered.See :ref:`intro_threads`</param>
        /// <returns>true</returns>
        public async Task<bool> markAsDelivered(string thread_id, string message_id)
        {
            /*
             * Mark a message as delivered
             * :param thread_id: User/Group ID to which the message belongs.See :ref:`intro_threads`
             * :param message_id: Message ID to set as delivered.See :ref:`intro_threads`
             * :return: true
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>{
            { "message_ids[0]", message_id },
            {string.Format("thread_ids[{0}][0]",thread_id), message_id}};

            var j = await this._payload_post("/ajax/mercury/delivery_receipts.php", data);
            return true;
        }

        private async Task _readStatus(bool read, List<string> thread_ids)
        {
            var uthread_ids = Utils.require_list<string>(thread_ids);

            var data = new Dictionary<string, object> { { "watermarkTimestamp", Utils.now() }, { "shouldSendReadReceipt", "true" } };

            foreach (var thread_id in uthread_ids)
                data[string.Format("ids[{0}]", thread_id)] = read ? "true" : "false";

            var j = await this._payload_post("/ajax/mercury/change_read_status.php", data);
        }

        /// <summary>
        /// Mark threads as read
        /// All messages inside the threads will be marked as read
        /// </summary>
        /// <param name="thread_ids">User/Group IDs to set as read.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task markAsRead(List<string> thread_ids = null)
        {
            /*
             * Mark threads as read
             * All messages inside the threads will be marked as read
             * :param thread_ids: User/Group IDs to set as read.See :ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            await this._readStatus(true, thread_ids);
        }

        /// <summary>
        /// Mark threads as unread
        /// All messages inside the threads will be marked as unread
        /// </summary>
        /// <param name="thread_ids">User/Group IDs to set as unread.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task markAsUnread(List<string> thread_ids = null)
        {
            /*
             * Mark threads as unread
             * All messages inside the threads will be marked as unread
             * :param thread_ids: User/Group IDs to set as unread.See :ref:`intro_threads`
             * :raises: FBchatException if request failed
             * */
            await this._readStatus(false, thread_ids);
        }

        public async Task markAsSeen()
        {
            /*
             * .. todo::
             * Documenting this
             * */
            var j = await this._payload_post("/ajax/mercury/mark_seen.php", new Dictionary<string, object>() { { "seen_timestamp", Utils.now() } });
        }

        public async Task friendConnect(string friend_id)
        {
            /*
             * .. todo::
             * Documenting this
             * */
            var data = new Dictionary<string, object> { { "to_friend", friend_id }, { "action", "confirm" } };

            var j = await this._payload_post("/ajax/add_friend/action.php?dpr=1", data);
        }

        /// <summary>
        /// Removes a specifed friend from your friend list
        /// </summary>
        /// <param name="friend_id">The ID of the friend that you want to remove</param>
        /// <returns>true</returns>
        public async Task<bool> removeFriend(string friend_id = null)
        {
            /*
             * Removes a specifed friend from your friend list
             * :param friend_id: The ID of the friend that you want to remove
             * :return: true
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object> { { "uid", friend_id } };
            var j = await this._payload_post("/ajax/profile/removefriendconfirm.php", data);
            return true;
        }

        /// <summary>
        /// Blocks messages from a specifed user
        /// </summary>
        /// <param name="user_id">The ID of the user that you want to block</param>
        /// <returns>true</returns>
        public async Task<bool> blockUser(string user_id)
        {
            /*
             * Blocks messages from a specifed user
             * :param user_id: The ID of the user that you want to block
             * :return: true
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object> { { "fbid", user_id } };
            var j = await this._payload_post("/messaging/block_messages/?dpr=1", data);
            return true;
        }

        /// <summary>
        /// The ID of the user that you want to block
        /// </summary>
        /// <param name="user_id">The ID of the user that you want to unblock</param>
        /// <returns>Whether the request was successful</returns>
        public async Task<bool> unblockUser(string user_id)
        {
            /*
             * Unblocks messages from a blocked user
             * :param user_id: The ID of the user that you want to unblock
             * :return: Whether the request was successful
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object> { { "fbid", user_id } };
            var j = await this._payload_post("/messaging/unblock_messages/?dpr=1", data);
            return true;
        }

        /// <summary>
        /// Moves threads to specifed location
        /// </summary>
        /// <param name="location">ThreadLocation: INBOX, PENDING, ARCHIVED or OTHER</param>
        /// <param name="thread_ids">Thread IDs to move.See :ref:`intro_threads`</param>
        /// <returns>true</returns>
        public async Task<bool> moveThreads(string location, List<string> thread_ids)
        {
            /*
             * Moves threads to specifed location
             * :param location: ThreadLocation: INBOX, PENDING, ARCHIVED or OTHER
             * :param thread_ids: Thread IDs to move.See :ref:`intro_threads`
             * :return: true
             * :raises: FBchatException if request failed
             * */
            var uthread_ids = Utils.require_list<string>(thread_ids);

            if (location == ThreadLocation.PENDING)
                location = ThreadLocation.OTHER;

            if (location == ThreadLocation.ARCHIVED)
            {
                var data_archive = new Dictionary<string, object>();
                var data_unpin = new Dictionary<string, object>();
                foreach (string thread_id in uthread_ids)
                {
                    data_archive[string.Format("ids[{0}]", thread_id)] = "true";
                    data_unpin[string.Format("ids[{0}]", thread_id)] = "false";
                }
                var j_archive = await this._payload_post(
                    "/ajax/mercury/change_archived_status.php?dpr=1", data_archive
                );
                var j_unpin = await this._payload_post(
                    "/ajax/mercury/change_pinned_status.php?dpr=1", data_unpin
                );
            }
            else
            {
                var data = new Dictionary<string, object>();
                foreach (var obj in thread_ids.Select((x, index) => new { thread_id = x, i = index }))
                    data[string.Format("{0}[{1}]", location.ToLower(), obj.i)] = obj.thread_id;
                var j = await this._payload_post("/ajax/mercury/move_thread.php", data);
            }
            return true;
        }

        /// <summary>
        /// Deletes threads
        /// </summary>
        /// <param name="thread_ids">Thread IDs to delete. See :ref:`intro_threads`</param>
        /// <returns>true</returns>
        public async Task<bool> deleteThreads(List<string> thread_ids)
        {
            /*
             * Deletes threads
             * :param thread_ids: Thread IDs to delete. See :ref:`intro_threads`
             * :return: true
             * :raises: FBchatException if request failed
             * */
            var uthread_ids = Utils.require_list<string>(thread_ids);

            var data_unpin = new Dictionary<string, object>();
            var data_delete = new Dictionary<string, object>();
            foreach (var obj in thread_ids.Select((x, index) => new { thread_id = x, i = index }))
            {
                data_unpin[string.Format("ids[{0}]", obj.thread_id)] = "false";
                data_delete[string.Format("ids[{0}]", obj.i)] = obj.thread_id;
            }
            var j_unpin = await this._payload_post(
                "/ajax/mercury/change_pinned_status.php?dpr=1", data_unpin
            );
            var j_delete = this._payload_post(
                "/ajax/mercury/delete_thread.php?dpr=1", data_delete
            );
            return true;
        }

        /// <summary>
        /// Mark a thread as spam and delete it
        /// </summary>
        /// <param name="thread_id">User/Group ID to mark as spam.See :ref:`intro_threads`</param>
        /// <returns>true</returns>
        public async Task<bool> markAsSpam(string thread_id = null)
        {
            /*
             * Mark a thread as spam and delete it
             * :param thread_id: User/Group ID to mark as spam.See :ref:`intro_threads`
             * :return: true
             * :raises: FBchatException if request failed
             * */
            var thread = this._getThread(thread_id, null);
            var j = await this._payload_post("/ajax/mercury/mark_spam.php?dpr=1", new Dictionary<string, object>() { { "id", thread.Item1 } });
            return true;
        }

        /// <summary>
        /// Deletes specifed messages
        /// </summary>
        /// <param name="message_ids">Message IDs to delete</param>
        /// <returns>true</returns>
        public async Task<bool> deleteMessages(List<string> message_ids)
        {
            /*
             * Deletes specifed messages
             * :param message_ids: Message IDs to delete
             * :return: true
             * :raises: FBchatException if request failed
             * */
            var umessage_ids = Utils.require_list<string>(message_ids);
            var data = new Dictionary<string, object>();
            foreach (var obj in umessage_ids.Select((x, index) => new { message_id = x, i = index }))
                data[string.Format("message_ids[{0}]", obj.i)] = obj.message_id;
            var j = await this._payload_post("/ajax/mercury/delete_messages.php?dpr=1", data);
            return true;
        }

        /// <summary>
        /// Mutes thread
        /// </summary>
        /// <param name="mute_time">Mute time in seconds, leave blank to mute forever</param>
        /// <param name="thread_id">User/Group ID to mute.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task muteThread(int mute_time = -1, string thread_id = null)
        {
            /*
             * Mutes thread
             * :param mute_time: Mute time in seconds, leave blank to mute forever
             * :param thread_id: User/Group ID to mute.See :ref:`intro_threads`
             * */
            var thread = this._getThread(thread_id, null);
            var data = new Dictionary<string, object> { { "mute_settings", mute_time.ToString() }, { "thread_fbid", thread.Item1 } };
            var j = await this._payload_post("/ajax/mercury/change_mute_thread.php?dpr=1", data);
        }

        /// <summary>
        /// Unmutes thread
        /// </summary>
        /// <param name="thread_id">User/Group ID to unmute.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task unmuteThread(string thread_id = null)
        {
            /*
             * Unmutes thread
             * :param thread_id: User/Group ID to unmute.See :ref:`intro_threads`
             * */
            await this.muteThread(0, thread_id);
        }

        /// <summary>
        /// Mutes thread reactions
        /// </summary>
        /// <param name="mute">Boolean.true to mute, false to unmute</param>
        /// <param name="thread_id">User/Group ID to mute.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task muteThreadReactions(bool mute = true, string thread_id = null)
        {
            /*
             * Mutes thread reactions
             * :param mute: Boolean.true to mute, false to unmute
             * :param thread_id: User/Group ID to mute.See :ref:`intro_threads`
             * */
            var thread = this._getThread(thread_id, null);
            var data = new Dictionary<string, object> { { "reactions_mute_mode", mute ? 1 : 0 }, { "thread_fbid", thread.Item1 } };
            var j = await this._payload_post(
                "/ajax/mercury/change_reactions_mute_thread/?dpr=1", data
            );
        }

        /// <summary>
        /// Unmutes thread reactions
        /// </summary>
        /// <param name="thread_id"></param>
        /// <returns>User/Group ID to unmute.See :ref:`intro_threads`</returns>
        public async Task unmuteThreadReactions(string thread_id = null)
        {
            /*
             * Unmutes thread reactions
             * :param thread_id: User/Group ID to unmute.See :ref:`intro_threads`
             * */
            await this.muteThreadReactions(false, thread_id);
        }

        /// <summary>
        /// Mutes thread mentions
        /// </summary>
        /// <param name="mute">Boolean.true to mute, false to unmute</param>
        /// <param name="thread_id">User/Group ID to mute.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task muteThreadMentions(bool mute = true, string thread_id = null)
        {
            /*
             * Mutes thread mentions
             * :param mute: Boolean.true to mute, false to unmute
             * :param thread_id: User/Group ID to mute.See :ref:`intro_threads`
             * */
            var thread = this._getThread(thread_id, null);
            var data = new Dictionary<string, object> { { "mentions_mute_mode", mute ? 1 : 0, { "thread_fbid", thread.Item1 } };
            var j = await this._payload_post("/ajax/mercury/change_mentions_mute_thread/?dpr=1", data);
        }

        /// <summary>
        /// Unmutes thread mentions
        /// </summary>
        /// <param name="thread_id">User/Group ID to unmute.See :ref:`intro_threads`</param>
        /// <returns></returns>
        public async Task unmuteThreadMentions(string thread_id = null)
        {
            /*
             * Unmutes thread mentions
             * :param thread_id: User/Group ID to unmute.See :ref:`intro_threads`
             * */
            await this.muteThreadMentions(false, thread_id);
        }
        #endregion

        #region LISTEN METHODS

        private async Task _ping(string sticky, string pool)
        {
            var data = new Dictionary<string, object>() {
                { "seq", this._seq},
                {"channel", "p_" + this._uid},
                { "clientid", this._client_id},
                { "partition", -2},
                { "cap", 0},
                { "uid", this._uid},
                { "sticky_token", this._sticky},
                { "sticky_pool", this._pool},
                { "viewer_uid", this._uid},
                { "state", "active"},
            };
            var j = await this._get(
                string.Format("https://{0}-edge-chat.facebook.com/active_ping", this._pull_channel), data);
        }

        private async Task<JToken> _pullMessage()
        {
            /*Call pull api with seq value to get message data.*/
            var data = new Dictionary<string, object>() {
                { "seq", this._seq },
                { "msgs_recv", 0 },
                {"sticky_token", this._sticky },
                {"sticky_pool", this._pool },
                {"clientid", this._client_id },
            };

            return await this._get(
                string.Format("https://{0}-edge-chat.facebook.com/pull", this._pull_channel), data
            );
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

        private async Task _parseDelta(JToken m)
        {
            var delta = m["delta"];
            var delta_type = delta["type"]?.Value<string>();
            var delta_class = delta["class"]?.Value<string>();
            var metadata = delta["messageMetadata"];

            var mid = metadata?["messageId"]?.Value<string>();
            var author_id = metadata?["actorFbId"]?.Value<string>();
            var ts = int.Parse(metadata?["timestamp"]?.Value<string>());

            // Added participants
            if (delta["addedParticipants"] != null)
            {
                var added_ids = delta["addedParticipants"].Select(x => x["userFbId"]?.Value<string>());
                var thread_id = metadata?["threadKey"]?["threadFbId"]?.Value<string>();
                this.onPeopleAdded(
                    mid: mid,
                    added_ids: added_ids,
                    author_id: author_id,
                    thread_id: thread_id,
                    ts: ts,
                    msg: m
                );
            }
            // Left/removed participants
            else if (delta["leftParticipantFbId"] != null)
            {
                var removed_id = delta["leftParticipantFbId"]?.Value<string>();
                var thread_id = metadata?["threadKey"]?["threadFbId"]?.Value<string>();
                this.onPersonRemoved(
                    mid: mid,
                    removed_id: removed_id,
                    author_id: author_id,
                    thread_id: thread_id,
                    ts: ts,
                    msg: m
                );
            }
            // Color change
            else if (delta["change_thread_theme"] != null)
            {
                var new_color = ThreadColor._from_graphql(delta["untypedData"]["theme_color"]);
                var thread = getThreadIdAndThreadType(metadata);
                this.onColorChange(
                    mid: mid,
                    author_id: author_id,
                    new_color: new_color,
                    thread_id: thread.Item1,
                    thread_type: thread.Item2,
                    ts: ts,
                    metadata: metadata,
                    msg: m
                );
            }
            // Emoji change
            else if (delta_type == "change_thread_icon")
            {
                var new_emoji = delta["untypedData"]["thread_icon"];
                var thread = getThreadIdAndThreadType(metadata);
                this.onEmojiChange(
                    mid: mid,
                    author_id: author_id,
                    new_emoji: new_emoji,
                    thread_id: thread.Item1,
                    thread_type: thread.Item2,
                    ts: ts,
                    metadata: metadata,
                    msg: m
                );
            }
            // Thread title change
            else if (delta_class == "ThreadName")
            {
                var new_title = delta["name"]?.Value<string>();
                var thread = getThreadIdAndThreadType(metadata);
                this.onTitleChange(
                    mid: mid,
                    author_id: author_id,
                    new_title: new_title,
                    thread_id: thread.Item1,
                    thread_type: thread.Item2,
                    ts: ts,
                    metadata: metadata,
                    msg: m
                );
            }
            // Forced fetch
            else if (delta_class == "ForcedFetch")
            {
                mid = delta["messageId"]?.Value<string>();
                if (mid == null)
                    this.onUnknownMesssageType(msg: m);
                else
                {
                    var thread_id = delta["threadKey"]?["threadFbId"]?.Value<string>();
                    var fetch_info = await this._forcedFetch(thread_id, mid);
                    var fetch_data = fetch_info["message"];
                    author_id = fetch_data["message_sender"]["id"]?.Value<string>();
                    ts = int.Parse(fetch_data["timestamp_precise"]?.Value<string>());
                    if (fetch_data["__typename"]?.Value<string>() == "ThreadImageMessage")
                    {
                        // Thread image change
                        var image_metadata = fetch_data["image_with_metadata"];
                        var image_id = image_metadata != null ? (int?)int.Parse(image_metadata["legacy_attachment_id"]?.Value<string>()) : null;
                        this.onImageChange(
                            mid: mid,
                            author_id: author_id,
                            new_image: image_id,
                            thread_id: thread_id,
                            thread_type: ThreadType.GROUP,
                            ts: ts,
                            msg: m
                        );
                    }
                }
            }
            // Nickname change
            else if (delta_type == "change_thread_nickname")
            {
                var changed_for = delta["untypedData"]["participant_id"]?.Value<string>();
                var new_nickname = delta["untypedData"]["nickname"];
                var thread = getThreadIdAndThreadType(metadata);
                this.onNicknameChange(
                    mid: mid,
                    author_id: author_id,
                    changed_for: changed_for,
                    new_nickname: new_nickname,
                    thread_id: thread.Item1,
                    thread_type: thread.Item2,
                    ts: ts,
                    metadata: metadata,
                    msg: m
                );
            }
            // Admin added or removed in a group thread
            else if (delta_type == "change_thread_admins")
            {
                var thread = getThreadIdAndThreadType(metadata);
                var target_id = delta["untypedData"]["TARGET_ID"]?.Value<string>();
                var admin_event = delta["untypedData"]["ADMIN_EVENT"]?.Value<string>();
                if (admin_event == "add_admin")
                    this.onAdminAdded(
                        mid: mid,
                        added_id: target_id,
                        author_id: author_id,
                        thread_id: thread.Item1,
                        thread_type: thread.Item2,
                        ts: ts,
                        msg: m
                    );
                else if (admin_event == "remove_admin")
                    this.onAdminRemoved(
                    mid: mid,
                    removed_id: target_id,
                    author_id: author_id,
                    thread_id: thread.Item1,
                    thread_type: thread.Item2,
                    ts: ts,
                    msg: m
                );
            }
            // Group approval mode change
            else if (delta_type == "change_thread_approval_mode")
            {
                var thread = getThreadIdAndThreadType(metadata);
                var approval_mode = int.Parse(delta["untypedData"]["APPROVAL_MODE"]?.Value<string>()) != 0 ? true : false;
                this.onApprovalModeChange(
                    mid: mid,
                    approval_mode: approval_mode,
                    author_id: author_id,
                    thread_id: thread.Item1,
                    thread_type: thread.Item2,
                    ts: ts,
                    msg: m
                );
            }
            // Message delivered
            else if (delta_class == "DeliveryReceipt")
            {
                var message_ids = delta["messageIds"];
                var delivered_for =
                    delta["actorFbId"]?.Value<string>() ?? delta["threadKey"]["otherUserFbId"]?.Value<string>();
                ts = int.Parse(delta["deliveredWatermarkTimestampMs"]?.Value<string>());
                var thread = getThreadIdAndThreadType(delta);
                this.onMessageDelivered(
                    msg_ids: message_ids,
                    delivered_for: delivered_for,
                    thread_id: thread.Item1,
                    thread_type: thread.Item2,
                    ts: ts,
                    metadata: metadata,
                    msg: m
                );
            }
            // Message seen
            else if (delta_class == "ReadReceipt")
            {
                var seen_by = delta["actorFbId"]?.Value<string>() ?? delta["threadKey"]["otherUserFbId"]?.Value<string>());
                var seen_ts = int.Parse(delta["actionTimestampMs"]?.Value<string>());
                var delivered_ts = int.Parse(delta["watermarkTimestampMs"]?.Value<string>());
                var thread = getThreadIdAndThreadType(delta);
                this.onMessageSeen(
                    seen_by: seen_by,
                    thread_id: thread.Item1,
                    thread_type: thread.Item2,
                    seen_ts: seen_ts,
                    ts: delivered_ts,
                    metadata: metadata,
                    msg: m
                );
            }
            // Messages marked as seen
            else if (delta_class == "MarkRead")
            {
                var seen_ts = int.Parse(
                delta["actionTimestampMs"]?.Value<string>() ?? delta["actionTimestamp"]?.Value<string>()
                );
                var delivered_ts = int.Parse(
                    delta["watermarkTimestampMs"]?.Value<string>() ?? delta["watermarkTimestamp"]?.Value<string>()
                );

                var threads = new List<Tuple<string, ThreadType>>();
                if (delta["folders"] == null)
                {
                    threads = delta["threadKeys"].Select(thr => getThreadIdAndThreadType(
                        new JObject(new JProperty("threadKey", thr)))).ToList();
                }

                // var thread = getThreadIdAndThreadType(delta);
                this.onMarkedSeen(
                    threads: threads, seen_ts: seen_ts, ts: delivered_ts, metadata: delta, msg: m
                );
            }
            // Game played
            else if (delta_type == "instant_game_update")
            {
                var game_id = delta["untypedData"]["game_id"];
                var game_name = delta["untypedData"]["game_name"];
                var score = delta["untypedData"]?["score"] != null ? (int?)int.Parse(delta["untypedData"]?["score"]?.Value<string>()) : null;
                var leaderboard = delta["untypedData"]?["leaderboard"] != null ? JToken.Parse(delta["untypedData"]?["leaderboard"]?.Value<string>())["scores"] : null;
                var thread = getThreadIdAndThreadType(metadata);
                this.onGamePlayed(
                    mid: mid,
                    author_id: author_id,
                    game_id: game_id,
                    game_name: game_name,
                    score: score,
                    leaderboard: leaderboard,
                    thread_id: thread.Item1,
                    thread_type: thread.Item2,
                    ts: ts,
                    metadata: metadata,
                    msg: m
                );
            }
        }
        #endregion
    }
}
