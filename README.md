<img src="https://github.com/gave92/fbchat-sharp/blob/master/fbchat-icon.png?raw=true" width="150" />

# fbchat-sharp: Facebook Messenger client library for C#

![logo](https://img.shields.io/badge/license-BSD-blue.svg)&nbsp;[![donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.me/gave92)

Facebook ([Messenger](https://www.messenger.com/)) client library for C#. This is a porting from the excellent [fbchat](https://github.com/carpedm20/fbchat) library for Python.

**No XMPP or API key is needed**. Just use your email and password.

## Installation

$ Install-Package Gave.Libs.Fbchat

## Key features
* **Powerful** (not limited to the facebook chatbot api)
* **Portable** (designed as a PCL - supporting .NET Standard 1.2)

The key difference from other C# messenger libraries is that *fbchat-sharp* does not use the chatbot api. The library is able to read and send chat messages to any of the user contacts.

## Quick guide

The simple example will login to messenger and get the last 20 messages from a user friend.

#### 1. Implement the abstract MessengerClient class

```cs
// The 4 abstract methods allow to load and save an active session to avoid logging in every time and to provide the 2FA code if requested
// In this example the mothods do nothing
public class FBClient : MessengerClient
{
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
```

#### 2. Instantiate FBClient class and login user

```cs
using fbchat_sharp.API;
...
// Instantiate FBClient
FBClient client = new FBClient();
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
```

#### 3. After login, you can get user's thread list and messages.

```cs
// Get user's last 10 threads
List<FB_Thread> threads = await client.FetchThreadList(limit: 10);
// Get user's last 20 messages in a thread
List<FB_Message> messages = await client.FetchThreadMessages(threads.First().uid);
```

## Supported platforms

fbchat-sharp has been created as a PCL targeting .NET Standard 1.2 that supports a wide range of platforms. The list includes but is not limited to:

* .NET Core 1.0
* .NET Framework 4.5.1
* Universal Windows Platform
* Windows 8.1
* Windows Phone 8.1
* Xamarin.Android
* Xamarin.iOS

© Copyright 2017 - 2018 by Marco Gavelli
