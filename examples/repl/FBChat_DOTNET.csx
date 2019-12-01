//cd C:\Users\Marco\source\repos\fbchat-sharp\examples\repl
//dotnet-script -s C:\Users\Marco\source\repos\fbchat-sharp\fbchat-sharp

#r "nuget: Gave.Libs.FBchat, 0.3.1"
#load "FBClient_Cookies.csx"

using fbchat_sharp.API;
using System.Linq;

var client = new FBClient_Cookies();
await client.TryLogin();
await client.isLoggedIn();
var uid = client.GetUserUid();
await client.fetchProfile();
await client.fetchAllUsers();
var threads = await client.fetchThreadList();
await client.fetchAllUsersFromThreads(threads);
await client.searchForUsers("Gavelli");
await client.StartListening(markAlive: true);
var msg = client.searchForMessages("test message",thread_id: threads[0].uid);
await msg.Select(mid => mid.text).ToListAsync();
await client.fetchUserInfo(new List<string>(){uid});
await client.fetchThreadMessages(threads[0].uid);
await client.fetchUnread();
await client.fetchUnseen();
await client.fetchMessageInfo((await msg.FirstAsync()).uid,threads[0].uid);
await client.getPhoneNumbers();
await client.getEmails();
await client.sendMessage("Message test", thread_id: uid, thread_type: ThreadType.USER);
await client.sendEmoji("👍", EmojiSize.LARGE, thread_id: client.GetUserUid(), thread_type: ThreadType.USER);
await client.sendRemoteImage(@"https://freeaddon.com/wp-content/uploads/2018/12/cat-memes-25.jpg", thread_id: uid, thread_type: ThreadType.USER);
await client.wave(thread_id: uid, thread_type: ThreadType.USER);
await client.markAsSeen();
client.StopListening();
