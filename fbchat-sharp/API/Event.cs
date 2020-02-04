using Newtonsoft.Json.Linq;
using System;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Base class for all events.
    /// </summary>
    public class FB_Event
    {
        public static FB_Event _parse(Session session, JToken data)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Represent an unknown event.
    /// </summary>
    public class FB_UnknownEvent : FB_Event
    {
        /// The unknown data. This cannot be relied on, it's only for debugging purposes.
        JToken data { get; set; }

        public static FB_UnknownEvent _parse(Session session, JToken data)
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
        FB_User author { get; set; }
        /// Thread that the action was done in
        FB_Thread thread { get; set; }

        public static FB_Thread _get_thread(Session session, JToken data)
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
}
