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
            await Task.Yield();
            Console.WriteLine("Insert 2FA code:");
            return Console.ReadLine();
        }

        protected override async Task OnEvent(FB_Event ev)
        {
            switch (ev)
            {
                case FB_MessageEvent t1:
                    Console.WriteLine(string.Format("Got new message from {0}: {1}", t1.author, t1.message));
                    break;
                default:
                    Console.WriteLine(string.Format("Something happened: {0}", ev.ToString()));
                    break;
            }
            await Task.Yield();
        }

        protected override async Task DeleteCookiesAsync()
        {
            await Task.Yield();
        }      

        protected override async Task<Dictionary<string, List<Cookie>>> ReadCookiesFromDiskAsync()
        {
            await Task.Yield();
            return null;
        }

        protected override async Task WriteCookiesToDiskAsync(Dictionary<string, List<Cookie>> cookieJar)
        {
            await Task.Yield();
        }
    }
}
