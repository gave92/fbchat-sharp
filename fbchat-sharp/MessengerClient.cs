using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async Task TryLogin()
        {
            var session_cookies = await this.ReadCookiesFromDiskAsync();
            await base.fromSession(session_cookies);
        }

        /// <summary>
        /// Tries to login using provided email and password.
        /// </summary>
        /// <param name="email">User facebook email</param>
        /// <param name="password">User facebook password</param>
        public async Task DoLogin(string email, string password)
        {
            await this.login(email, password);
            await this.WriteCookiesAsync();
        }

        /// <summary>
        /// Deletes session cookies calling DeleteCookiesAsync and logs out the client
        /// </summary>
        public async Task DoLogout()
        {
            await this.DeleteCookiesAsync();
            await base.logout();
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
            this._cancellationTokenSource = new CancellationTokenSource();
            if (await base.startListening(_cancellationTokenSource, markAlive))
            {
                await base.onListening();
                // Store this references as a private member, call Cancel() on it if UI wants to stop            
                new Task(async () => await Listen(_cancellationTokenSource.Token), _cancellationTokenSource.Token, TaskCreationOptions.LongRunning).Start();
            }
        }

        private async Task Listen(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!await base.doOneListen(token))
                    break;
                token.WaitHandle.WaitOne((int)(1 * 1000));
            }

            await base.stopListening();
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
