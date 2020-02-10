using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
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

    internal class EmojiSizeMethods
    {
        public static EmojiSize? _from_tags(List<string> tags)
        {
            var string_to_emojisize = FB_Message_Constants.LIKES;
            foreach (string tag in tags)
            {
                var data = tag.Split(new char[] { ':' }, 2);
                if (data.Length > 1 && data[0] == "hot_emoji_size")
                    return string_to_emojisize[data[1]];
            }
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class FB_Message_Constants
    {
        internal static readonly Dictionary<string, EmojiSize> LIKES = new Dictionary<string, EmojiSize>() {
            { "large", EmojiSize.LARGE },
            { "medium", EmojiSize.MEDIUM },
            { "small", EmojiSize.SMALL },
            { "l", EmojiSize.LARGE },
            { "m", EmojiSize.MEDIUM },
            { "s", EmojiSize.SMALL }
        };

        /// <summary>
        /// Possible reactions to send. See FB_Message.react
        /// </summary>
        public static readonly List<string> SENDABLE_REACTIONS = new List<string>() {
            "❤", "😍", "😆", "😮", "😢", "😠", "👍", "👎"
        };
    }

    /// <summary>
    /// Facebook messenger mention class
    /// </summary>
    public class FB_Mention
    {
        /// The thread ID the mention is pointing at
        public string thread_id { get; set; }
        /// The character where the mention starts
        public int offset { get; set; }
        /// The length of the mention
        public int length { get; set; }

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

        internal static FB_Mention _from_range(JToken data)
        {
            return new FB_Mention(
                thread_id: data?.get("entity")?.get("id")?.Value<string>(),
                offset: data?.get("offset")?.Value<int>() ?? 0,
                length: data?.get("length")?.Value<int>() ?? 0
            );
        }

        internal static FB_Mention _from_prng(JToken data)
        {
            return new FB_Mention(
                thread_id: data?.get("i")?.Value<string>(),
                offset: data?.get("o")?.Value<int>() ?? 0,
                length: data?.get("l")?.Value<int>() ?? 0
            );
        }

        internal Dictionary<string, object> _to_send_data(int i)
        {
            var data = new Dictionary<string, object>();
            data[string.Format("profile_xmd[{0}][id]", i)] = this.thread_id;
            data[string.Format("profile_xmd[{0}][offset]", i)] = this.offset.ToString();
            data[string.Format("profile_xmd[{0}][length]", i)] = this.length.ToString();
            data[string.Format("profile_xmd[{0}][type]", i)] = "p";
            return data;
        }
    }

    /// <summary>
    /// Facebook messenger message class
    /// </summary>
    public class FB_Message
    {
        /// The session to use when making requests
        public Session session { get; set; }
        /// The message ID
        public string uid { get; set; }
        /// The actual message
        public string text { get; set; }
        /// A list of `Mention` objects
        public List<FB_Mention> mentions { get; set; }
        /// A `EmojiSize`. Size of a sent emoji
        public EmojiSize? emoji_size { get; set; }
        /// ID of the sender
        public string author { get; set; }
        /// Timestamp of when the message was sent
        public string timestamp { get; set; }
        /// Whether the message is read
        public bool is_read { get; set; }
        /// A list of pepole IDs who read the message, works only with :func:`FB_Thread.fetchMessages`
        public List<string> read_by { get; set; }
        /// A dict with user's IDs as keys, and their `MessageReaction` as values
        public Dictionary<string, string> reactions { get; set; }
        /// An ID of a sent sticker
        public FB_Sticker sticker { get; set; }
        /// A list of attachments
        public List<FB_Attachment> attachments { get; set; }
        /// A list of `QuickReply`
        public List<FB_QuickReply> quick_replies { get; set; }
        /// Whether the message is unsent (deleted for everyone)
        public bool unsent { get; set; }
        /// Message ID you want to reply to
        public string reply_to_id { get; set; }
        /// Replied message
        public FB_Message replied_to { get; set; }
        /// Whether the message was forwarded
        public bool forwarded { get; set; }
        /// The message was sent from me (not filled)
        public bool is_from_me { get; set; }
        /// The thread this message belong to
        public string thread_id { get; set; }

        /// <summary>
        /// Facebook messenger message class
        /// </summary>
        /// <param name="session"></param>
        /// <param name="text"></param>
        /// <param name="mentions"></param>
        /// <param name="emoji_size"></param>
        /// <param name="uid"></param>
        /// <param name="author"></param>
        /// <param name="timestamp"></param>
        /// <param name="is_read"></param>
        /// <param name="read_by"></param>
        /// <param name="reactions"></param>
        /// <param name="sticker"></param>
        /// <param name="attachments"></param>
        /// <param name="quick_replies"></param>
        /// <param name="unsent"></param>
        /// <param name="reply_to_id"></param>
        /// <param name="replied_to"></param>
        /// <param name="forwarded"></param>
        /// <param name="is_from_me"></param>
        /// <param name="thread_id"></param>
        public FB_Message(Session session, string text = null, List<FB_Mention> mentions = null, EmojiSize? emoji_size = null, string uid = null, string author = null, string timestamp = null, bool is_read = false, List<string> read_by = null, Dictionary<string, string> reactions = null, FB_Sticker sticker = null, List<FB_Attachment> attachments = null, List<FB_QuickReply> quick_replies = null, bool unsent = false, string reply_to_id = null, FB_Message replied_to = null, bool forwarded = false, bool is_from_me = false, string thread_id = null)
        {
            this.session = session;
            this.text = text;
            this.mentions = mentions ?? new List<FB_Mention>();
            this.emoji_size = emoji_size;
            this.uid = uid;
            this.author = author;
            this.timestamp = timestamp;
            this.is_read = is_read;
            this.read_by = read_by ?? new List<string>();
            this.reactions = reactions ?? new Dictionary<string, string>();
            this.sticker = sticker;
            this.attachments = attachments ?? new List<FB_Attachment>();
            this.quick_replies = quick_replies ?? new List<FB_QuickReply>();
            this.unsent = unsent;
            this.reply_to_id = reply_to_id;
            this.replied_to = replied_to;
            this.forwarded = forwarded;
            this.is_from_me = is_from_me;
            this.thread_id = thread_id;
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

        /// <summary>
        /// Like `str.format`, but takes tuples with a thread id and text instead.
        /// Returns a `Message` object, with the formatted string and relevant mentions.
        /// </summary>
        /// <param name="text"></param>
        public static FB_Message formatMentions(string text)
        {
            throw new NotImplementedException();
        }

        internal static bool _get_forwarded_from_tags(List<string> tags)
        {
            if (tags == null)
                return false;
            return tags.Any((tag) => tag.Contains("forward") || tag.Contains("copy"));
        }

        internal Dictionary<string, object> _to_send_data()
        {
            var data = new Dictionary<string, object>();

            if (this.text != null || this.sticker != null || this.emoji_size != null)
                data["action_type"] = "ma-type:user-generated-message";

            if (this.text != null)
                data["body"] = this.text;

            if (this.mentions != null)
            {
                foreach (var item in this.mentions.Select((mention, i) => new { i, mention }))
                {
                    data.update(item.mention._to_send_data(item.i));
                }
            }

            if (this.emoji_size != null)
            {
                if (this.text != null)
                    data["tags[0]"] = "hot_emoji_size:" + Enum.GetName(typeof(EmojiSize), this.emoji_size).ToLower();
                else
                    data["sticker_id"] = this.emoji_size?.GetEnumDescriptionAttribute();
            }

            if (this.sticker != null)
            {
                data["sticker_id"] = this.sticker.uid;
            }

            if (this.quick_replies != null && this.quick_replies.Any())
            {
                var xmd = new Dictionary<string, object>() { { "quick_replies", new List<Dictionary<string, object>>() } };
                foreach (var quick_reply in this.quick_replies)
                {
                    var q = new Dictionary<string, object>();
                    q["content_type"] = quick_reply._type;
                    q["payload"] = quick_reply.payload;
                    q["external_payload"] = quick_reply.external_payload;
                    q["data"] = quick_reply.data;
                    if (quick_reply.is_response)
                        q["ignore_for_webhook"] = false;
                    if (quick_reply is FB_QuickReplyText)
                        q["title"] = ((FB_QuickReplyText)quick_reply).title;
                    if (!(quick_reply is FB_QuickReplyLocation))
                        q["image_url"] = quick_reply.GetType().GetRuntimeProperty("image_url").GetValue(quick_reply, null);
                    ((List<Dictionary<string, object>>)xmd["quick_replies"]).Add(q);
                }
                if (this.quick_replies.Count == 1 && this.quick_replies[0].is_response)
                    xmd["quick_replies"] = ((List<Dictionary<string, object>>)xmd["quick_replies"])[0];
                data["platform_xmd"] = JsonConvert.SerializeObject(xmd);
            }

            if (this.reply_to_id != null)
                data["replied_to_message_id"] = this.reply_to_id;

            return data;
        }

        /// <summary>
        /// Fetches`Message` object from the message id
        /// </summary>
        /// <param name="thread">Thread containing this message</param>
        /// <param name="mid">Message ID to fetch from</param>        
        /// <returns>`FB_Message` object</returns>
        public static async Task<FB_Message> _from_fetch(FB_Thread thread, string mid)
        {
            /*
             * Fetches`Message` object from the message id
             * :param thread: Thread containing this message
             * :param mid: Message ID to fetch from             
             * :return: `Message` object
             * :rtype: Message
             * :raises: FBchatException if request failed
             * */
            var message_info = ((JToken)await thread._forcedFetch(mid))?.get("message");
            return FB_Message._from_graphql(message_info, thread);
        }

        internal static FB_Message _from_graphql(JToken data, FB_Thread thread, JToken read_receipts = null)
        {
            if (data["message_sender"] == null)
                data["message_sender"] = new JObject(new JProperty("id", 0));
            if (data["message"] == null)
                data["message"] = new JObject(new JProperty("text", ""));

            var tags = data.get("tags_list")?.ToObject<List<string>>();

            var rtn = new FB_Message(
                session: thread.session,
                text: data.get("message")?.get("text")?.Value<string>(),
                mentions: data.get("message")?.get("ranges")?.Select((m) =>
                    FB_Mention._from_range(m)
                ).ToList(),
                emoji_size: EmojiSizeMethods._from_tags(tags),
                sticker: FB_Sticker._from_graphql(data.get("sticker")));

            rtn.forwarded = FB_Message._get_forwarded_from_tags(tags);
            rtn.uid = data.get("message_id")?.Value<string>();
            rtn.thread_id = thread.uid; // Added
            rtn.author = data.get("message_sender")?.get("id")?.Value<string>();
            rtn.timestamp = data.get("timestamp_precise")?.Value<string>();
            rtn.unsent = false;

            if (data.get("unread") != null)
            {
                rtn.is_read = !data.get("unread").Value<bool>();
            }

            rtn.reactions = new Dictionary<string, string>();
            foreach (var r in data.get("message_reactions"))
            {
                rtn.reactions.Add(r.get("user")?.get("id")?.Value<string>(), r.get("reaction").Value<string>());
            }
            if (data.get("blob_attachments") != null)
            {
                rtn.attachments = new List<FB_Attachment>();
                foreach (var attachment in data.get("blob_attachments"))
                {
                    rtn.attachments.Add(FB_Attachment.graphql_to_attachment(attachment));
                }
            }
            if (data.get("platform_xmd_encoded") != null)
            {
                var quick_replies = JToken.Parse(data.get("platform_xmd_encoded")?.Value<string>()).get("quick_replies");
                if (quick_replies != null)
                {
                    if (quick_replies.Type == JTokenType.Array)
                        rtn.quick_replies = quick_replies.Select((q) => FB_QuickReply.graphql_to_quick_reply(q)).ToList();
                    else
                        rtn.quick_replies = new List<FB_QuickReply>() { FB_QuickReply.graphql_to_quick_reply(quick_replies) };
                }
            }
            if (data.get("extensible_attachment") != null)
            {
                var attachment = FB_Attachment.graphql_to_extensible_attachment(data.get("extensible_attachment"));
                if (attachment is FB_UnsentMessage)
                    rtn.unsent = true;
                else if (attachment != null)
                {
                    rtn.attachments.Add(attachment);
                }
            }
            if (data.get("replied_to_message") != null && data.get("replied_to_message").get("message") != null)
            {
                // data["replied_to_message"]["message"] is None if the message is deleted
                rtn.replied_to = FB_Message._from_graphql(data.get("replied_to_message").get("message"), thread);
                rtn.reply_to_id = rtn.replied_to.uid;
            }
            if (read_receipts != null)
            {
                rtn.read_by = read_receipts.Where(r => long.Parse(r.get("watermark")?.Value<string>()) >= long.Parse(rtn.timestamp))
                    .Select(r => r.get("actor")?.get("id")?.Value<string>()).ToList();
            }
            return rtn;
        }

        internal static FB_Message _from_reply(JToken data, FB_Thread thread)
        {
            var tags = data.get("messageMetadata")?.get("tags")?.ToObject<List<string>>();

            var rtn = new FB_Message(
                session: thread.session,
                text: data.get("body")?.Value<string>(),
                mentions: JToken.Parse(data.get("data")?.get("prng")?.Value<string>() ?? "{}")?.Select((m) =>
                    FB_Mention._from_prng(m)
                ).ToList(),
                emoji_size: EmojiSizeMethods._from_tags(tags));

            var metadata = data.get("messageMetadata");
            rtn.forwarded = FB_Message._get_forwarded_from_tags(tags);
            rtn.uid = metadata?.get("messageId")?.Value<string>();
            rtn.thread_id = thread.uid; // Added
            rtn.author = metadata?.get("actorFbId")?.Value<string>();
            rtn.timestamp = metadata?.get("timestamp")?.Value<string>();
            rtn.reply_to_id = data?.get("messageReply")?.get("replyToMessageId")?.get("id")?.Value<string>();
            rtn.unsent = false;

            if (data.get("data")?.get("platform_xmd") != null)
            {
                var quick_replies = JToken.Parse(data.get("data")?.get("platform_xmd").Value<string>()).get("quick_replies");
                if (quick_replies.Type == JTokenType.Array)
                    rtn.quick_replies = quick_replies.Select((q) => FB_QuickReply.graphql_to_quick_reply(q)).ToList();
                else
                    rtn.quick_replies = new List<FB_QuickReply>() { FB_QuickReply.graphql_to_quick_reply(quick_replies) };
            }
            foreach (var atc in data.get("attachments") ?? Enumerable.Empty<JToken>())
            {
                var attachment = JToken.Parse(atc.get("mercuryJSON")?.Value<string>());
                if (attachment.get("blob_attachment") != null)
                {
                    rtn.attachments.Add(
                        FB_Attachment.graphql_to_attachment(attachment.get("blob_attachment"))
                    );
                }
                if (attachment.get("extensible_attachment") != null)
                {
                    var ext_attachment = FB_Attachment.graphql_to_extensible_attachment(attachment.get("extensible_attachment"));
                    if (ext_attachment is FB_UnsentMessage)
                        rtn.unsent = true;
                    else if (ext_attachment != null)
                    {
                        rtn.attachments.Add(ext_attachment);
                    }
                }
            }

            return rtn;
        }

        internal static FB_Message _from_pull(JToken data, FB_Thread thread, string author = null, string timestamp = null)
        {
            var metadata = data?.get("messageMetadata");
            var tags = metadata?.get("tags")?.ToObject<List<string>>();

            var rtn = new FB_Message(
                session: thread.session,
                text: data.get("body")?.Value<string>());
            rtn.uid = metadata?.get("messageId")?.Value<string>();
            rtn.session = thread.session;
            rtn.thread_id = thread.uid;
            rtn.author = author;
            rtn.timestamp = timestamp;
            rtn.mentions = JToken.Parse(data.get("data")?.get("prng")?.Value<string>() ?? "{}")?.Select((m) =>
                    FB_Mention._from_prng(m)
                ).ToList();

            if (data.get("attachments") != null)
            {
                try
                {
                    foreach (var a in data.get("attachments"))
                    {
                        var mercury = a.get("mercury");
                        if (mercury.get("blob_attachment") != null)
                        {
                            var image_metadata = a.get("imageMetadata");
                            var attach_type = mercury.get("blob_attachment")?.get("__typename")?.Value<string>();
                            var attachment = FB_Attachment.graphql_to_attachment(
                                mercury.get("blob_attachment")
                            );

                            if (new string[] { "MessageFile", "MessageVideo", "MessageAudio" }.Contains(attach_type))
                            {
                                // TODO: Add more data here for audio files
                                if (attachment is FB_FileAttachment)
                                    ((FB_FileAttachment)attachment).size = a?.get("fileSize")?.Value<int>() ?? 0;
                                if (attachment is FB_VideoAttachment)
                                    ((FB_VideoAttachment)attachment).size = a?.get("fileSize")?.Value<int>() ?? 0;
                            }
                            rtn.attachments.Add(attachment);
                        }
                        else if (mercury.get("sticker_attachment") != null)
                        {
                            rtn.sticker = FB_Sticker._from_graphql(
                                mercury.get("sticker_attachment")
                            );
                        }
                        else if (mercury.get("extensible_attachment") != null)
                        {
                            var attachment = FB_Attachment.graphql_to_extensible_attachment(
                                mercury.get("extensible_attachment")
                            );
                            if (attachment is FB_UnsentMessage)
                                rtn.unsent = true;
                            else if (attachment != null)
                                rtn.attachments.Add(attachment);
                        }
                    }
                }
                catch
                {
                    Debug.WriteLine(string.Format("An exception occured while reading attachments: {0}", data.get("attachments")));
                }
            }

            rtn.emoji_size = EmojiSizeMethods._from_tags(tags);
            rtn.forwarded = FB_Message._get_forwarded_from_tags(tags);
            return rtn;
        }

        /// <summary>
        /// Create Message from string or other Message
        /// </summary>
        /// <param name="message"></param>
        public FB_Message(object message)
        {
            if (message is FB_Message msg)
            {
                this.text = msg.text;
                this.mentions = msg.mentions;
                this.emoji_size = msg.emoji_size;
                this.uid = msg.uid;
                this.author = msg.author;
                this.timestamp = msg.timestamp;
                this.is_read = msg.is_read;
                this.read_by = msg.read_by;
                this.reactions = msg.reactions;
                this.sticker = msg.sticker;
                this.attachments = msg.attachments;
                this.quick_replies = msg.quick_replies;
                this.unsent = msg.unsent;
                this.reply_to_id = msg.reply_to_id;
                this.replied_to = msg.replied_to;
                this.forwarded = msg.forwarded;
                this.is_from_me = msg.is_from_me;
                this.thread_id = msg.thread_id;
            }
            else
            {
                this.text = (string)message;
            }
        }

        /// <summary>
        /// Unsends a message(removes for everyone)
        /// </summary>
        /// <returns></returns>
        public async Task unsend()
        {
            /*
             * Unsends a message(removes for everyone)
             * */
            var data = new Dictionary<string, object>() { { "message_id", this.uid } };
            var j = await this.session._payload_post("/messaging/unsend_message/?dpr=1", data);
        }

        /// <summary>
        /// Reacts to a message, or removes reaction
        /// </summary>
        /// <param name="reaction">Reaction emoji to use, if null removes reaction</param>
        /// <returns></returns>
        public async Task react(string reaction = null)
        {
            /*
             * Reacts to a message, or removes reaction
             * :param reaction: Reaction emoji to use, if null removes reaction
             :type reaction: MessageReaction or null
             : raises: FBchatException if request failed
             */
            var data = new Dictionary<string, object>() {
                { "action", reaction != null ? "ADD_REACTION" : "REMOVE_REACTION"},
                {"client_mutation_id", "1"},
                {"actor_id", this.session.user.uid},
                {"message_id", this.uid},
                {"reaction", reaction != null && FB_Message_Constants.SENDABLE_REACTIONS.Contains(reaction) ? reaction : null}
            };

            var payl = new Dictionary<string, object>() { { "doc_id", 1491398900900362 }, { "variables", JsonConvert.SerializeObject(new Dictionary<string, object>() { { "data", data } }) } };
            var j = await this.session._payload_post("/webgraphql/mutation", payl);
            Utils.handle_graphql_errors(j);
        }

        internal static async Task _delete_many(Session session, IEnumerable<string> message_ids)
        {
            var umessage_ids = Utils.require_list<string>(message_ids);
            var data = new Dictionary<string, object>();
            foreach (var obj in umessage_ids.Select((x, index) => new { message_id = x, i = index }))
                data[string.Format("message_ids[{0}]", obj.i)] = obj.message_id;
            var j = await session._payload_post("/ajax/mercury/delete_messages.php?dpr=1", data);
        }

        /// <summary>
        /// TODO: Implement this...
        /// </summary>
        /// <returns></returns>
        public async Task fetch()
        {
            await Task.Yield();
            throw new NotImplementedException();
        }

        /// <summary>
        /// Delete the message (removes it only for the user).
        /// If you want to delete multiple messages, please use `Client.delete_messages`.
        /// Example:
        ///     >>> message.delete()
        /// </summary>
        /// <returns></returns>
        public async Task delete()
        {
            await _delete_many(this.session, new string[] { this.uid });
        }
    }

    /// <summary>
    /// Represents data in a Facebook message snippet
    /// </summary>
    public class FB_Message_Snippet : FB_Message
    {
        // A dict with offsets, mapped to the matched text
        Dictionary<int, string> matched_keywords;

        /// <summary>
        /// Represents data in a Facebook message snippet
        /// </summary>
        /// <param name="session"></param>
        /// <param name="uid"></param>
        /// <param name="text"></param>
        /// <param name="author"></param>
        /// <param name="timestamp"></param>
        /// <param name="thread_id"></param>
        /// <param name="matched_keywords"></param>
        public FB_Message_Snippet(Session session, string uid = null, string text = null, string author = null, string timestamp = null, string thread_id = null, Dictionary<int, string> matched_keywords = null)
            : base(session, uid: uid, text: text, timestamp: timestamp, author: author, thread_id: thread_id)
        {
            this.matched_keywords = matched_keywords;
        }

        internal static FB_Message_Snippet _parse(JToken data, FB_Thread thread)
        {
            return new FB_Message_Snippet(
                session: thread.session,
                thread_id: thread.uid,
                uid: data?.get("message_id")?.Value<string>(),
                author: data?.get("author")?.Value<string>()?.Replace("fbid:", ""),
                timestamp: data?.get("timestamp")?.Value<string>(),
                text: data?.get("body")?.Value<string>(),
                matched_keywords: data?.get("matched_keywords")?.ToObject<Dictionary<int, string>>()
            );
        }
    }
}
