using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Somebody added people to a group thread.
    /// </summary>
    public class FB_PeopleAdded : FB_ThreadEvent
    {
        // TODO: Add message id

        /// Thread that the action was done in
        public new FB_Group thread { get; set; }
        /// The people who got added
        public List<FB_User> added { get; set; }
        /// When the people were added
        public long at { get; set; }

        internal static new FB_PeopleAdded _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_PeopleAdded._parse_metadata(session, data);
            var added = data?.get("addedParticipants")?.Select(x => new FB_User(x.get("userFbId")?.Value<string>(), session));

            return new FB_PeopleAdded()
            {
                author = author,
                thread = thread as FB_Group,
                added = added.ToList()
            };
        }
    }

    /// <summary>
    /// Somebody removed a person from a group thread.
    /// </summary>
    public class FB_PersonRemoved : FB_ThreadEvent
    {
        // TODO: Add message id

        /// Thread that the action was done in
        public new FB_Group thread { get; set; }
        /// The person who got removed
        public FB_User removed { get; set; }
        /// When the people were added
        public long at { get; set; }

        internal static new FB_PersonRemoved _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_PersonRemoved._parse_metadata(session, data);
            var removed = new FB_User(data?.get("leftParticipantFbId")?.Value<string>(), session);

            return new FB_PersonRemoved()
            {
                author = author,
                thread = thread as FB_Group,
                removed = removed
            };
        }
    }

    /// <summary>
    /// Somebody changed a group's title.
    /// </summary>
    public class FB_TitleSet : FB_ThreadEvent
    {
        // TODO: Add message id

        /// Thread that the action was done in
        public new FB_Group thread { get; set; }
        /// The new title
        public string title { get; set; }
        /// When the title was set
        public long at { get; set; }

        internal static new FB_TitleSet _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_TitleSet._parse_metadata(session, data);

            return new FB_TitleSet()
            {
                author = author,
                thread = thread as FB_Group,
                title = data?.get("name")?.Value<string>()
            };
        }
    }

    /// <summary>
    /// A message was received, but the data must be fetched manually.
    /// Use `Message.fetch` to retrieve the message data.
    /// This is usually used when somebody changes the group's photo, or when a new pending
    /// group is created.
    /// </summary>
    public class FB_UnfetchedThreadEvent : FB_Event
    {
        // TODO: Present this in a way that users can fetch the changed group photo easily

        /// The thread the message was sent to
        public FB_Thread thread { get; set; }
        /// The message
        public FB_Message message { get; set; }

        internal static new FB_UnfetchedThreadEvent _parse(Session session, JToken data)
        {
            var thread = FB_ThreadEvent._get_thread(session, data);
            var message = new FB_Message(session, thread_id: thread.uid, uid: data?.get("messageId")?.Value<string>());
            return new FB_UnfetchedThreadEvent()
            {
                thread = thread,
                message = message
            };
        }
    }

    /// <summary>
    /// Somebody marked messages as delivered in a thread.
    /// </summary>
    public class FB_MessagesDelivered : FB_ThreadEvent
    {
        /// The messages that were marked as delivered
        public List<FB_Message> messages { get; set; }
        /// When the messages were delivered
        public long at { get; set; }

        internal static new FB_MessagesDelivered _parse(Session session, JToken data)
        {
            var author = new FB_User(data?.get("actorFbId")?.Value<string>(), session);
            var thread = FB_MessagesDelivered._get_thread(session, data);
            var messages = data?.get("messageIds")?.Select(x => new FB_Message(session, thread_id: thread.uid, uid: x?.Value<string>()));
            var at = long.Parse(data?.get("deliveredWatermarkTimestampMs")?.Value<string>());
            return new FB_MessagesDelivered()
            {
                author = author,
                thread = thread,
                messages = messages.ToList(),
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody marked threads as read/seen.
    /// </summary>
    public class FB_ThreadsRead : FB_Event
    {
        /// The person who marked the threads as read
        public FB_Thread author { get; set; }
        /// The threads that were marked as read
        public List<FB_Thread> threads { get; set; }
        /// When the threads were read
        public long at { get; set; }

        internal static FB_ThreadsRead _parse_read_receipt(Session session, JToken data)
        {
            var author = new FB_User(session: session, uid: data?.get("actorFbId")?.Value<string>());
            var thread = FB_ThreadEvent._get_thread(session, data);
            var at = long.Parse(data.get("actionTimestampMs")?.Value<string>());
            return new FB_ThreadsRead()
            {
                author = author,
                threads = new List<FB_Thread>() { thread },
                at = at
            };
        }

        internal static new FB_ThreadsRead _parse(Session session, JToken data)
        {
            var author = new FB_User(session.get_user_id(), session);
            var threads = data?.get("threadKeys")?.Select(x => FB_ThreadEvent._get_thread(session, new JObject() { { "threadKey", x } }));
            var at = long.Parse(data?.get("actionTimestamp")?.Value<string>());
            return new FB_ThreadsRead()
            {
                author = author,
                threads = threads.ToList(),
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody sent a message to a thread.
    /// </summary>
    public class FB_MessageEvent : FB_ThreadEvent
    {
        /// The sent message
        public FB_Message message { get; set; }
        /// When the message was sent
        public long at { get; set; }

        internal static new FB_MessageEvent _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_MessageEvent._parse_metadata(session, data);
            var message = FB_Message._from_pull(data, thread: thread, author: author.uid, timestamp: at.ToString());
            return new FB_MessageEvent()
            {
                author = author,
                thread = thread,
                message = message,
                at = at
            };
        }
    }

    internal class DeltaClass
    {
        public static FB_Event parse_delta(Session session, JToken data)
        {
            var class_ = data?.get("class")?.Value<string>();
            if (class_ == "ParticipantsAddedToGroupThread")
                return FB_PeopleAdded._parse(session, data);
            else if (class_ == "ParticipantLeftGroupThread")
                return FB_PersonRemoved._parse(session, data);
            else if (class_ == "MarkFolderSeen")
            {
                // TODO: Finish this
                var folders = data.get("folders")?.Select(folder =>
                    folder?.Value<string>().Replace("FOLDER_", ""));
                var at = long.Parse(data?.get("timestamp")?.Value<string>());
                return null;
            }
            else if (class_ == "ThreadName")
                return FB_TitleSet._parse(session, data);
            else if (class_ == "ForcedFetch")
                return FB_UnfetchedThreadEvent._parse(session, data);
            else if (class_ == "DeliveryReceipt")
                return FB_MessagesDelivered._parse(session, data);
            else if (class_ == "ReadReceipt")
                return FB_ThreadsRead._parse_read_receipt(session, data);
            else if (class_ == "MarkRead")
                return FB_ThreadsRead._parse(session, data);
            else if (class_ == "NoOp")
            {
                // Skip "no operation" events
                return null;
            }
            else if (class_ == "ClientPayload")
                throw new FBchatParseError("This is implemented in `parse_events`");
            else if (class_ == "NewMessage")
                return FB_MessageEvent._parse(session, data);
            return new FB_UnknownEvent() { source = "Delta class", data = data };
        }
    }
}
