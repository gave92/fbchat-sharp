using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Used to specify what type of Facebook thread is being used
    /// </summary>
    public enum ThreadType
    {
        USER = 1,
        GROUP = 2,
        ROOM = 2,
        PAGE = 3,
        INVALID = 4
    }

    /// <summary>
    /// Used to specify where a thread is located (inbox, pending, archived, other).
    /// </summary>
    public class ThreadLocation
    {
        public const string INBOX = "INBOX";
        public const string PENDING = "PENDING";
        public const string ARCHIVED = "ARCHIVED";
        public const string OTHER = "OTHER";
    }

    /// <summary>
    /// Used to specify a thread colors
    /// </summary>
    public class ThreadColor
    {
        public const string MESSENGER_BLUE = "#0084ff";
        public const string VIKING = "#44bec7";
        public const string GOLDEN_POPPY = "#ffc300";
        public const string RADICAL_RED = "#fa3c4c";
        public const string SHOCKING = "#d696bb";
        public const string PICTON_BLUE = "#6699cc";
        public const string FREE_SPEECH_GREEN = "#13cf13";
        public const string PUMPKIN = "#ff7e29";
        public const string LIGHT_CORAL = "#e68585";
        public const string MEDIUM_SLATE_BLUE = "#7646ff";
        public const string DEEP_SKY_BLUE = "#20cef5";
        public const string FERN = "#67b868";
        public const string CAMEO = "#d4a88c";
        public const string BRILLIANT_ROSE = "#ff5ca1";
        public const string BILOBA_FLOWER = "#a695c7";
        public const string TICKLE_ME_PINK = "#ff7ca8";
        public const string MALACHITE = "#1adb5b";
        public const string RUBY = "#f01d6a";
        public const string DARK_TANGERINE = "#ff9c19";
        public const string BRIGHT_TURQUOISE = "#0edcde";

        public static string _from_graphql(JToken data)
        {
            if (data == null)
                return null;
            var color = data.Value<string>();
            if (string.IsNullOrEmpty(color))
                return ThreadColor.MESSENGER_BLUE;
            try
            {
                return string.Format("#{0}", color.Substring(2));
            }
            catch
            {
                throw new FBchatException(string.Format("Could not get ThreadColor from color: {0}", color));
            }
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
        public ThreadType type { get; set; }
        /// The thread"s picture
        public string photo { get; set; }
        /// The name of the thread
        public string name { get; set; }
        /// Timestamp of last message
        public string last_message_timestamp { get; set; }
        /// Number of messages in the thread
        public int message_count { get; set; }
        /// Set :class:`Plan`
        public FB_Plan plan { get; set; }

        /// <summary>
        /// Represents a Facebook thread
        /// </summary>
        /// <param name="type"></param>
        /// <param name="uid"></param>
        /// <param name="photo"></param>
        /// <param name="name"></param>
        /// <param name="last_message_timestamp"></param>
        /// <param name="message_count"></param>
        /// <param name="plan"></param>
        public FB_Thread(ThreadType type, string uid, string photo = null, string name = null, string last_message_timestamp = null, int message_count = 0, FB_Plan plan = null)
        {
            this.uid = uid;
            this.type = type;
            this.photo = photo;
            this.name = name;
            this.last_message_timestamp = last_message_timestamp;
            this.message_count = message_count;
            this.plan = plan;
        }

        public static Dictionary<string, object> _parse_customization_info(JToken data)
        {
            var rtn = new Dictionary<string, object>();
            if (data == null || data.get("customization_info") == null)
                return rtn;
            var info = data.get("customization_info");
            rtn["emoji"] = info.get("emoji");
            rtn["color"]= ThreadColor._from_graphql(info.get("outgoing_bubble_color"));

            if (
                data.get("thread_type")?.Value<string>() == "GROUP"
                || (data.get("is_group_thread")?.Value<bool>() ?? false)
                || (data.get("thread_key")?.get("thread_fbid") != null))
            {
                rtn["nicknames"] = new Dictionary<string, string>();
                foreach (var k in info.get("participant_customizations"))
                    ((Dictionary<string, string>)rtn["nicknames"])[k.get("participant_id")?.Value<string>()] = k.get("nickname")?.Value<string>();
            }
            else if (info.get("participant_customizations") != null)
            {
                string uid = data.get("thread_key")?.get("other_user_id")?.Value<string>() ?? data.get("id")?.Value<string>();
                var pc = info.get("participant_customizations");
                if (pc.Type == JTokenType.Array && pc.Value<JArray>().Count > 0)
                {
                    if (pc[0]?.get("participant_id")?.Value<string>() == uid)
                        rtn["nickname"] = pc[0]?.get("nickname")?.Value<string>();
                    else
                        rtn["own_nickname"] = pc[0]?.get("nickname")?.Value<string>();
                }
                if (pc.Type == JTokenType.Array && pc.Value<JArray>().Count > 1)
                {
                    if (pc[1]?.get("participant_id")?.Value<string>() == uid)
                        rtn["nickname"] = pc[1]?.get("nickname")?.Value<string>();
                    else
                        rtn["own_nickname"] = pc[1]?.get("nickname")?.Value<string>();
                }
            }

            return rtn;
        }

        public virtual Dictionary<string,object> _to_send_data()
        {
            // TODO: Only implement this in subclasses
            return new Dictionary<string, object>() { { "other_user_fbid", this.uid } };
        }
    }
}
