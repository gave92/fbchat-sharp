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
                // client.StartListening();

                // Send a message to myself
                //var msg_uid = await client.SendMessage("Message test", thread_id: client.GetUserUid());

                //if (msg_uid != null)
                //{
                //    Console.WriteLine("Message sent: {0}", msg_uid);
                //}

                // Fetch latest threads
                var threads = await client.FetchThreadList();
                threads.ForEach(v => Console.WriteLine(v));

                // Fetch own profile
                var self = await client.FetchProfile();
                Console.WriteLine(self);

                // Fetch users I'm chatting with
                var users = await client.FetchAllUsers();
                users.ForEach(v => Console.WriteLine(v));

                // Find user by name/id
                var search = await client.SearchUsers("Marco", 2);
                search.ForEach(v => Console.WriteLine(v));

                // Fetch latest messages
                var messages = await client.FetchThreadMessages(threads.FirstOrDefault()?.uid, 5);
                messages.ForEach(v => Console.WriteLine(v));

                // client.StopListening();

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
