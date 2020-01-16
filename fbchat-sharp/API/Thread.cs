using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        MARKETPLACE = 4,
        INVALID = 5
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
        /// The profile url
        public Session session { get; set; }
        /// The unique identifier of the thread. Can be used a `thread_id`. See :ref:`intro_threads` for more info
        public string uid { get; set; }
        /// The thread's picture
        public FB_Image photo { get; set; }
        /// The name of the thread
        public string name { get; set; }
        /// Timestamp of last message
        public string last_message_timestamp { get; set; }
        /// Number of messages in the thread
        public int message_count { get; set; }
        /// Set `Plan`
        public FB_Plan plan { get; set; }

        /// <summary>
        /// Represents a Facebook thread
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="session"></param>
        /// <param name="photo"></param>
        /// <param name="name"></param>
        /// <param name="last_message_timestamp"></param>
        /// <param name="message_count"></param>
        /// <param name="plan"></param>
        public FB_Thread(string uid, Session session, FB_Image photo = null, string name = null, string last_message_timestamp = null, int message_count = 0, FB_Plan plan = null)
        {
            this.uid = uid;
            this.session = session;
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

        public ThreadType get_thread_type()
        {
            switch (this)
            {
                case FB_User t:
                    return ThreadType.USER;
                case FB_Room t:
                    return ThreadType.ROOM;
                case FB_Group t:
                    return ThreadType.GROUP;                
                case FB_Page t:
                    return ThreadType.PAGE;
                case FB_Marketplace t:
                    return ThreadType.MARKETPLACE;
                default:
                    return ThreadType.INVALID;
            }
        }

        public async Task<JToken> _forcedFetch(string thread_id, string mid)
        {
            var param = new Dictionary<string, object>() { { "thread_and_message_id", new Dictionary<string, object>() { { "thread_id", thread_id }, { "message_id", mid } } } };
            return await this.session.graphql_request(GraphQL.from_doc_id("1768656253222505", param));
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>Message ID of the sent message</returns>
        public async Task<string> send(FB_Message message = null)
        {
            /*
             * Sends a message to a thread
             * :param message: Message to send
             * :type message: models.Message
             * :return: :ref:`Message ID <intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             */

            var data = this._to_send_data();
            data.update(message._to_send_data());
            return await this.session._do_send_request(data);
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        public async Task<string> sendText(string message = null)
        {
            return await this.send(new FB_Message(text: message));
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.send` instead")]
        public async Task<string> sendEmoji(string emoji = null, EmojiSize size = EmojiSize.SMALL)
        {
            return await this.send(new FB_Message(text: emoji, emoji_size: size));
        }

        /// <summary>
        /// :ref:`Message ID` of the sent message
        /// </summary>
        /// <param name="wave_first">Whether to wave first or wave back</param>
        /// <returns></returns>
        public async Task<string> wave(bool wave_first = true)
        {
            /*
             * Says hello with a wave to a thread!
             * :param wave_first: Whether to wave first or wave back
             * :return: :ref:`Message ID<intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             * */
            var data = this._to_send_data();
            data["action_type"] = "ma-type:user-generated-message";
            data["lightweight_action_attachment[lwa_state]"] = wave_first ? "INITIATED" : "RECIPROCATED";
            data["lightweight_action_attachment[lwa_type]"] = "WAVE";
            if (this is FB_User)
                data["specific_to_list[0]"] = string.Format("fbid:{0}", this.uid);
            return await this.session._do_send_request(data);
        }

        /// <summary>
        /// Replies to a chosen quick reply
        /// </summary>
        /// <param name="quick_reply">Quick reply to reply to</param>
        /// <param name="payload">Optional answer to the quick reply</param>
        /// <returns></returns>
        public async Task<string> quickReply(FB_QuickReply quick_reply, dynamic payload = null)
        {
            /*
             * Replies to a chosen quick reply
             * :param quick_reply: Quick reply to reply to
             * :param payload: Optional answer to the quick reply
             * :type quick_reply: QuickReply
             * :return: :ref:`Message ID<intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             * */
            quick_reply.is_response = true;
            if (quick_reply is FB_QuickReplyText)
            {
                return await this.send(
                    new FB_Message(text: ((FB_QuickReplyText)quick_reply).title, quick_replies: new List<FB_QuickReply>() { quick_reply })
                );
            }
            else if (quick_reply is FB_QuickReplyLocation)
            {
                if (!(payload is FB_LocationAttachment))
                    throw new ArgumentException(
                        "Payload must be an instance of `fbchat-sharp.LocationAttachment`"
                    );
                return await this.sendLocation(payload);
            }
            else if (quick_reply is FB_QuickReplyEmail)
            {
                //if (payload == null)
                //    payload = (await this.getEmails())[0];
                quick_reply.external_payload = quick_reply.payload;
                quick_reply.payload = payload;
                return await this.send(new FB_Message(text: payload, quick_replies: new List<FB_QuickReply>() { quick_reply }));
            }
            else if (quick_reply is FB_QuickReplyPhoneNumber)
            {
                //if (payload == null)
                //    payload = (await this.getPhoneNumbers())[0];
                quick_reply.external_payload = quick_reply.payload;
                quick_reply.payload = payload;
                return await this.send(new FB_Message(text: payload, quick_replies: new List<FB_QuickReply>() { quick_reply }));
            }
            return null;
        }

        private async Task<dynamic> _sendLocation(
            FB_LocationAttachment location, bool current = true, FB_Message message = null
        )
        {
            var data = this._to_send_data();
            if (message != null)
                data.update(message._to_send_data());
            data["action_type"] = "ma-type:user-generated-message";
            data["location_attachment[coordinates][latitude]"] = location.latitude;
            data["location_attachment[coordinates][longitude]"] = location.longitude;
            data["location_attachment[is_current_location]"] = current;
            return await this.session._do_send_request(data);
        }

        /// <summary>
        /// Sends a given location to a thread as the user's current location
        /// </summary>
        /// <param name="location">Location to send</param>
        /// <param name="message">Additional message</param>
        /// <returns>:ref:`Message ID` of the sent message</returns>
        public async Task<string> sendLocation(FB_LocationAttachment location, FB_Message message = null)
        {
            /*
             * Sends a given location to a thread as the user's current location
             * :param location: Location to send
             * :param message: Additional message
             * :type location: LocationAttachment
             * :type message: Message
             * :return: :ref:`Message ID<intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             * */
            return await this._sendLocation(
                location: location,
                current: true,
                message: message
            );
        }

        /// <summary>
        /// Sends a given location to a thread as a pinned location
        /// </summary>
        /// <param name="location">Location to send</param>
        /// <param name="message">Additional message</param>
        /// <returns>:ref:`Message ID` of the sent message</returns>
        public async Task<string> sendPinnedLocation(FB_LocationAttachment location, FB_Message message = null)
        {
            /*
             * Sends a given location to a thread as a pinned location
             * :param location: Location to send
             * :param message: Additional message
             * :type location: LocationAttachment
             * :type message: Message
             * :return: :ref:`Message ID<intro_message_ids>` of the sent message
             * :raises: FBchatException if request failed
             * */
            return await this._sendLocation(
                location: location,
                current: false,
                message: message
            );
        }

        private async Task<dynamic> _sendFiles(
            List<Tuple<string, string>> files, FB_Message message = null)
        {
            /*
             * Sends files from file IDs to a thread
             * `files` should be a list of tuples, with a file's ID and mimetype
             * */
            var data = this._to_send_data();
            data.update(new FB_Message(message)._to_send_data());

            data["action_type"] = "ma-type:user-generated-message";
            data["has_attachment"] = true;

            foreach (var obj in files.Select((x, index) => new { f = x, i = index }))
                data[string.Format("{0}s[{1}]", Utils.mimetype_to_key(obj.f.Item2), obj.i)] = obj.f.Item1;

            return await this.session._do_send_request(data);
        }

        /// <summary>
        /// Sends files from URLs to a thread
        /// </summary>
        /// <param name="file_urls">URLs of files to upload and send</param>
        /// <param name="message">Additional message</param>
        /// <returns>`Message ID of the sent files</returns>
        public async Task<dynamic> sendRemoteFiles(
            List<string> file_urls, FB_Message message = null)
        {
            /*
             * Sends files from URLs to a thread
             * :param file_urls: URLs of files to upload and send
             * :param message: Additional message
             * :return: :ref:`Message ID<intro_message_ids>` of the sent files
             * :raises: FBchatException if request failed
             * */
            var ufile_urls = Utils.require_list<string>(file_urls);
            var files = await this.session._upload(await this.session.get_files_from_urls(ufile_urls));
            return await this._sendFiles(
                files: files, message: message
            );
        }

        /// <summary>
        /// Sends local files to a thread
        /// </summary>
        /// <param name="file_paths">Paths of files to upload and send</param>
        /// <param name="message">Additional message</param>
        /// <returns>:ref:`Message ID` of the sent files</returns>
        public async Task<string> sendLocalFiles(Dictionary<string, Stream> file_paths = null, FB_Message message = null)
        {
            /*
             * Sends local files to a thread
             * :param file_paths: Paths of files to upload and send
             * :param message: Additional message
             * :return: :ref:`Message ID <intro_message_ids>` of the sent files
             * :raises: FBchatException if request failed
             */

            var files = await this.session._upload(this.session.get_files_from_paths(file_paths));
            return await this._sendFiles(files: files, message: message);
        }

        /// <summary>
        /// Sends voice clips from URLs to a thread
        /// </summary>
        /// <param name="clip_urls">URLs of voice clips to upload and send</param>
        /// <param name="message">Additional message</param>
        /// <returns>`Message ID of the sent files</returns>
        public async Task<dynamic> sendRemoteVoiceClips(
            List<string> clip_urls, FB_Message message = null)
        {
            /*
             * Sends voice clips from URLs to a thread
             * :param clip_urls: URLs of clips to upload and send
             * :param message: Additional message
             * :return: :ref:`Message ID<intro_message_ids>` of the sent files
             * :raises: FBchatException if request failed
             * */
            var uclip_urls = Utils.require_list<string>(clip_urls);
            var files = await this.session._upload(await this.session.get_files_from_urls(uclip_urls), voice_clip: true);
            return await this._sendFiles(
                files: files, message: message
            );
        }

        /// <summary>
        /// Sends local voice clips to a thread
        /// </summary>
        /// <param name="clip_paths">Paths of voice clips to upload and send</param>
        /// <param name="message">Additional message</param>
        /// <returns>:ref:`Message ID` of the sent files</returns>
        public async Task<string> sendLocalVoiceClips(Dictionary<string, Stream> clip_paths = null, FB_Message message = null)
        {
            /*
             * Sends local voice clips to a thread
             * :param file_paths: Paths of files to upload and send
             * :param message: Additional message
             * :return: :ref:`Message ID <intro_message_ids>` of the sent files
             * :raises: FBchatException if request failed
             */

            var files = await this.session._upload(this.session.get_files_from_paths(clip_paths), voice_clip: true);
            return await this._sendFiles(files: files, message: message);
        }

        /// <summary>
        /// Sends an image to a thread
        /// </summary>
        [Obsolete("Deprecated.")]
        public async Task<string> sendImage(string image_id = null, FB_Message message = null, bool is_gif = false)
        {
            string mimetype = null;
            if (!is_gif)
                mimetype = "image/png";
            else
                mimetype = "image/gif";

            return await this._sendFiles(
                files: new List<Tuple<string, string>>() { new Tuple<string, string>(image_id, mimetype) },
                message: message);
        }

        /// <summary>
        /// Sends an image from a URL to a thread
        /// </summary>
        /// <param name="image_url"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.sendRemoteFiles` instead")]
        public async Task<string> sendRemoteImage(string image_url = null, FB_Message message = null)
        {
            /*
             * Sends an image from a URL to a thread
             * : param image_url: URL of an image to upload and send
             * :param message: Additional message
             * :return: :ref:`Message ID<intro_message_ids>` of the sent image
             * :raises: FBchatException if request failed
             */

            return await this.sendRemoteFiles(
                file_urls: new List<string>() { image_url },
                message: message);
        }

        /// <summary>
        /// Sends a local image to a thread
        /// </summary>
        /// <param name="image_path"></param>
        /// <param name="data"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.sendLocalFiles` instead")]
        public async Task<string> sendLocalImage(string image_path = null, Stream data = null, FB_Message message = null)
        {
            /*
             * Sends a local image to a thread
             * : param image_path: Path of an image to upload and send
             * :param message: Additional message
             * :return: :ref:`Message ID<intro_message_ids>` of the sent image
             * :raises: FBchatException if request failed
             */

            return await this.sendLocalFiles(
                file_paths: new Dictionary<string, Stream>() { { image_path, data } },
                message: message);
        }
    }
}
