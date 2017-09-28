using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace fbchat_sharp.API
{
    public class Thread
    {
        // The unique identifier of the thread. Can be used a `thread_id`. See :ref:`intro_threads` for more info
        public string uid { get; set; }
        // Specifies the type of thread. Can be used a `thread_type`. See :ref:`intro_threads` for more info
        public ThreadType type = ThreadType.USER;
        // The thread"s picture
        public string photo { get; set; }
        // The name of the thread
        public string name { get; set; }
        // Timestamp of last message
        public string last_message_timestamp { get; set; }
        // Number of messages in the thread
        public int message_count { get; set; }        

        public Thread(ThreadType _type, string uid, string photo = null, string name = null, string last_message_timestamp = null, int message_count = 0)
        {
            // Represents a Facebook thread
            this.uid = uid.ToString();
            this.type = _type;
            this.photo = photo;
            this.name = name;
            this.last_message_timestamp = last_message_timestamp;
            this.message_count = message_count;
        }

        public override string ToString()
        {
            return this.__unicode__();
        }

        private string __unicode__()
        {
            return string.Format("<{0} {1} ({2})>", this.type.ToString(), this.name, this.uid);
        }
    }

    public class User : Thread
    {
        // The profile url
        public string url { get; set; }
        // The users first name
        public string first_name { get; set; }
        // The users last name
        public string last_name { get; set; }
        // Whether the user and the client are friends
        public bool is_friend = false;
        // The user"s gender
        public string gender = "";
        // From 0 to 1. How close the client is to the user
        public float affinity = 0;
        // The user"s nickname
        public string nickname { get; set; }
        // The clients nickname, as seen by the user
        public string own_nickname = "";
        // A :class:`ThreadColor`. The message color
        public string color = null;
        // The default emoji
        public string emoji = "";

        public User(string uid, string photo = null, string name = null, int message_count = 0, string url = null, string first_name = null, string last_name = null, bool is_friend = false, string gender = null, float affinity = 0, string nickname = null, string own_nickname = null, string color = null, string emoji = null) :
            base(ThreadType.USER, uid, photo, name, message_count: message_count)
        {
            // Represents a Facebook user. Inherits `Thread`

            this.url = url;
            this.first_name = first_name;
            this.last_name = last_name;
            this.is_friend = is_friend;
            this.gender = gender;
            this.affinity = affinity;
            this.nickname = nickname;
            this.own_nickname = own_nickname;
            this.color = color;
            this.emoji = emoji;
        }
    }

    public class FGroup : Thread
    {
        // Unique list (set) of the group thread"s participant user IDs
        HashSet<string> participants = new HashSet<string>();
        // Dict, containing user nicknames mapped to their IDs
        Dictionary<string, string> nicknames = new Dictionary<string, string>();
        // A :class:`ThreadColor`. The groups"s message color
        string color = null;
        // The groups"s default emoji
        string emoji = "";

        public FGroup(string uid, string photo = null, string name = null, int message_count = 0, HashSet <string> participants = null, Dictionary<string, string> nicknames = null, string color = null, string emoji = null) :
            base(ThreadType.GROUP, uid, photo, name, message_count: message_count)
        {
            // Represents a Facebook group. Inherits `Thread`        
            this.participants = participants;
            this.nicknames = nicknames;
            this.color = color;
            this.emoji = emoji;
        }
    }

    public class FPage : Thread
    {
        // The page"s custom url
        string url = "";
        // The name of the page"s location city
        string city = "";
        // Amount of likes the page has
        int likes = 0;
        // Some extra information about the page
        string sub_title = "";
        // The page"s category
        string category = "";

        public FPage(string uid, string photo = null, string name = null, int message_count = 0, string url = null, string city = null, int likes = 0, string sub_title = null, string category = null) :
            base(ThreadType.PAGE, uid, photo, name, message_count: message_count)
        {
            // Represents a Facebook page. Inherits `Thread`
            this.url = url;
            this.city = city;
            this.likes = likes;
            this.sub_title = sub_title;
            this.category = category;
        }
    }

    public class Message
    {
        // The message ID
        public string uid = "";
        // ID of the sender
        public string author = "";
        // Timestamp of when the message was sent
        public string timestamp = "";
        // Whether the message is read
        public bool is_read = false;
        // A list of message reactions
        public List<string> reactions = new List<string>();
        // The actual message
        public string text { get; set; }
        // A list of :class:`Mention` objects
        public List<Mention> mentions = new List<Mention>();
        // An ID of a sent sticker
        public string sticker = "";
        // A list of attachments
        public List<string> attachments = new List<string>();
        // An extensible attachment, e.g. share object
        public Dictionary<string, JToken> extensible_attachment = new Dictionary<string, JToken>();
        // The message was sent from me
        public bool is_from_me { get; set; }

        public Message(string uid, string author = null, string timestamp = null, bool is_read = false, List<string> reactions = null, string text = null, List<Mention> mentions = null, string sticker = null, List<string> attachments = null, Dictionary<string, JToken> extensible_attachment = null)
        {
            this.uid = uid;
            this.author = author;
            this.timestamp = timestamp;
            this.is_read = is_read;
            this.reactions = reactions;
            this.text = text;
            this.mentions = mentions;
            this.sticker = sticker;
            this.attachments = attachments;
            this.extensible_attachment = extensible_attachment;
        }
    }

    public class Mention
    {
        // The user ID the mention is pointing at
        string user_id = "";
        // The character where the mention starts
        int offset = 0;
        // The length of the mention
        int length = 0;

        public Mention(string user_id, int offset = 0, int length = 10)
        {
            // Represents a @mention
            this.user_id = user_id;
            this.offset = offset;
            this.length = length;
        }
    }

    public enum ThreadType
    {
        // Used to specify what type of Facebook thread is being used. See :ref:`intro_threads` for more info
        USER = 1,
        GROUP = 2,
        PAGE = 3,
    }

    public enum TypingStatus
    {
        // Used to specify whether the user is typing or has stopped typing
        STOPPED = 0,
        TYPING = 1,
    }

    public class EmojiSize
    {
        // Used to specify the size of a sent emoji
        public static readonly string LARGE = "369239383222810";
        public static readonly string MEDIUM = "369239343222814";
        public static readonly string SMALL = "369239263222822";
    }

    public class ThreadColor
    {
        // Used to specify a thread colors
        public static readonly string MESSENGER_BLUE = "";
        public static readonly string VIKING = "#44bec7";
        public static readonly string GOLDEN_POPPY = "#ffc300";
        public static readonly string RADICAL_RED = "#fa3c4c";
        public static readonly string SHOCKING = "#d696bb";
        public static readonly string PICTON_BLUE = "#6699cc";
        public static readonly string FREE_SPEECH_GREEN = "#13cf13";
        public static readonly string PUMPKIN = "#ff7e29";
        public static readonly string LIGHT_CORAL = "#e68585";
        public static readonly string MEDIUM_SLATE_BLUE = "#7646ff";
        public static readonly string DEEP_SKY_BLUE = "#20cef5";
        public static readonly string FERN = "#67b868";
        public static readonly string CAMEO = "#d4a88c";
        public static readonly string BRILLIANT_ROSE = "#ff5ca1";
        public static readonly string BILOBA_FLOWER = "#a695c7";
    }

    public class MessageReaction
    {
        // Used to specify a message reaction
        public static readonly string LOVE = "😍";
        public static readonly string SMILE = "😆";
        public static readonly string WOW = "😮";
        public static readonly string SAD = "😢";
        public static readonly string ANGRY = "😠";
        public static readonly string YES = "👍";
        public static readonly string NO = "👎";
    }

    public class Constants
    {
        public static readonly Dictionary<string, string> LIKES = new Dictionary<string, string>() {
            { "large", EmojiSize.LARGE},
            { "medium", EmojiSize.MEDIUM},
            { "small", EmojiSize.SMALL},
            { "l", EmojiSize.LARGE},
            { "m", EmojiSize.MEDIUM},
            { "s", EmojiSize.SMALL }
        };

        public static readonly Dictionary<string, Tuple<string, string>> MessageReactionFix = new Dictionary<string, Tuple<string, string>>() {
            { "😍", new Tuple<string, string>("0001f60d", "%F0%9F%98%8D")},
            { "😆", new Tuple<string, string>("0001f606", "%F0%9F%98%86")},
            { "😮", new Tuple<string, string>("0001f62e", "%F0%9F%98%AE")},
            { "😢", new Tuple<string, string>("0001f622", "%F0%9F%98%A2")},
            { "😠", new Tuple<string, string>("0001f620", "%F0%9F%98%A0")},
            { "👍", new Tuple<string, string>("0001f44d", "%F0%9F%91%8D")},
            { "👎", new Tuple<string, string>("0001f44e", "%F0%9F%91%8E")}
        };
    }
}
