//"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csi.exe"

#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\AngleSharp.0.9.11\lib\net45\AngleSharp.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\MQTTnet.3.0.8\lib\net461\MQTTnet.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\Newtonsoft.Json.11.0.2\lib\net45\Newtonsoft.Json.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\System.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Collections.Specialized.4.3.0\lib\net46\System.Collections.Specialized.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\System.Data.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Net.NameResolution.4.3.0\lib\net46\System.Net.NameResolution.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Net.Security.4.3.2\lib\net46\System.Net.Security.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Net.Sockets.4.3.0\lib\net46\System.Net.Sockets.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Net.WebSockets.4.3.0\lib\net46\System.Net.WebSockets.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Net.WebSockets.Client.4.3.2\lib\net46\System.Net.WebSockets.Client.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Security.Cryptography.Algorithms.4.3.0\lib\net461\System.Security.Cryptography.Algorithms.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Security.Cryptography.Encoding.4.3.0\lib\net46\System.Security.Cryptography.Encoding.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Security.Cryptography.Primitives.4.3.0\lib\net46\System.Security.Cryptography.Primitives.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\packages\System.Security.Cryptography.X509Certificates.4.3.0\lib\net461\System.Security.Cryptography.X509Certificates.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\System.Xml.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\Microsoft.CSharp.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\System.Core.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\System.Xml.Linq.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\System.Data.DataSetExtensions.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\System.Net.Http.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\System.Xaml.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\WindowsBase.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\PresentationCore.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\PresentationFramework.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\fbchat-sharp\bin\Debug\fbchat-sharp.dll"
#r "C:\Users\Marco\source\repos\fbchat-sharp\examples\wpfapp\bin\Debug\wpfapp.exe"

using System.Threading.Tasks;
using System.Collections.Async;
using fbchat_sharp.API;
using wpfapp;

wpfapp.Helpers.WtfWpf.UnsetRestrictedHeaders();

var client = new FBClient_Wpf();
await client.TryLogin()
await client.isLoggedIn()
var uid = client.GetUserUid();
await client.fetchProfile()
await client.fetchAllUsers()
var threads = await client.fetchThreadList();
await client.fetchAllUsersFromThreads(threads)
await client.searchForUsers("Gavelli")
await client.StartListening(markAlive: true)
var msg = client.searchForMessages("test message",thread_id: threads[0].uid);
await msg.Select(mid => mid.text).ToListAsync()
await client.fetchUserInfo(new List<string>(){uid})
await client.fetchThreadMessages(threads[0].uid)
await client.fetchUnread()
await client.fetchUnseen()
await client.fetchMessageInfo((await msg.FirstAsync()).uid,threads[0].uid)
await client.getPhoneNumbers()
await client.getEmails()
await client.sendMessage("Message test", thread_id: uid, thread_type: ThreadType.USER)
await client.sendRemoteImage(@"https://freeaddon.com/wp-content/uploads/2018/12/cat-memes-25.jpg", thread_id: uid, thread_type: ThreadType.USER);
await client.wave(thread_id: uid, thread_type: ThreadType.USER)
await client.markAsSeen()
client.StopListening()
