<img src="https://github.com/gave92/fbchat-sharp/blob/master/fbchat-icon.png?raw=true" width="150" />

# fbchat-sharp: Facebook Messenger client library for C#

![logo](https://img.shields.io/badge/license-BSD-blue.svg)&nbsp;[![donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.me/gave92)&nbsp;[![Build Status](https://ci.appveyor.com/api/projects/status/github/gave92/fbchat-sharp?branch=master&svg=true)](https://ci.appveyor.com/project/gave92/fbchat-sharp)

A powerful and efficient library to interact with Facebook's [Messenger](https://www.messenger.com/), using just your email and password.
This is a porting from the excellent [fbchat](https://github.com/carpedm20/fbchat) library to C#.

This is *not* an official API, Facebook has that [over here](https://developers.facebook.com/docs/messenger-platform) for chat bots. This library differs by using a normal Facebook account instead.

**fbchat-sharp** currently support:

- Sending many types of messages, with files, stickers, mentions, etc.
- Fetching all messages, threads and images in threads.
- Searching for messages and threads.
- Creating groups, setting the group emoji, changing nicknames, creating polls, etc.
- Listening for, an reacting to messages and other events in real-time.

Essentially, everything you need to make an amazing Facebook bot!

#### Version warning
*v2* is currently being developed at the *master* branch and it's highly unstable. If you want to view the old *v1*, go [here](https://github.com/gave92/fbchat-sharp/tree/v1)`.

## Installation

$ Install-Package Gave.Libs.Fbchat

## Quick guide

The simple example will login to messenger and get the last 20 messages from a user friend.

#### 1. Inherit the MessengerClient class

```cs
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using fbchat_sharp.API;
...
// The methods allow to load and save an active session to avoid logging in every time and to provide the 2FA code if requested
// In this example the methods do nothing
public class FBClient : MessengerClient
{
    public FBClient()
    {
        On2FACodeCallback = get2FACode;
    }

    private async Task<string> get2FACode()
    {
        // You need to implement this if your account uses two factor authentication.
        // It should return a valid 2FA code as string.
        return null;
    }

    // This method is called when the client receives an event from Facebook:
    // A new message/reaction was received
    // A picture was posted in a thread
    // A friend connected
    // A friend request was received
    // ...
    protected override async Task OnEvent(FB_Event ev)
    {
        switch (ev)
        {
            case FB_MessageEvent t1:
                // You got a new message!
                Console.WriteLine(string.Format("Got new message from {0}: {1}", t1.author, t1.message));
                break;
            default:
                // Something else happened
                Console.WriteLine(string.Format("Something happened: {0}", ev.ToString()));
                break;
        }
        await Task.Yield();
    }

    /// <summary>
    /// How to delete saved cookies from disk. Called when logging out.
    /// </summary>
    protected override async Task DeleteCookiesAsync()
    {
        // You should always implement this. Facebook complains if you login too often.
    }
    
    /// <summary>
    /// How to load a list of saved cookies
    /// </summary>
    protected override async Task<Dictionary<string, List<Cookie>>> ReadCookiesFromDiskAsync()
    {
        // You should always implement this. Facebook complains if you login too often.
        return null;
    }

    /// <summary>
    /// How to save a list of cookies to disk
    /// </summary>
    protected override async Task WriteCookiesToDiskAsync(Dictionary<string, List<Cookie>> cookieJar)
    {
        // You should always implement this. Facebook complains if you login too often.
    }
}
```

#### 2. Instantiate FBClient class and login user

```cs
using System;
using System.Threading;
using System.Threading.Tasks;
using fbchat_sharp.API;
...
// Instantiate FBClient
FBClient client = new FBClient();
// Login with username and password
var session = await client.DoLogin(email, password);

// Check login was successful
if (session != null)
{
    // Send a message to myself
    var user = new FB_Thread(session.user.uid, session);
    var msg_uid = await user.sendText("Hi me!");                
    if (msg_uid != null)
    {
        Console.WriteLine("Message sent: {0}", msg_uid);
    }

    // Logging out is not recommended. Facebook complains if you login too often.
    // Instead always implement WriteCookiesToDiskAsync() and ReadCookiesFromDiskAsync() client methods.
    // await client.DoLogout();
}
else
{
    Console.WriteLine("Error logging in");
}
```

#### 3. After login, you can get user's thread list and messages.

```cs
// Get user's last 10 threads
List<FB_Thread> threads = await client.fetchThreadList(limit: 10);
// Get user's last 20 messages in a thread
List<FB_Message> messages = await threads.FirstOrDefault()?.fetchMessages(20);
```

## Supported platforms

fbchat-sharp has been created as a PCL targeting .NET Standard 1.3 that supports a wide range of platforms. The list includes but is not limited to:

* .NetStandard 1.3
* .NET Core 1.0
* .NET Framework 4.6
* Universal Windows Platform

© Copyright 2017 by Marco Gavelli
