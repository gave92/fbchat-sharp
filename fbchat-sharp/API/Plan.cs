using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace fbchat_sharp.API
{
    public enum GuestStatus
    {
        INVITED = 1,
        GOING = 2,
        DECLINED = 3
    }

    /// <summary>
    /// 
    /// </summary>
    public class FB_Plan_Constants
    {
        public static readonly Dictionary<string, GuestStatus> GUESTS = new Dictionary<string, GuestStatus>() {
            { "INVITED", GuestStatus.INVITED },
            { "GOING", GuestStatus.GOING },
            { "DECLINED", GuestStatus.DECLINED },
        };
    }
    
    /// <summary>
    /// Represents a plan
    /// </summary>
    public class FB_Plan
    {
        /// The session to use when making requests
        public Session session { get; set; }
        /// ID of the plan
        public string uid { get; set; }
        /// Plan time (unix time stamp), only precise down to the minute
        public string time { get; set; }
        /// Plan title
        public string title { get; set; }
        /// Plan location name
        public string location { get; set; }
        /// Plan location ID
        public string location_id { get; set; }
        /// ID of the plan creator
        public string author_id { get; set; }
        /// Dict of `User` IDs mapped to their `GuestStatus`
        public Dictionary<string,GuestStatus> guests { get; set; }

        /// <summary>
        /// Represents a plan
        /// </summary>
        /// <param name="session"></param>
        /// <param name="uid"></param>        
        /// <param name="time"></param>
        /// <param name="title"></param>
        /// <param name="location"></param>
        /// <param name="location_id"></param>
        /// <param name="author_id"></param>
        /// <param name="guests"></param>
        public FB_Plan(Session session, string uid = null, string time = null, string title = null, string location = null, string location_id = null, string author_id = null, Dictionary<string, GuestStatus> guests = null)
        {
            this.session = session;
            this.uid = uid;
            this.time = time;
            this.title = title;
            this.location = location;
            this.location_id = location_id;
            this.author_id = author_id;
            this.guests = guests ?? new Dictionary<string, GuestStatus>();
        }

        /// List of the `User` IDs who will take part in the plan.
        public List<string> going
        {
            get
            {
                return this.guests.Where((g) => g.Value == GuestStatus.GOING).Select((g) => g.Key).ToList();
            }
        }

        /// List of the `User` IDs who won't take part in the plan.
        public List<string> declined
        {
            get
            {
                return this.guests.Where((g) => g.Value == GuestStatus.DECLINED).Select((g) => g.Key).ToList();
            }
        }

        /// List of the `User` IDs who are invited to the plan.
        public List<string> invited
        {
            get
            {
                return this.guests.Where((g) => g.Value == GuestStatus.INVITED).Select((g) => g.Key).ToList();
            }
        }

        public static FB_Plan _from_pull(JToken data, Session session)
        {
            FB_Plan rtn = new FB_Plan(
                session: session,
                time: data.get("event_time")?.Value<string>(),
                title: data.get("event_title")?.Value<string>(),
                location: data.get("event_location_name")?.Value<string>(),
                location_id: data.get("event_location_id")?.Value<string>()
            );
            rtn.uid = data.get("event_id")?.Value<string>();
            rtn.author_id = data.get("event_creator_id")?.Value<string>();
            rtn.guests = JToken.Parse(data.get("guest_state_list")?.Value<string>()).Select((x) => {
                return new { Key = x.get("node")?.get("id")?.Value<string>(), Value = FB_Plan_Constants.GUESTS[x.get("guest_list_state")?.Value<string>()] };
            }).ToDictionary(t => t.Key, t => t.Value);
            return rtn;
        }

        public static FB_Plan _from_fetch(JToken data, Session session)
        {
            FB_Plan rtn = new FB_Plan(
                session: session,
                time: data.get("event_time")?.Value<string>(),
                title: data.get("title")?.Value<string>(),
                location: data.get("location_name")?.Value<string>(),
                location_id: data.get("location_id")?.Value<string>()
            );
            rtn.uid = data.get("oid")?.Value<string>();
            rtn.author_id = data.get("creator_id")?.Value<string>();
            rtn.guests = data.get("event_members")?.Value<JObject>().Properties().Select((x) => {
                return new { Key = x.Name, Value = FB_Plan_Constants.GUESTS[x.Value?.Value<string>()] };
            }).ToDictionary(t => t.Key, t => t.Value);
            return rtn;
        }

        public static FB_Plan _from_graphql(JToken data, Session session)
        {
            FB_Plan rtn = new FB_Plan(
                session: session,
                time: data.get("time")?.Value<string>(),
                title: data.get("event_title")?.Value<string>(),
                location: data.get("location_name")?.Value<string>()
            );
            rtn.uid = data.get("id")?.Value<string>();
            rtn.author_id = data.get("lightweight_event_creator")?.get("id")?.Value<string>();
            rtn.guests = data.get("event_reminder_members")?.get("edges")?.Select((x) => {
                return new { Key = x.get("node")?.get("id") ?.Value<string>(), Value = FB_Plan_Constants.GUESTS[x.get("guest_list_state")?.Value<string>()] };
            }).ToDictionary(t => t.Key, t => t.Value);
            return rtn;
        }
    }
}
