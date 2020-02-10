using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Base class for all events.
    /// </summary>
    public class FB_Event
    {

    }

    internal class EventCommon
    {
        private static IEnumerable<FB_Event> _parse_delta(Session session, JToken delta)
        {
            var class_ = delta.get("class")?.Value<string>();

            // Client payload (that weird numbers)
            if (class_ == "ClientPayload")
            {
                foreach (var ev in ClientPayload.parse_client_payloads(session, delta) ?? Enumerable.Empty<FB_Event>())
                {
                    yield return ev;
                }
            }
            else if (class_ == "AdminTextMessage")
            {
                var ev = DeltaType.parse_delta(session, delta);
                if (ev != null)
                    yield return ev;
            }
            else
            {
                var ev = DeltaClass.parse_delta(session, delta);
                if (ev != null)
                    yield return ev;
            }
        }

        public static IEnumerable<FB_Event> parse_events(Session session, string topic, JToken data)
        {
            // See Mqtt._configure_connect_options for information about these topics
            if (topic == "/t_ms")
            {
                if (data?.get("deltas") != null)
                {
                    foreach (var delta in data?.get("deltas"))
                    {
                        foreach (var ev in _parse_delta(session, delta))
                            yield return ev;
                    }
                }
            }
            else if (topic == "/thread_typing")
                yield return FB_TypingStatus._from_thread_typing(session, data);
            else if (topic == "/orca_typing_notifications")
                yield return FB_TypingStatus._from_orca(session, data);
            else if (topic == "/legacy_web")
            {
                if (data.get("type")?.Value<string>() == "jewel_requests_add")
                    yield return FB_FriendRequest._parse(session, data);
                else
                    yield return new FB_UnknownEvent() { source = "/legacy_web", data = data };
            }
            else if (topic == "/orca_presence")
                yield return FB_Presence._parse(session, data);
            else
                yield return new FB_UnknownEvent() { source = topic, data = data };
        }
    }

    /// <summary>
    /// Represent an unknown event.
    /// </summary>
    public class FB_UnknownEvent : FB_Event
    {
        /// Some data describing the unknown event's origin
        public string source { get; set; }
        /// The unknown data. This cannot be relied on, it's only for debugging purposes.
        public JToken data { get; set; }

        internal static FB_UnknownEvent _parse(Session session, JToken data)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Represent an event that was done by a user/page in a thread.
    /// </summary>
    public class FB_ThreadEvent : FB_Event
    {
        /// The person who did the action
        public FB_User author { get; set; }
        /// Thread that the action was done in
        public FB_Thread thread { get; set; }

        internal static (FB_User author, long at) _parse_fetch(Session session, JToken data)
        {
            var author = new FB_User(session: session, uid: data?.get("message_sender")?.get("id")?.Value<string>());
            var at = long.Parse(data?.get("timestamp_precise")?.Value<string>());
            return (author, at);
        }

        internal static (FB_User author, FB_Thread thread, long at) _parse_metadata(Session session, JToken data)
        {
            var metadata = data?.get("messageMetadata");
            var author = new FB_User(session: session, uid: metadata?.get("actorFbId")?.Value<string>());
            var thread = FB_ThreadEvent._get_thread(session, metadata);
            var at = long.Parse(metadata?.get("timestamp")?.Value<string>());
            return (author, thread, at);
        }

        internal static FB_Thread _get_thread(Session session, JToken data)
        {
            // TODO: Handle pages? Is it even possible?
            var key = data?.get("threadKey");

            if (key?.get("threadFbId") != null)
            {
                return new FB_Group(session: session, uid: key["threadFbId"].Value<string>());
            }
            else if (key?.get("otherUserFbId") != null)
            {
                return new FB_User(session: session, uid: key["otherUserFbId"].Value<string>());
            }
            throw new FBchatParseError("Could not find thread data", data: data);
        }
    }

    /// <summary>
    /// Somebody started/stopped typing in a thread.
    /// </summary>
    public class FB_TypingStatus : FB_ThreadEvent
    {
        /// ``True`` if the user started typing, ``False`` if they stopped
        public bool status { get; set; }

        internal static FB_TypingStatus _from_orca(Session session, JToken data)
        {
            var author = new FB_User(session: session, uid: data?.get("sender_fbid")?.Value<string>());
            var status = data?.get("state")?.Value<int>() == 1;
            return new FB_TypingStatus()
            {
                author = author,
                thread = author,
                status = status
            };
        }

        internal static FB_TypingStatus _from_thread_typing(Session session, JToken data)
        {
            var author = new FB_User(session: session, uid: data?.get("sender_fbid")?.Value<string>());
            var thread = new FB_Group(session: session, uid: data?.get("thread")?.Value<string>());
            var status = data?.get("state")?.Value<int>() == 1;
            return new FB_TypingStatus()
            {
                author = author,
                thread = thread,
                status = status
            };
        }
    }

    /// <summary>
    /// Somebody sent a friend request.
    /// </summary>
    public class FB_FriendRequest : FB_Event
    {
        /// The user that sent the request
        public FB_User author { get; set; }

        internal static FB_FriendRequest _parse(Session session, JToken data)
        {
            var author = new FB_User(session: session, uid: data?.get("from")?.Value<string>());
            return new FB_FriendRequest()
            {
                author = author
            };
        }
    }

    /// <summary>
    /// The list of active statuses was updated.
    /// Chat online presence update.
    /// </summary>
    public class FB_Presence : FB_Event
    {
        /// The user that sent the request
        public Dictionary<string, FB_ActiveStatus> statuses { get; set; }
        /// ``True`` if the list is fully updated and ``False`` if it's partially updated
        public bool full { get; set; }

        internal static  FB_Presence _parse(Session session, JToken data)
        {
            var statuses = data.get("list").ToDictionary(x => x?.get("u")?.Value<string>(), x => FB_ActiveStatus._from_orca_presence(x));
            return new FB_Presence()
            {
                statuses = statuses,
                full = data?.get("list_type")?.Value<string>() == "full"
            };
        }
    }
}
