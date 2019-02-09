using fbchat_sharp.API;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace examples
{
    class Basic_Usage
    {
        public static async Task Run()
        {
            // Instantiate FBClient
            MessengerClient client = new FBClient_Cookies();

            // Try logging in from saved session
            var logged_in = await client.TryLogin();
            if (!logged_in)
            {
                // Read email and pw from console
                Console.WriteLine("Insert Facebook email:");
                var email = Console.ReadLine();
                Console.WriteLine("Insert Facebook password:");
                var password = Console.ReadLine();
                
                // Login with username and password
                logged_in = await client.DoLogin(email, password);
            }

            // Check login was successful
            if (logged_in)
            {
                // client.UpdateEvent += (e,d) => { if (d.EventType == UpdateStatus.NEW_MESSAGE) Console.WriteLine(d.Payload); };
                // client.StartListening();

                // Save session cookies
                await client.WriteCookiesAsync();

                // Fetch latest threads
                var threads = await client.FetchThreadList();
                threads.ForEach(v => Console.WriteLine(v));

                var info = await client.fetchThreadInfo(new string[] { threads[0].uid });

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

                // Send a message to myself
                var msg_uid = await client.SendMessage("Message test", thread_id: client.GetUserUid());

                if (msg_uid != null)
                {
                    Console.WriteLine("Message sent: {0}", msg_uid);
                }

                // Send an image to myself
                // using (FileStream stream = File.OpenRead(@"C:\Users\Marco\Pictures\Saved Pictures\opengraph.png"))
                // {
                // await client.sendLocalImage(@"C:\Users\Marco\Pictures\Saved Pictures\opengraph.png", stream, null, client.GetUserUid(), ThreadType.USER);
                // }

                // await Task.Delay(60 * 1000);
                // client.StopListening();

                // Do logout
                // await client.DoLogout();
            }
            else
            {
                Console.WriteLine("Error logging in");
            }
        }
    }
}
