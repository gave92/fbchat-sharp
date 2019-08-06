using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Facebook Client wrapper class. Library users should use this class
    /// </summary>
    public abstract class MessengerClient : Client
    {
        /*
         * METHODS
         */
        /// <summary>
        /// Loads sessions cookies calling ReadCookiesFromDiskAsync and tries to login.
        /// </summary>
        /// <returns>Returns true if login was successful</returns>
        public async Task<bool> TryLogin()
        {
            try
            {
                var session_cookies = await this.ReadCookiesFromDiskAsync();
                await base.fromSession(session_cookies);
                return true;
            }
            catch (Exception ex)
            {
                this.Log(ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Tries to login using provided email and password.
        /// </summary>
        /// <param name="email">User facebook email</param>
        /// <param name="password">User facebook password</param>
        /// <param name="max_tries">Max. number of retries</param>
        /// <returns>Returns true if login was successful</returns>
        public async Task<bool> DoLogin(string email, string password, int max_tries = 5)
        {
            try
            {
                await this.login(email, password, max_tries);
                await this.WriteCookiesAsync();
                return true;
            }
            catch (Exception ex)
            {
                this.Log(ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Deletes session cookies calling DeleteCookiesAsync and logs out the client
        /// </summary>
        /// <returns>Returns true if logout was successful</returns>
        public async Task<bool> DoLogout()
        {
            try
            {
                await this.DeleteCookiesAsync();
                await base.logout();
                return true;
            }
            catch (Exception ex)
            {
                this.Log(ex.ToString());
                return false;
            }
        }

        /// <returns>Returns the user id or null if not logged in</returns>
        public string GetUserUid()
        {
            return base._uid;
        }

        /// <summary>
        /// Starts listening for messenger updates (e.g. a new message) on a background thread
        /// </summary>
        public async Task StartListening(bool markAlive = true)
        {
            base.startListening();
            await base.onListening();

            // Store this references as a private member, call Cancel() on it if UI wants to stop
            this._cancellationTokenSource = new CancellationTokenSource();
            new Task(async () => await Listen(_cancellationTokenSource.Token, markAlive), _cancellationTokenSource.Token, TaskCreationOptions.LongRunning).Start();
        }

        private async Task Listen(CancellationToken token, bool markAlive = false)
        {
            base.setActiveStatus(markAlive);

            while (!token.IsCancellationRequested)
            {
                if (!await base.doOneListen(markAlive))
                    break;
                token.WaitHandle.WaitOne((int)(1 * 1000));
            }

            base.stopListening();
        }

        /// <summary>
        /// Stops listening thread
        /// </summary>
        public void StopListening()
        {
            this._cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Gets the client's session cookies and saves them to disk calling WriteCookiesToDiskAsync
        /// </summary>
        public async Task WriteCookiesAsync()
        {
            var session_cookies = this.getSession();
            await this.WriteCookiesToDiskAsync(session_cookies);
        }

        /// <summary>
        /// Get the last messages in a thread
        /// </summary>
        /// <param name="thread_id">User / Group ID from which to retrieve the messages</param>
        /// <param name="limit">Max.number of messages to retrieve</param>
        /// <param name="before">A unix timestamp, indicating from which point to retrieve messages</param>
        public async Task<List<FB_Message>> FetchThreadMessages(string thread_id = null, int limit = 20, string before = null)
        {
            return await this.SafeWrapper<List<FB_Message>>(async () => await base.fetchThreadMessages(thread_id, limit, before));
        }

        /// <summary>
        /// Get logged user's info
        /// </summary>
        public async Task<FB_User> FetchProfile()
        {
            return await this.SafeWrapper<FB_User>(async () => (await base.fetchUserInfo(new List<string>() { base._uid })).Single().Value);
        }

        /// <summary>
        /// Gets all users the client is currently chatting with
        /// </summary>
        public async Task<List<FB_User>> FetchAllUsers()
        {
            return await this.SafeWrapper<List<FB_User>>(async () => await base.fetchAllUsers());
        }

        /// <summary>
        /// Find and get user by his/her name
        /// </summary>
        /// <param name="name">Name of the user</param>
        /// <param name="limit">The max. amount of users to fetch</param>
        public async Task<List<FB_User>> SearchUsers(string name, int limit = 1)
        {
            return await this.SafeWrapper<List<FB_User>>(async () => await base.searchForUsers(name, limit));
        }

        /// <summary>
        /// Get thread list of your facebook account
        /// </summary>
        /// <param name="limit">Max.number of threads to retrieve. Capped at 20</param>
        /// <param name="thread_location">models.ThreadLocation: INBOX, PENDING, ARCHIVED or OTHER</param>
        /// <param name="before">A unix timestamp, indicating from which point to retrieve messages</param>
        public async Task<List<FB_Thread>> FetchThreadList(int limit = 20, string thread_location = "inbox", string before = null)
        {
            return await this.SafeWrapper<List<FB_Thread>>(async () => await base.fetchThreadList(limit, thread_location, before));
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="thread_id">User / Group ID to send to</param>
        /// <param name="thread_type">ThreadType enum</param>
        /// <returns>Message ID of the sent message</returns>
        public async Task<string> SendMessage(string message, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            return await this.SafeWrapper<string>(async () => await base.send(new FB_Message() { text = message }, thread_id, thread_type));
        }

        /// <summary>
        /// Logs a message to console
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="method">Name of the caller method</param>
        protected void Log(string message, [CallerMemberName] string method = null)
        {
            Debug.WriteLine(string.Format("{0}: {1}", message, method));
        }

        /// <summary>
        /// Use this to set the callback for providing a 2FA code
        /// </summary>
        public void Set2FACallback(Func<Task<string>> get2FACode)
        {
            this.get2FACode = get2FACode;
        }

        private Func<Task<string>> get2FACode;

        /// <summary>
        /// Called when a 2FA code is requested
        /// </summary>
        protected override async Task<string> on2FACode()
        {
            if (get2FACode == null)
            {
                this.Log("2FA code callback is not set. Use Set2FACallback().");
                return null;
            }
            return await get2FACode();
        }

        /// <summary>
        /// How to delete saved cookies from disk
        /// </summary>
        protected abstract Task DeleteCookiesAsync();

        /// <summary>
        /// How to save a list of cookies to disk
        /// </summary>
        /// <param name="cookieJar">List of session cookies</param>
        protected abstract Task WriteCookiesToDiskAsync(Dictionary<string, List<Cookie>> cookieJar);

        /// <summary>
        /// How to load a list of saved cookies
        /// </summary>
        protected abstract Task<Dictionary<string, List<Cookie>>> ReadCookiesFromDiskAsync();

        #region PRIVATE
        private CancellationTokenSource _cancellationTokenSource;

        private async Task<T> SafeWrapper<T>(Func<Task<T>> action, [CallerMemberName] string method = null)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                this.Log(ex.ToString(), method);
                return default(T);
            }
        }
        #endregion
    }
}
