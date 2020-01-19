using fbchat_sharp.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace examples
{
    class Basic_Usage
    {
        private static readonly AutoResetEvent _closing = new AutoResetEvent(false);

        public static async Task Run()
        {
            // Instantiate FBClient
            MessengerClient client = new FBClient_Cookies();

            // Try logging in from saved session
            var session = await client.TryLogin();
            if (session != null)
            {
                // Read email and pw from console
                Console.WriteLine("Insert Facebook email:");
                var email = Console.ReadLine();
                Console.WriteLine("Insert Facebook password:");
                var password = Console.ReadLine();

                // Login with username and password
                session = await client.DoLogin(email, password);
            }

            // Check login was successful
            if (session != null)
            {
                // Start listening for new messages
                await client.StartListening();

                // Fetch latest threads
                var threads = await client.fetchThreadList();
                threads.ForEach(v => Console.WriteLine(v));

                var info = await client.fetchThreadInfo(new List<string> { threads[0].uid });

                // Fetch own profile
                var self = await client.fetchProfile();
                Console.WriteLine(self);

                // Fetch users I'm chatting with
                var users = await client.fetchUsers();
                users.ForEach(v => Console.WriteLine(v));

                // Find user by name/id
                var search = await client.searchUsers("Marco", 2);
                search.ForEach(v => Console.WriteLine(v));

                // Fetch latest messages
                var messages = await threads.FirstOrDefault()?.fetchThreadMessages(5);
                messages.ForEach(v => Console.WriteLine(v));

                // Send a message to myself
                var user = new FB_Thread(session.get_user_id(), session);
                var msg_uid = await user.sendText("Message test");
                if (msg_uid != null)
                {
                    Console.WriteLine("Message sent: {0}", msg_uid);
                }

                // Send an emoji to myself
                await user.sendEmoji("👍", EmojiSize.LARGE);

                // Send a local file to myself
                /*
                using (FileStream stream = File.OpenRead(@"C:\Users\Marco\Documents\a032.pdf"))
                {
                    //await client.sendLocalImage(@"C:\Users\Marco\Pictures\Saved Pictures\opengraph.png", stream, null, client.GetUserUid(), ThreadType.USER);
                    await client.sendLocalFiles(
                        file_paths: new Dictionary<string, Stream>() { { @"C:\Users\Marco\Documents\a032.pdf", stream } },
                        message: null,
                        thread_id: client.GetUserUid(),
                        thread_type: ThreadType.USER);
                }
                */

                // Send a remote image to myself
                await user.sendRemoteImage(@"https://freeaddon.com/wp-content/uploads/2018/12/cat-memes-25.jpg");

                // Stop listening Ctrl+C
                Console.WriteLine("Listening... Press Ctrl+C to exit.");
                Console.CancelKeyPress += new ConsoleCancelEventHandler((s, e) => { e.Cancel = true; _closing.Set(); });
                _closing.WaitOne();
                client.StopListening();

                // Logging out is not required
                // await client.DoLogout();
            }
            else
            {
                Console.WriteLine("Error logging in");
            }
        }
    }
}
