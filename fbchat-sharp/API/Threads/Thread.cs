using Dasync.Collections;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
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

        internal static string _from_graphql(JToken data)
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
        /// The session to use when making requests
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

        internal static FB_Thread _from_metadata(JToken msg_metadata, Session session)
        {
            /*Returns a tuple consisting of thread ID and thread type*/
            string id_thread = null;
            if (msg_metadata.get("threadKey")?.get("threadFbId") != null)
            {
                id_thread = (msg_metadata.get("threadKey")?.get("threadFbId").Value<string>());
                return new FB_Group(id_thread, session);
            }
            else if (msg_metadata.get("threadKey")?.get("otherUserFbId") != null)
            {
                id_thread = (msg_metadata.get("threadKey")?.get("otherUserFbId").Value<string>());
                return new FB_User(id_thread, session);
            }
            return new FB_User(id_thread, session);
        }

        internal static IEnumerable<FB_Thread> _parse_participants(JToken data, Session session)
        {
            foreach (var node in data?.get("nodes"))
            {
                var actor = node?.get("messaging_actor");
                var typename = actor?.get("__typename")?.Value<string>();
                var thread_id = actor?.get("id")?.Value<string>();

                if (typename == "User")
                    yield return new FB_User(session: session, uid: thread_id);
                else if (typename == "MessageThread")
                    // MessageThread => Group thread
                    yield return new FB_Group(session: session, uid: thread_id);
                else if (typename == "Page")
                    yield return new FB_Page(session: session, uid: thread_id);
                else if (typename == "Group")
                    // We don't handle Facebook "Groups"
                    continue;
                else
                    Debug.WriteLine($"Unknown type {typename} in {data.ToString()}");
            }
        }

        internal static Dictionary<string, object> _parse_customization_info(JToken data)
        {
            var rtn = new Dictionary<string, object>();
            if (data == null || data.get("customization_info") == null)
                return rtn;
            var info = data.get("customization_info");
            rtn["emoji"] = info.get("emoji");
            rtn["color"] = ThreadColor._from_graphql(info.get("outgoing_bubble_color"));

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

        internal virtual Dictionary<string, object> _to_send_data()
        {
            // TODO: Only implement this in subclasses
            return new Dictionary<string, object>() { { "other_user_fbid", this.uid } };
        }

        internal async Task<JToken> _forcedFetch(string mid)
        {
            var param = new Dictionary<string, object>() { { "thread_and_message_id", new Dictionary<string, object>() { { "thread_id", this.uid }, { "message_id", mid } } } };
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
            return await this.send(new FB_Message(session: this.session, text: message));
        }

        /// <summary>
        /// Sends a message to a thread
        /// </summary>
        [Obsolete("Deprecated. Use :func:`fbchat.Client.send` instead")]
        public async Task<string> sendEmoji(string emoji = null, EmojiSize size = EmojiSize.SMALL)
        {
            return await this.send(new FB_Message(session: this.session, text: emoji, emoji_size: size));
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
                    new FB_Message(session: this.session, text: ((FB_QuickReplyText)quick_reply).title, quick_replies: new List<FB_QuickReply>() { quick_reply })
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
                return await this.send(new FB_Message(session: this.session, text: payload, quick_replies: new List<FB_QuickReply>() { quick_reply }));
            }
            else if (quick_reply is FB_QuickReplyPhoneNumber)
            {
                //if (payload == null)
                //    payload = (await this.getPhoneNumbers())[0];
                quick_reply.external_payload = quick_reply.payload;
                quick_reply.payload = payload;
                return await this.send(new FB_Message(session: this.session, text: payload, quick_replies: new List<FB_QuickReply>() { quick_reply }));
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
            List<(string mimeKey, string fileType)> files, FB_Message message = null)
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
                data[string.Format("{0}s[{1}]", Utils.mimetype_to_key(obj.f.fileType), obj.i)] = obj.f.mimeKey;

            return await this.session._do_send_request(data);
        }

        private async Task<(int count, IEnumerable<FB_Message_Snippet> snippets)> _search_messages(string query, int offset = 0, int limit = 5)
        {
            var data = new Dictionary<string, object>() {
                { "query", query },
                { "snippetOffset", offset.ToString() },
                { "snippetLimit", limit.ToString() },
                { "identifier", "thread_fbid"},
                { "thread_fbid", this.uid} };
            var j = await this.session._payload_post("/ajax/mercury/search_snippets.php?dpr=1", data);

            var result = j.get("search_snippets")?.get(query)?.get(this.uid);
            if (result == null) return (0, null);

            // TODO: May or may not be a good idea to attach the current thread?
            var snippets = result.get("snippets")?.Select((snippet) => FB_Message_Snippet._parse(snippet, this));
            return (result["num_total_snippets"]?.Value<int>() ?? 0, snippets);
        }

        /// <summary>
        /// Find and get`FB_Message_Snippet` objects by query
        /// </summary>
        /// <param name="query">Text to search for</param>
        /// <param name="offset">Number of messages to skip</param>
        /// <param name="limit">Max.number of messages to retrieve</param>
        /// <returns>Found `FB_Message` objects</returns>
        public IAsyncEnumerable<FB_Message_Snippet> searchMessages(string query, int offset = 0, int limit = 5)
        {
            /*
             * Find and get`FB_Message_Snippet` objects by query
             * ..warning::
             * Warning! If someone send a message to the thread that matches the query, while
             * we're searching, some snippets will get returned twice.
             * Not sure if we should handle it, Facebook's implementation doesn't...
             * :param query: Text to search for
             * :param offset: Number of messages to skip
             * :param limit: Max.number of messages to retrieve
             * :type offset: int
             * :type limit: int
             * :return: Found `FB_Message_Snippet` objects
             * :rtype: typing.Iterable
             * :raises: FBchatException if request failed
             * */

            // TODO: fbchat simplifies iteration calculating the offset + yield return
            return new AsyncEnumerable<FB_Message_Snippet>(async yield =>
            {
                var message_snippets = await this._search_messages(
                    query, offset: offset, limit: limit
                );
                foreach (var snippet in message_snippets.snippets)
                    await yield.ReturnAsync(snippet);
            });
        }

        /// <summary>
        /// Get the last messages in a thread
        /// </summary>
        /// <param name="limit">Max.number of messages to retrieve</param>
        /// <param name="before">A unix timestamp, indicating from which point to retrieve messages</param>
        /// <returns></returns>
        public async Task<List<FB_Message>> fetchMessages(int limit = 20, string before = null)
        {
            /*
             * Get the last messages in a thread
             * :param limit: Max.number of messages to retrieve
             * : param before: A timestamp, indicating from which point to retrieve messages
             * :type limit: int
             * :type before: int
             * :return: `models.Message` objects
             * :rtype: list
             * :raises: Exception if request failed
             */

            var dict = new Dictionary<string, object>() {
                { "id", this.uid},
                { "message_limit", limit},
                { "load_messages", true},
                { "load_read_receipts", false},
                // "load_delivery_receipts": False,
                // "is_work_teamwork_not_putting_muted_in_unreads": False,
                { "before", before }
            };

            var j = await this.session.graphql_request(GraphQL.from_doc_id(doc_id: "1860982147341344", param: dict));

            if (j.get("message_thread") == null)
            {
                throw new FBchatException(string.Format("Could not fetch thread {0}", this.uid));
            }

            var read_receipts = j?.get("message_thread")?.get("read_receipts")?.get("nodes");
            var messages = j?.get("message_thread")?.get("messages")?.get("nodes")?.Select(message => 
                FB_Message._from_graphql(message, this, read_receipts))?.Reverse()?.ToList();

            return messages;
        }

        /// <summary>
        /// Creates generator object for fetching images posted in thread.
        /// </summary>
        /// <returns>`ImageAttachment` or `VideoAttachment`.</returns>
        public IAsyncEnumerable<(string cursor, FB_Attachment attachment)> fetchImages(int limit = 5, string after = null)
        {
            /*
             * Creates generator object for fetching images posted in thread.
             * :return: `ImageAttachment` or `VideoAttachment`.
             * :rtype: iterable
             * */
            return new AsyncEnumerable<(string cursor,FB_Attachment attachment)>(async yield =>
            {
                var data = new Dictionary<string, object>() {
                    { "id", this.uid },
                    { "limit", limit },
                    { "after", after }
                };

                var j = await this.session.graphql_request(GraphQL.from_query_id("515216185516880", data));
                if (j?.get(this.uid) == null)
                    throw new FBchatException("Could not find images");

                var result = j.get(this.uid).get("message_shared_media");

                foreach (var edge in result?.get("edges"))
                {
                    var node = edge?.get("node");
                    var type_ = node?.get("__typename");

                    if (type_?.Value<string>() == "MessageImage")
                    {
                        await yield.ReturnAsync((result?.get("page_info")?.get("end_cursor")?.Value<string>(), 
                            FB_ImageAttachment._from_list(node)));
                    }                        
                    else if (type_?.Value<string>() == "MessageVideo")
                    {
                        await yield.ReturnAsync((result?.get("page_info")?.get("end_cursor")?.Value<string>(), 
                            FB_VideoAttachment._from_list(node)));
                    }                        
                    else
                    {
                        Debug.WriteLine($"Unknown image type {type_}, data: {edge.ToString()}");
                        continue;
                    }
                }
            });
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
                files: new List<(string, string)>() { (image_id, mimetype) },
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

        /// <summary>
        /// Forwards an attachment
        /// </summary>
        /// <param name="attachment_id">Attachment ID to forward</param>
        /// <returns></returns>
        public async Task forwardAttachment(string attachment_id)
        {
            /*
             * Forwards an attachment
             * :param attachment_id: Attachment ID to forward
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>(){
                { "attachment_id", attachment_id },
                { string.Format("recipient_map[{0]",Utils.generateOfflineThreadingID()), this.uid }
            };
            var j = await this.session._payload_post("/mercury/attachments/forward/", data);
            if (j.get("success") == null)
                throw new FBchatFacebookError(
                string.Format("Failed forwarding attachment: {0}", j.get("error")),
                fb_error_message: j.get("error")?.Value<string>()
            );
        }

        /// <summary>
        /// Creates a group with the given ids
        /// </summary>
        /// <param name="message">The initial message</param>
        /// <param name="user_ids">A list of users to create the group with.</param>
        /// <returns>ID of the new group</returns>
        public async Task<string> createGroup(string message, List<string> user_ids)
        {
            /*
             * Creates a group with the given ids
             * :param message: The initial message
             * :param user_ids: A list of users to create the group with.
             * :return: ID of the new group
             * :raises: FBchatException if request failed
             * */
            var data = new FB_Message(message)._to_send_data();

            if (user_ids.Count < 2)
                throw new FBchatUserError("Error when creating group: Not enough participants");

            foreach (var obj in user_ids.Concat(new string[] { this.session.user.uid }).Select((x, index) => new { user_id = x, i = index }))
                data[string.Format("specific_to_list[{0}]", obj.i)] = string.Format("fbid:{0}", obj.user_id);

            var req = await this.session._do_send_request(data, get_thread_id: true);
            if (req.THR == null)
                throw new FBchatException(
                    "Error when creating group: No thread_id could be found"
                );
            return req.THR;
        }

        /// <summary>
        /// Changes title of a thread.
        /// If this is executed on a user thread, this will change the nickname of that user, effectively changing the title
        /// </summary>
        /// <param name="title">New group thread title</param>
        /// <returns></returns>
        public async Task changeTitle(string title)
        {
            /*
             * Changes title of a thread.
             * If this is executed on a user thread, this will change the nickname of that user, effectively changing the title
             * :param title: New group thread title
             * : raises: FBchatException if request failed
             * */
            if (this is FB_User)
                // The thread is a user, so we change the user's nickname
                await this.setNickname(title, this.uid);

            var data = new Dictionary<string, object>() { { "thread_name", title }, { "thread_id", this.uid } };
            var j = await this.session._payload_post("/messaging/set_thread_name/?dpr=1", data);
        }

        /// <summary>
        /// Changes the nickname of a user in a thread
        /// </summary>
        /// <param name="nickname">New nickname</param>
        /// <param name="user_id">User that will have their nickname changed</param>
        /// <returns></returns>
        public async Task setNickname(
            string nickname, string user_id
        )
        {
            /*
             * Changes the nickname of a user in a thread
             * :param nickname: New nickname
             * :param user_id: User that will have their nickname changed
             * : raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() {
                { "nickname", nickname },
                { "participant_id", user_id },
                { "thread_or_other_fbid", this.uid }
            };
            var j = await this.session._payload_post(
                "/messaging/save_thread_nickname/?source=thread_settings&dpr=1", data);
        }

        /// <summary>
        /// Changes thread color
        /// </summary>
        /// <param name="color">New thread color</param>
        /// <returns></returns>
        public async Task setColor(string color)
        {
            /*
             * Changes thread color
             * : param color: New thread color
             * :type color: ThreadColor
             * : raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() {
                { "color_choice", color != ThreadColor.MESSENGER_BLUE ? color : ""},
                { "thread_or_other_fbid", this.uid}
            };
            var j = await this.session._payload_post(
                "/messaging/save_thread_color/?source=thread_settings&dpr=1", data);
        }

        /// <summary>
        /// Changes thread color
        /// </summary>
        /// <param name="emoji">While changing the emoji, the Facebook web client actually sends multiple different requests, though only this one is required to make the change</param>
        /// <returns></returns>
        public async Task setEmoji(string emoji)
        {
            /*
             * Changes thread color
             * Trivia: While changing the emoji, the Facebook web client actually sends multiple different requests, though only this one is required to make the change
             * : param emoji: New thread emoji
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() { { "emoji_choice", emoji }, { "thread_or_other_fbid", this.uid } };
            var j = await this.session._payload_post(
                "/messaging/save_thread_emoji/?source=thread_settings&dpr=1", data);
        }

        /// <summary>
        /// Sets a plan
        /// </summary>
        /// <param name="name"></param>
        /// <param name="time"></param>
        /// <param name="location_name"></param>
        /// <param name="location_id"></param>
        /// <returns></returns>
        public async Task createPlan(string name, string time, string location_name = null, string location_id = null)
        {
            /*
             * Sets a plan
             * : param plan: Plan to set
             * :type plan: Plan
             * : raises: FBchatException if request failed
             * */
            await FB_Plan._create(this, name, time, location_name, location_id);
        }

        /// <summary>
        /// Creates poll in a group thread
        /// </summary>
        /// <param name="poll">Poll to create</param>
        /// <returns></returns>
        public async Task createPoll(FB_Poll poll)
        {
            /*
             * Creates poll in a group thread
             * : param poll: Poll to create
             * :type poll: Poll
             * : raises: FBchatException if request failed
             * */

            // We're using ordered dicts, because the Facebook endpoint that parses the POST
            // parameters is badly implemented, and deals with ordering the options wrongly.
            // If you can find a way to fix this for the endpoint, or if you find another
            // endpoint, please do suggest it ;)
            var data = new Dictionary<string, object>(){
                { "question_text", poll.title }, {"target_id", this.uid }};

            foreach (var obj in poll.options.Select((x, index) => new { option = x, i = index }))
            {
                data[string.Format("option_text_array[{0}]", obj.i)] = obj.option.text;
                data[string.Format("option_is_selected_array[{0}]", obj.i)] = (obj.option.vote ? 1 : 0).ToString();

                var j = await this.session._payload_post("/messaging/group_polling/create_poll/?dpr=1", data);
                if (j.get("status")?.Value<string>() != "success")
                    throw new FBchatFacebookError(
                        string.Format("Failed creating poll: {0}", j.get("errorTitle")),
                        fb_error_message: j.get("errorMessage")?.Value<string>()
                );
            }
        }

        /// <summary>
        /// Sets users typing status in a thread
        /// </summary>
        /// <param name="status">Specify the typing status</param>
        /// <returns></returns>
        public async Task setTypingStatus(bool status)
        {
            /*
             * Sets users typing status in a thread
             * :param status: Specify the typing status
             * :type status: TypingStatus
             * : raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() {
                { "typ", status ? 1 : 0 },
                { "thread", this.uid },
                { "to", this is FB_User ? this.uid : ""},
                {"source", "mercury-chat"}
            };
            var j = await this.session._payload_post("/ajax/messaging/typ.php", data);
        }

        /// <summary>
        /// Mark a thread as spam and delete it
        /// </summary>
        public async Task markAsSpam()
        {
            /*
             * Mark a thread as spam and delete it
             * :return: true
             * :raises: FBchatException if request failed
             * */
            var j = await this.session._payload_post("/ajax/mercury/mark_spam.php?dpr=1", new Dictionary<string, object>() { { "id", this.uid } });
        }

        /// <summary>
        /// Mutes thread
        /// </summary>
        /// <param name="mute_time">Mute time in seconds, leave blank to mute forever</param>
        /// <returns></returns>
        public async Task mute(int mute_time = -1)
        {
            /*
             * Mutes thread
             * :param mute_time: Mute time in seconds, leave blank to mute forever
             * */
            var data = new Dictionary<string, object> { { "mute_settings", mute_time.ToString() }, { "thread_fbid", this.uid } };
            var j = await this.session._payload_post("/ajax/mercury/change_mute_thread.php?dpr=1", data);
        }

        /// <summary>
        /// Unmutes thread
        /// </summary>
        /// <returns></returns>
        public async Task unmute(string thread_id = null)
        {
            /*
             * Unmutes thread
             * */
            await this.mute(0);
        }

        /// <summary>
        /// Mutes thread reactions
        /// </summary>
        /// <param name="mute">Boolean.true to mute, false to unmute</param>
        /// <returns></returns>
        public async Task muteReactions(bool mute = true)
        {
            /*
             * Mutes thread reactions
             * :param mute: Boolean.true to mute, false to unmute
             * :param thread_id: User/Group ID to mute.See :ref:`intro_threads`
             * */
            var data = new Dictionary<string, object> { { "reactions_mute_mode", mute ? 1 : 0 }, { "thread_fbid", this.uid } };
            var j = await this.session._payload_post(
                "/ajax/mercury/change_reactions_mute_thread/?dpr=1", data
            );
        }

        /// <summary>
        /// Unmutes thread reactions
        /// </summary>
        /// <returns>User/Group ID to unmute.See :ref:`intro_threads`</returns>
        public async Task unmuteReactions()
        {
            /*
             * Unmutes thread reactions
             * */
            await this.muteReactions(false);
        }

        /// <summary>
        /// Mutes thread mentions
        /// </summary>
        /// <param name="mute">Boolean.true to mute, false to unmute</param>
        /// <returns></returns>
        public async Task muteMentions(bool mute = true)
        {
            /*
             * Mutes thread mentions
             * :param mute: Boolean.true to mute, false to unmute
             * */
            var data = new Dictionary<string, object> { { "mentions_mute_mode", mute ? 1 : 0 }, { "thread_fbid", this.uid } };
            var j = await this.session._payload_post("/ajax/mercury/change_mentions_mute_thread/?dpr=1", data);
        }

        /// <summary>
        /// Unmutes thread mentions
        /// </summary>
        /// <returns></returns>
        public async Task unmuteMentions()
        {
            /*
             * Unmutes thread mentions
             * */
            await this.muteMentions(false);
        }
    }
}
