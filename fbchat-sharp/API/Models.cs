using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Custom exception thrown by fbchat. All exceptions in the fbchat module inherits this
    /// </summary>
    public class FBchatException : Exception
    {
        public FBchatException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Thrown by fbchat when Facebook returns an error
    /// </summary>
    public class FBchatFacebookError : FBchatException
    {
        /// The error code that Facebook returned
        public string fb_error_code = null;
        /// The error message that Facebook returned (In the user's own language)
        public string fb_error_message = null;
        /// The status code that was sent in the http response (eg. 404) (Usually only set if not successful, aka. not 200)
        public int request_status_code = 0;

        public FBchatFacebookError(string message, string fb_error_code = null, string fb_error_message = null, int request_status_code = 0) : base(message)
        {
            this.fb_error_code = fb_error_code;
            this.fb_error_message = fb_error_message;
            this.request_status_code = request_status_code;
        }
    }

    /// <summary>
    /// Thrown by fbchat when wrong values are entered
    /// </summary>
    public class FBchatUserError : FBchatException
    {
        public FBchatUserError(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Facebook messenger file class
    /// </summary>
    public class FB_File
    {
        /// Local or remote file path
        public string path = null;
        /// Local or remote file stream
        public Stream data = null;

        public FB_File(Stream data = null, string path = null)
        {
            this.data = data;
            this.path = path;
        }
    }

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
        public ISet<string> participants = null;
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
        public FB_Group(string uid, string photo = null, string name = null, int message_count = 0, ISet<string> participants = null, Dictionary<string, string> nicknames = null, string color = null, string emoji = null) :
            base(ThreadType.GROUP, uid, photo, name, message_count: message_count)
        {
            this.participants = participants;
            this.nicknames = nicknames;
            this.color = color;
            this.emoji = emoji;
        }
    }

    /// <summary>
    /// Represents a Facebook room. Inherits `Group`
    /// </summary>
    public class FB_Room : FB_Group
    {

        /// Set containing user IDs of thread admins
        public ISet<string> admins = null;
        /// True if users need approval to join
        public bool approval_mode = false;
        /// Set containing user IDs requesting to join
        public ISet<string> approval_requests = null;
        /// Link for joining room
        public string join_link = null;
        /// True is room is not discoverable
        public bool privacy_mode = false;

        public FB_Room(string uid, string photo = null, string name = null, int message_count = 0, ISet<string> participants = null, Dictionary<string, string> nicknames = null, string color = null, string emoji = null, ISet<string> admins = null, bool approval_mode = false, ISet<string> approval_requests = null, string join_link = null, bool privacy_mode = false)
            : base(uid, photo, name, message_count, participants, nicknames, color, emoji)
        {
            this.type = ThreadType.ROOM;
            if (admins == null)
            {
                admins = new HashSet<string>();
            }
            this.admins = admins;
            this.approval_mode = approval_mode;
            if (approval_requests == null)
            {
                approval_requests = new HashSet<string>();
            }
            this.approval_requests = approval_requests;
            this.join_link = join_link;
            this.privacy_mode = privacy_mode;
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
        /// The actual message
        public string text = null;
        /// A list of :class:`Mention` objects
        public List<FB_Mention> mentions = new List<FB_Mention>();
        /// A :class:`EmojiSize`. Size of a sent emoji
        public EmojiSize? emoji_size = null;
        /// The message ID
        public string uid = null;
        /// ID of the sender
        public string author = null;
        /// Timestamp of when the message was sent
        public string timestamp = null;
        /// Whether the message is read
        public bool is_read = false;
        /// A dict with user's IDs as keys, and their :class:`MessageReaction` as values
        public Dictionary<string, MessageReaction> reactions = null;
        /// An ID of a sent sticker
        public FB_Sticker sticker = null;
        /// A list of attachments
        public List<FB_Attachment> attachments = new List<FB_Attachment>();
        /// ID of the thread the message was sent to
        public string thread_id = "";
        /// The message was sent from me (not filled)
        public bool is_from_me { get; set; }

        /// <summary>
        /// Represents a Facebook message
        /// </summary>
        /// <param name="text"></param>
        /// <param name="mentions"></param>
        /// <param name="sticker"></param>
        /// <param name="emoji_size"></param>
        /// <param name="attachments"></param>
        public FB_Message(string text = null, List<FB_Mention> mentions = null, FB_Sticker sticker = null, EmojiSize? emoji_size = null, List<FB_Attachment> attachments = null)
        {
            // this.uid = uid;
            // this.author = author;
            // this.timestamp = timestamp;
            // this.thread_id = thread_id;
            this.text = text;
            if (mentions == null)
                mentions = new List<FB_Mention>();
            this.mentions = mentions;
            this.sticker = sticker;
            if (attachments == null)
                this.attachments = new List<FB_Attachment>();
            this.emoji_size = emoji_size;
            this.reactions = new Dictionary<string, MessageReaction>();
        }

        /// <returns>Pretty string representation of the thread</returns>
        public override string ToString()
        {
            return this.__unicode__();
        }

        private string __unicode__()
        {
            return string.Format("<Message ({0}): {1}, mentions={2} emoji_size={3} attachments={4}>", this.uid, this.text, this.mentions, this.emoji_size, this.attachments);
        }
    }

    /// <summary>
    /// Represents a Facebook attachment
    /// </summary>    
    public class FB_Attachment
    {
        /// The attachment ID
        public string uid = null;

        public FB_Attachment(string uid = null)
        {
            this.uid = uid;
        }
    }

    /// <summary>
    /// Represents a Facebook sticker that has been sent to a Facebook thread as an attachment
    /// </summary>
    public class FB_Sticker : FB_Attachment
    {
        /// The sticker-pack's ID
        public string pack = null;
        /// Whether the sticker is animated
        public bool is_animated = false;
        /// If the sticker is animated, the following should be present
        /// URL to a medium spritemap
        public string medium_sprite_image = null;
        /// URL to a large spritemap
        public string large_sprite_image = null;
        /// The amount of frames present in the spritemap pr. row
        public int frames_per_row = 0;
        /// The amount of frames present in the spritemap pr. coloumn
        public int frames_per_col = 0;
        /// The frame rate the spritemap is intended to be played in
        public float frame_rate = 0;
        /// URL to the sticker's image
        public object url = null;
        /// Width of the sticker
        public float width = 0;
        /// Height of the sticker
        public float height = 0;
        /// The sticker's label/name
        public string label = null;

        public FB_Sticker(string uid = null) : base(uid)
        {
        }
    }

    /// <summary>
    /// Represents a shared item (eg. URL) that has been sent as a Facebook attachment - *Currently Incomplete!*
    /// </summary>
    public class FB_ShareAttachment : FB_Attachment
    {
        public FB_ShareAttachment(string uid = null) : base(uid)
        {
        }
    }

    /// <summary>
    /// Represents a file that has been sent as a Facebook attachment
    /// </summary>
    public class FB_FileAttachment : FB_Attachment
    {
        /// Url where you can download the file
        public object url = null;
        /// Size of the file in bytes
        public long size = 0;
        /// Name of the file
        public string name = null;
        /// Whether Facebook determines that this file may be harmful
        public bool is_malicious = false;

        public FB_FileAttachment(string uid = null, string url = null, long size = 0, string name = null, bool is_malicious = false) : base(uid)
        {
            this.url = url;
            this.size = size;
            this.name = name;
            this.is_malicious = is_malicious;
        }
    }

    /// <summary>
    /// Represents an audio file that has been sent as a Facebook attachment - *Currently Incomplete!*
    /// </summary>
    public class FB_AudioAttachment : FB_FileAttachment
    {
        public FB_AudioAttachment(string uid = null, string url = null, long size = 0, string name = null, bool is_malicious = false)
            : base(uid, url, size, name, is_malicious)
        {
        }
    }

    /// <summary>
    /// Represents an image that has been sent as a Facebook attachment
    /// To retrieve the full image url, use: :func:`fbchat.Client.fetchImageUrl`
    /// and pass it the uid of the image attachment
    /// </summary>
    public class FB_ImageAttachment : FB_Attachment
    {
        /// The extension of the original image (eg. 'png')
        public string original_extension = null;
        /// Width of original image
        public float width = 0;
        /// Height of original image
        public float height = 0;
        /// Whether the image is animated
        public bool is_animated = false;
        /// URL to a thumbnail of the image
        public string thumbnail_url = null;
        /// URL to a medium preview of the image
        public string preview_url = null;
        /// Width of the medium preview image
        public int preview_width = 0;
        /// Height of the medium preview image
        public int preview_height = 0;
        /// URL to a large preview of the image
        public string large_preview_url = null;
        /// Width of the large preview image
        public int large_preview_width = 0;
        /// Height of the large preview image
        public int large_preview_height = 0;
        /// URL to an animated preview of the image (eg. for gifs)
        public string animated_preview_url = null;
        /// Width of the animated preview image
        public int animated_preview_width = 0;
        /// Height of the animated preview image
        public int animated_preview_height = 0;

        public FB_ImageAttachment(string uid = null, string original_extension = null, int width = 0, int height = 0, bool is_animated = false, string thumbnail_url = null, object preview = null, object large_preview = null, object animated_preview = null) : base(uid)
        {
            this.original_extension = original_extension;
            if (width != 0)
            {
                width = Convert.ToInt32(width);
            }
            this.width = width;
            if (height != 0)
            {
                height = Convert.ToInt32(height);
            }
            this.height = height;
            this.is_animated = is_animated;
            this.thumbnail_url = thumbnail_url;
            if (preview == null)
            {
            }
            // this.preview_url = preview.get("uri");
            // this.preview_width = preview.get("width");
            // this.preview_height = preview.get("height");
            if (large_preview == null)
            {
            }
            // this.large_preview_url = large_preview.get("uri");
            // this.large_preview_width = large_preview.get("width");
            // this.large_preview_height = large_preview.get("height");
            if (animated_preview == null)
            {
            }
            // this.animated_preview_url = animated_preview.get("uri");
            // this.animated_preview_width = animated_preview.get("width");
            // this.animated_preview_height = animated_preview.get("height");
        }
    }

    /// <summary>
    /// Represents a video that has been sent as a Facebook attachment
    /// </summary>
    public class FB_VideoAttachment
        : FB_Attachment
    {
        /// Size of the original video in bytes
        public int size = 0;
        /// Width of original video
        public int width = 0;
        /// Height of original video
        public int height = 0;
        /// Length of video in milliseconds
        public int duration = 0;
        /// URL to very compressed preview video
        public string preview_url = null;
        /// URL to a small preview image of the video
        public string small_image_url = null;
        /// Width of the small preview image
        public int small_image_width = 0;
        /// Height of the small preview image
        public int small_image_height = 0;
        /// URL to a medium preview image of the video
        public string medium_image_url = null;
        /// Width of the medium preview image
        public int medium_image_width = 0;
        /// Height of the medium preview image
        public int medium_image_height = 0;
        /// URL to a large preview image of the video
        public string large_image_url = null;
        /// Width of the large preview image
        public int large_image_width = 0;
        /// Height of the large preview image
        public int large_image_height = 0;

        public FB_VideoAttachment(string uid = null, int size = 0, int width = 0, int height = 0, int duration = 0, string preview_url = null, object small_image = null, object medium_image = null, object large_image = null) : base(uid)
        {
            this.size = size;
            this.width = width;
            this.height = height;
            this.duration = duration;
            this.preview_url = preview_url;
            if (small_image == null)
            {
            }
            // this.small_image_url = small_image.get("uri");
            // this.small_image_width = small_image.get("width");
            // this.small_image_height = small_image.get("height");
            if (medium_image == null)
            {
            }
            // this.medium_image_url = medium_image.get("uri");
            // this.medium_image_width = medium_image.get("width");
            // this.medium_image_height = medium_image.get("height");
            if (large_image == null)
            {
            }
            // this.large_image_url = large_image.get("uri");
            // this.large_image_width = large_image.get("width");
            // this.large_image_height = large_image.get("height");
        }
    }
    /// <summary>
    /// Facebook messenger mention class
    /// </summary>
    public class FB_Mention
    {
        /// The thread ID the mention is pointing at
        public string thread_id = null;
        /// The character where the mention starts
        public int offset = 0;
        /// The length of the mention
        public int length = 0;

        /// <summary>
        /// Represents a @mention
        /// </summary>
        /// <param name="thread_id"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public FB_Mention(string thread_id = null, int offset = 0, int length = 10)
        {
            this.thread_id = thread_id;
            this.offset = offset;
            this.length = length;
        }

        /// <returns>Pretty string representation of the thread</returns>
        public override string ToString()
        {
            return this.__unicode__();
        }

        private string __unicode__()
        {
            return string.Format("<Mention {0}: offset={1} length={2}>", this.thread_id, this.offset, this.length);
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
        ROOM = 4,
    }

    /// <summary>
    /// Used to specify where a thread is located (inbox, pending, archived, other).
    /// </summary>
    public class ThreadLocation
    {
        public const string INBOX = "inbox";
        public const string PENDING = "pending";
        public const string ARCHIVED = "action:archived";
        public const string OTHER = "other";
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
    public enum EmojiSize
    {
        [Description("369239383222810")]
        LARGE,
        [Description("369239343222814")]
        MEDIUM,
        [Description("369239263222822")]
        SMALL
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
    public enum MessageReaction
    {
        [Description("😍")]
        LOVE,
        [Description("😆")]
        SMILE,
        [Description("😮")]
        WOW,
        [Description("😢")]
        SAD,
        [Description("😠")]
        ANGRY,
        [Description("👍")]
        YES,
        [Description("👎")]
        NO
    }
}
