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
    public abstract class MessengerClient : Client
    {
        /*
         * METHODS
         */
        public async void TryLogin()
        {
            try
            {
                var session_cookies = await this.ReadCookiesFromDiskAsync();
                await base.tryLogin(session_cookies);
            }
            catch (Exception ex)
            {
                this.Log(ex.ToString());
                base.OnLoginEvent(new LoginEventArgs(LoginStatus.LOGIN_FAILED));
            }
        }

        public async void DoLogin(string email, string password, int max_tries = 5)
        {
            try
            {
                await this.doLogin(email, password, max_tries);
            }
            catch (Exception ex)
            {
                this.Log(ex.ToString());
                base.OnLoginEvent(new LoginEventArgs(LoginStatus.LOGIN_FAILED));
            }
        }

        public async void DoLogout()
        {
            try
            {
                await this.DeleteCookiesAsync();
                await base.doLogout();
            }
            catch (Exception ex)
            {
                this.Log(ex.ToString());
                base.OnLoginEvent(new LoginEventArgs(LoginStatus.LOGOUT_FAILED));
            }
        }

        public async void StartListening()
        {
            await base.startListening();
            base.onListening();

            // Store this references as a private member, call Cancel() on it if UI wants to stop
            this._cancellationTokenSource = new CancellationTokenSource();
            new Task(async () => await Listen(_cancellationTokenSource.Token), _cancellationTokenSource.Token, TaskCreationOptions.LongRunning).Start();
        }

        protected async Task Listen(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await base.doOneListen(false);
                token.WaitHandle.WaitOne((int)(1 * 1000));
            }

            base.stopListening();
        }

        public void StopListening()
        {
            this._cancellationTokenSource.Cancel();
        }

        public async Task WriteCookies()
        {
            var session_cookies = this.getSession(ReqUrl.BASE).ToList();
            await this.WriteCookiesToDiskAsync(session_cookies);
        }

        public async Task<List<FB_Message>> FetchThreadMessages(string thread_id = null, int limit = 20, string before = null)
        {
            return await this.SafeWrapper<List<FB_Message>>(async () => await base.fetchThreadMessages(thread_id, limit, before));
        }

        public async Task<FB_User> FetchProfile()
        {
            return await this.SafeWrapper<FB_User>(async () => (await base.fetchUserInfo(new[] { base.GetUserUid() })).Single().Value);
            // return await this.SafeWrapper<Dictionary<string, object>>(async () => (await base.fetchInfo(new[] { base.uid })).Single().Value);
        }

        public async Task<List<FB_User>> FetchAllUsers()
        {
            return await this.SafeWrapper<List<FB_User>>(async () => await base.fetchAllUsers());
        }

        public async Task<List<FB_User>> SearchUsers(string name, int limit = 1)
        {
            return await this.SafeWrapper<List<FB_User>>(async () => await base.searchForUsers(name, limit));
        }

        public async Task<List<FB_Thread>> FetchThreadList(int offset = 0, int limit = 20)
        {
            return await this.SafeWrapper<List<FB_Thread>>(async () => await base.fetchThreadList(offset, limit));
        }

        public async Task<string> SendMessage(string message, string thread_id = null, ThreadType thread_type = ThreadType.USER)
        {
            return await this.SafeWrapper<string>(async () => await base.sendMessage(message, thread_id, thread_type));
        }

        protected void Log(string message, [CallerMemberName] string method = null)
        {
            Debug.WriteLine(string.Format("{0}: {1}", message, method));
        }

        public abstract Task DeleteCookiesAsync();

        public abstract Task WriteCookiesToDiskAsync(List<Cookie> cookieJar);

        public abstract Task<List<Cookie>> ReadCookiesFromDiskAsync();

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
