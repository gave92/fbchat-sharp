using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Somebody reacted to a message.
    /// </summary>
    public class FB_ReactionEvent : FB_ThreadEvent
    {
        /// Message that the user reacted to
        public FB_Message message { get; set; }
        /// The reaction.
        /// Not limited to the ones in `FB_Message_Constants.SENDABLE_REACTIONS`.
        /// If `null`, the reaction was removed.
        public string reaction { get; set; }

        internal static FB_ReactionEvent _parse(Session session, JToken data)
        {
            var thread = FB_ReactionEvent._get_thread(session, data);
            return new FB_ReactionEvent()
            {
                author = new FB_User(session: session, uid: data?.get("userId")?.Value<string>()),
                thread = thread,
                message = new FB_Message(session: session, thread_id: thread.uid, uid: data?.get("messageId")?.Value<string>()),
                reaction = data?.get("action")?.Value<int>() == 0 ? data?.get("reaction")?.Value<string>() : null
            };
        }
    }

    /// <summary>
    /// Whether the user was blocked or unblocked
    /// </summary>
    public class FB_UserStatusEvent : FB_ThreadEvent
    {
        /// Whether the user was blocked or unblocked
        public bool blocked { get; set; }

        internal static FB_UserStatusEvent _parse(Session session, JToken data)
        {
            return new FB_UserStatusEvent()
            {
                author = new FB_User(session: session, uid: data?.get("actorFbid")?.Value<string>()),
                thread = _get_thread(session, data),
                blocked = !(data?.get("canViewerReply")?.Value<bool>() ?? true),
            };
        }
    }

    /// <summary>
    /// Somebody sent live location info.
    /// </summary>
    public class FB_LiveLocationEvent : FB_ThreadEvent
    {
        /// TODO: This!

        internal static FB_LiveLocationEvent _parse(Session session, JToken data)
        {
            var thread = FB_LiveLocationEvent._get_thread(session, data);

            foreach (var location_data in data?.get("messageLiveLocations") ?? Enumerable.Empty<JToken>())
            {
                var message = new FB_Message(session: session, thread_id: thread.uid, uid: data?.get("messageId")?.Value<string>());
                var author = new FB_User(session: session, uid: location_data?.get("senderId")?.Value<string>());
                var location = FB_LiveLocationAttachment._from_pull(location_data);
            }

            return null;
        }
    }

    /// <summary>
    /// Somebody unsent a message (which deletes it for everyone).
    /// </summary>
    public class FB_UnsendEvent : FB_ThreadEvent
    {
        /// The unsent message
        public FB_Message message { get; set; }
        /// When the message was unsent
        public long at { get; set; }

        internal static FB_UnsendEvent _parse(Session session, JToken data)
        {
            var thread = FB_UnsendEvent._get_thread(session, data);
            long.TryParse(data?.get("deletionTimestamp")?.Value<string>(), out long at);

            return new FB_UnsendEvent()
            {
                author = new FB_User(session: session, uid: data?.get("senderID")?.Value<string>()),
                thread = thread,
                message = new FB_Message(session: session, thread_id: thread.uid, uid: data?.get("messageID")?.Value<string>()),
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody replied to a message.
    /// </summary>
    public class FB_MessageReplyEvent : FB_ThreadEvent
    {
        /// The sent message
        public FB_Message message { get; set; }
        /// he message that was replied to
        public FB_Message replied_to { get; set; }

        internal static FB_MessageReplyEvent _parse(Session session, JToken data)
        {
            var thread = FB_MessageReplyEvent._get_thread(session, data);
            var metadata = data?.get("message")?.get("messageMetadata");

            return new FB_MessageReplyEvent()
            {
                author = new FB_User(session: session, uid: metadata?.get("actorFbId")?.Value<string>()),
                thread = thread,
                message = FB_Message._from_reply(data?.get("message"), thread),
                replied_to = FB_Message._from_reply(data?.get("repliedToMessage"), thread),
            };
        }
    }

    internal class ClientPayload
    {
        public static IEnumerable<FB_Event> parse_client_payloads(Session session, JToken data)
        {
            var payload = JToken.Parse(string.Join("", data.get("payload")?.Select(x => x?.Value<int?>()?.ToString())));

            foreach (var d in payload?.get("deltas") ?? Enumerable.Empty<JToken>())
            {
                yield return parse_client_delta(session, d);
            }
        }

        private static FB_Event parse_client_delta(Session session, JToken data)
        {
            if (data?.get("deltaMessageReaction") != null)
                return FB_ReactionEvent._parse(session, data["deltaMessageReaction"]);
            else if (data?.get("deltaChangeViewerStatus") != null)
            {
                // TODO: Parse all `reason`
                if (data?.get("deltaChangeViewerStatus")?.get("reason")?.Value<int>() == 2)
                    return FB_UserStatusEvent._parse(session, data["deltaChangeViewerStatus"]);
            }
            else if (data?.get("liveLocationData") != null)
                return FB_LiveLocationEvent._parse(session, data["liveLocationData"]);
            else if (data?.get("deltaRecallMessageData") != null)
                return FB_UnsendEvent._parse(session, data["deltaRecallMessageData"]);
            else if (data?.get("deltaMessageReply") != null)
                return FB_MessageReplyEvent._parse(session, data["deltaMessageReply"]);
            return new FB_UnknownEvent() { source = "client payload", data = data };
        }
    }
}
