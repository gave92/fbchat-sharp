using fbchat_sharp.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace examples
{
    class Basic_Usage
    {
        public static async Task Run(string email, string password)
        {
            // Instantiate FBClient
            FBClient_Simple client = new FBClient_Simple();

            // Login with username and password
            var logged_in = await client.DoLogin(email, password);

            // Check login was successful
            if (logged_in)
            {
                // Send a message to myself
                var msg_uid = await client.SendMessage("Hi me!", thread_id: client.GetUserUid());
                
                if (msg_uid != null)
                {
                    Console.WriteLine("Message sent: {0}", msg_uid);
                }

                // Do logout
                await client.DoLogout();
            }
            else
            {
                Console.WriteLine("Error logging in");
            }            
        }
    }
}
