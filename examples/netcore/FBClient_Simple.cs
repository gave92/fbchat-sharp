using fbchat_sharp.API;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace examples
{
    public class FBClient_Simple : MessengerClient
    {
        public FBClient_Simple()
        {
            this.Set2FACallback(get2FACode);
        }

        private async Task<string> get2FACode()
        {
            Console.WriteLine("Insert 2FA code:");
            return Console.ReadLine();
        }

        protected override async Task DeleteCookiesAsync()
        {
            await Task.Yield();
        }      

        protected override async Task<List<Cookie>> ReadCookiesFromDiskAsync()
        {
            await Task.Yield();
            return null;
        }

        protected override async Task WriteCookiesToDiskAsync(List<Cookie> cookieJar)
        {
            await Task.Yield();
        }
    }
}
