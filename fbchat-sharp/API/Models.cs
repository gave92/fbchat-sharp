using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Facebook messenger thread class
    /// </summary>
    public class FB_Thread
    {
        /// The unique identifier of the thread. Can be used a `thread_id`. See :ref:`intro_threads` for more info
        public string uid { get; set; }
        /// Specifies the type of thread. Can be used a `thread_type`. See :ref:`intro_threads` for more info
        public ThreadType type = ThreadType.USER;
        /// The thread"s picture
        public string photo { get; set; }
        /// The name of the thread
        public string name { get; set; }
        /// Timestamp of last message
        public string last_message_timestamp { get; set; }
        /// Number of messages in the thread
        public int message_count { get; set; }

        /// <summary>
        /// Represents a Facebook thread
        /// </summary>
        /// <param name="type"></param>
        /// <param name="uid"></param>
        /// <param name="photo"></param>
        /// <param name="name"></param>
        /// <param name="last_message_timestamp"></param>
        /// <param name="message_count"></param>
        public FB_Thread(ThreadType type, string uid, string photo = null, string name = null, string last_message_timestamp = null, int message_count = 0)
        {
            this.uid = uid.ToString();
            this.type = type;
            this.photo = photo;
            this.name = name;
            this.last_message_timestamp = last_message_timestamp;
            this.message_count = message_count;
        }

        /// <returns>Pretty string representation of the thread</returns>
        public override string ToString()
        {
            return this.__unicode__();
        }

        private string __unicode__()
        {
            return string.Format("<{0} {1} ({2})>", this.type.ToString(), this.name, this.uid);
        }
    }

    /// <summary>
    /// Facebook messenger user class
    /// </summary>
    public class FB_User : FB_Thread
    {
        /// The profile url
        public string url { get; set; }
        /// The users first name
        public string first_name { get; set; }
        /// The users last name
        public string last_name { get; set; }
        /// Whether the user and the client are friends
        public bool is_friend = false;
        /// The user"s gender
        public string gender = "";
        /// From 0 to 1. How close the client is to the user
        public float affinity = 0;
        /// The user"s nickname
        public string nickname { get; set; }
        /// The clients nickname, as seen by the user
        public string own_nickname = "";
        /// A :class:`ThreadColor`. The message color
        public string color = null;
        /// The default emoji
        public string emoji = "";

        /// <summary>
        /// Represents a Facebook user. Inherits `Thread`
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="photo"></param>
        /// <param name="name"></param>
        /// <param name="message_count"></param>
        /// <param name="url"></param>
        /// <param name="first_name"></param>
        /// <param name="last_name"></param>
        /// <param name="is_friend"></param>
        /// <param name="gender"></param>
        /// <param name="affinity"></param>
        /// <param name="nickname"></param>
        /// <param name="own_nickname"></param>
        /// <param name="color"></param>
        /// <param name="emoji"></param>
        public FB_User(string uid, string photo = null, string name = null, int message_count = 0, string url = null, string first_name = null, string last_name = null, bool is_friend = false, string gender = null, float affinity = 0, string nickname = null, string own_nickname = null, string color = null, string emoji = null) :
            base(ThreadType.USER, uid, photo, name, message_count: message_count)
        {            
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

    /// <summary>
    /// Facebook messenger group class
    /// </summary>
    public class FB_Group : FB_Thread
    {
        /// Unique list (set) of the group thread"s participant user IDs
        public HashSet<string> participants = new HashSet<string>();
        /// Dict, containing user nicknames mapped to their IDs
        public Dictionary<string, string> nicknames = new Dictionary<string, string>();
        /// A `ThreadColor`. The groups"s message color
        public string color = null;
        /// The groups"s default emoji
        public string emoji = "";

        /// <summary>
        /// Represents a Facebook group. Inherits `Thread`
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="photo"></param>
        /// <param name="name"></param>
        /// <param name="message_count"></param>
        /// <param name="participants"></param>
        /// <param name="nicknames"></param>
        /// <param name="color"></param>
        /// <param name="emoji"></param>
        public FB_Group(string uid, string photo = null, string name = null, int message_count = 0, HashSet <string> participants = null, Dictionary<string, string> nicknames = null, string color = null, string emoji = null) :
            base(ThreadType.GROUP, uid, photo, name, message_count: message_count)
        {            
            this.participants = participants;
            this.nicknames = nicknames;
            this.color = color;
            this.emoji = emoji;
        }
    }

    /// <summary>
    /// Facebook messenger page class
    /// </summary>
    public class FB_Page : FB_Thread
    {
        /// The page's custom url
        public string url = "";
        /// The name of the page"s location city
        public string city = "";
        /// Amount of likes the page has
        public int likes = 0;
        /// Some extra information about the page
        public string sub_title = "";
        /// The page's category
        public string category = "";

        /// <summary>
        /// Represents a Facebook page. Inherits `Thread`
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="photo"></param>
        /// <param name="name"></param>
        /// <param name="message_count"></param>
        /// <param name="url"></param>
        /// <param name="city"></param>
        /// <param name="likes"></param>
        /// <param name="sub_title"></param>
        /// <param name="category"></param>
        public FB_Page(string uid, string photo = null, string name = null, int message_count = 0, string url = null, string city = null, int likes = 0, string sub_title = null, string category = null) :
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

    /// <summary>
    /// Facebook messenger message class
    /// </summary>
    public class FB_Message
    {
        /// The message ID
        public string uid = "";
        /// ID of the sender
        public string author = "";
        /// ID of the thread the message was sent to
        public string thread_id = "";
        /// Timestamp of when the message was sent
        public string timestamp = "";
        /// Whether the message is read
        public bool is_read = false;
        /// A list of message reactions
        public List<string> reactions = new List<string>();
        /// The actual message
        public string text { get; set; }
        /// A list of :class:`Mention` objects
        public List<FB_Mention> mentions = new List<FB_Mention>();
        /// An ID of a sent sticker
        public JObject sticker = new JObject();
        /// A list of attachments
        public JArray attachments = new JArray();
        /// An extensible attachment, e.g. share object
        public Dictionary<string, JToken> extensible_attachment = new Dictionary<string, JToken>();
        /// The message was sent from me (not filled)
        public bool is_from_me { get; set; }

        /// <summary>
        /// Represents a Facebook message
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="author"></param>
        /// <param name="thread_id"></param>
        /// <param name="timestamp"></param>
        /// <param name="is_read"></param>
        /// <param name="reactions"></param>
        /// <param name="text"></param>
        /// <param name="mentions"></param>
        /// <param name="sticker"></param>
        /// <param name="attachments"></param>
        /// <param name="extensible_attachment"></param>
        public FB_Message(string uid, string author = null, string thread_id = null, string timestamp = null, bool is_read = false, List<string> reactions = null, string text = null, List<FB_Mention> mentions = null, JObject sticker = null, JArray attachments = null, Dictionary<string, JToken> extensible_attachment = null)
        {
            this.uid = uid;
            this.author = author;
            this.thread_id = thread_id;
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

    /// <summary>
    /// Facebook messenger mention class
    /// </summary>
    public class FB_Mention
    {
        /// The user ID the mention is pointing at
        public string user_id = "";
        /// The character where the mention starts
        public int offset = 0;
        /// The length of the mention
        public int length = 0;

        /// <summary>
        /// Represents a @mention
        /// </summary>
        /// <param name="user_id"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public FB_Mention(string user_id, int offset = 0, int length = 10)
        {
            this.user_id = user_id;
            this.offset = offset;
            this.length = length;
        }
    }

    /// <summary>
    /// Used to specify what type of Facebook thread is being used
    /// </summary>
    public enum ThreadType
    {
        USER = 1,
        GROUP = 2,
        PAGE = 3,
    }

    /// <summary>
    /// Used to specify whether the user is typing or has stopped typing
    /// </summary>
    public enum TypingStatus
    {
        STOPPED = 0,
        TYPING = 1,
    }

    /// <summary>
    /// Used to specify the size of a sent emoji
    /// </summary>
    public class EmojiSize
    {
        public static readonly string LARGE = "369239383222810";
        public static readonly string MEDIUM = "369239343222814";
        public static readonly string SMALL = "369239263222822";
    }

    /// <summary>
    /// Used to specify a thread colors
    /// </summary>
    public class ThreadColor
    {
        /// <summary>
        /// Default color
        /// </summary>
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

    /// <summary>
    /// Used to specify a message reaction
    /// </summary>
    public class MessageReaction
    {
        public static readonly string LOVE = "😍";
        public static readonly string SMILE = "😆";
        public static readonly string WOW = "😮";
        public static readonly string SAD = "😢";
        public static readonly string ANGRY = "😠";
        public static readonly string YES = "👍";
        public static readonly string NO = "👎";
    }

    /// <summary>
    /// 
    /// </summary>
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
