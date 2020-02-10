using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Somebody set the color in a thread.
    /// </summary>
    public class FB_ColorSet : FB_ThreadEvent
    {
        /// The new color. Not limited to the ones in `ThreadABC.set_color`
        public string color { get; set; }
        /// When the color was set
        public long at { get; set; }

        internal static FB_ColorSet _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_ColorSet._parse_metadata(session, data);
            var color = ThreadColor._from_graphql(data?.get("untypedData")?.get("theme_color"));
            return new FB_ColorSet()
            {
                author = author,
                thread = thread,
                color = color,
                at = at
            };
        }

        internal static FB_ColorSet _from_fetch(FB_Thread thread, JToken data)
        {
            (FB_User author, long at) = FB_ColorSet._parse_fetch(thread.session, data);
            var color = ThreadColor._from_graphql(data?.get("extensible_message_admin_text")?.get("theme_color"));
            return new FB_ColorSet()
            {
                author = author,
                thread = thread,
                color = color,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody set the emoji in a thread.
    /// </summary>
    public class FB_EmojiSet : FB_ThreadEvent
    {
        /// The new emoji. If ``None``, the emoji was reset to the default "LIKE" icon
        public string emoji { get; set; }
        /// When the emoji was set
        public long at { get; set; }

        internal static FB_EmojiSet _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_EmojiSet._parse_metadata(session, data);
            var emoji = data?.get("untypedData")?.get("thread_icon")?.Value<string>();
            return new FB_EmojiSet()
            {
                author = author,
                thread = thread,
                emoji = emoji,
                at = at
            };
        }

        internal static FB_EmojiSet _from_fetch(FB_Thread thread, JToken data)
        {
            (FB_User author, long at) = FB_EmojiSet._parse_fetch(thread.session, data);
            var emoji = data?.get("extensible_message_admin_text")?.get("thread_icon")?.Value<string>();
            return new FB_EmojiSet()
            {
                author = author,
                thread = thread,
                emoji = emoji,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody set the nickname of a person in a thread.
    /// </summary>
    public class FB_NicknameSet : FB_ThreadEvent
    {
        /// The person whose nickname was set
        public FB_User subject { get; set; }
        /// The new nickname. If ``None``, the nickname was cleared
        public string nickname { get; set; }
        /// When the nickname was set
        public long at { get; set; }

        internal static FB_NicknameSet _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_NicknameSet._parse_metadata(session, data);
            var subject = new FB_User(data?.get("untypedData")?.get("participant_id")?.Value<string>(), session);
            var nickname = data?.get("untypedData")?.get("nickname")?.Value<string>();
            return new FB_NicknameSet()
            {
                author = author,
                thread = thread,
                subject = subject,
                nickname = nickname,
                at = at
            };
        }

        internal static FB_NicknameSet _from_fetch(FB_Thread thread, JToken data)
        {
            (FB_User author, long at) = FB_NicknameSet._parse_fetch(thread.session, data);
            var extra = data?.get("extensible_message_admin_text");
            var subject = new FB_User(extra?.get("participant_id")?.Value<string>(), thread.session);
            var nickname = extra?.get("nickname")?.Value<string>();
            return new FB_NicknameSet()
            {
                author = author,
                thread = thread,
                subject = subject,
                nickname = nickname,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody added admins to a group.
    /// </summary>
    public class FB_AdminsAdded : FB_ThreadEvent
    {
        /// The people that were set as admins
        public List<FB_User> added { get; set; }
        /// When the admins were added
        public long at { get; set; }

        internal static FB_AdminsAdded _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_AdminsAdded._parse_metadata(session, data);
            var target = new FB_User(data?.get("untypedData")?.get("TARGET_ID")?.Value<string>(), session);
            return new FB_AdminsAdded()
            {
                author = author,
                thread = thread,
                added = new List<FB_User>() { target },
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody removed admins from a group.
    /// </summary>
    public class FB_AdminsRemoved : FB_ThreadEvent
    {
        /// The people that were removed as admins
        public List<FB_User> removed { get; set; }
        /// When the admins were removed
        public long at { get; set; }

        internal static FB_AdminsRemoved _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_AdminsRemoved._parse_metadata(session, data);
            var target = new FB_User(data?.get("untypedData")?.get("TARGET_ID")?.Value<string>(), session);
            return new FB_AdminsRemoved()
            {
                author = author,
                thread = thread,
                removed = new List<FB_User>() { target },
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody changed the approval mode in a group.
    /// </summary>
    public class FB_ApprovalModeSet : FB_ThreadEvent
    {
        /// The new approval mode
        public bool require_admin_approval { get; set; }
        /// When the approval mode was set
        public long at { get; set; }

        internal static FB_ApprovalModeSet _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_ApprovalModeSet._parse_metadata(session, data);
            var approval_mode = long.Parse(data?.get("untypedData")?.get("APPROVAL_MODE")?.Value<string>()) != 0;
            return new FB_ApprovalModeSet()
            {
                author = author,
                thread = thread,
                require_admin_approval = approval_mode,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody started a call.
    /// </summary>
    public class FB_CallStarted : FB_ThreadEvent
    {
        /// When the call was started
        public long at { get; set; }

        internal static FB_CallStarted _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_CallStarted._parse_metadata(session, data);
            return new FB_CallStarted()
            {
                author = author,
                thread = thread,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody ended a call.
    /// </summary>
    public class FB_CallEnded : FB_ThreadEvent
    {
        /// How long the call took
        public long duration { get; set; }
        /// When the call ended
        public long at { get; set; }

        internal static FB_CallEnded _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_CallEnded._parse_metadata(session, data);
            long call_duration = long.Parse(data?.get("untypedData")?.get("call_duration")?.Value<string>());
            return new FB_CallEnded()
            {
                author = author,
                thread = thread,
                duration = call_duration,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody joined a call.
    /// </summary>
    public class FB_CallJoined : FB_ThreadEvent
    {
        /// When the call was joined
        public long at { get; set; }

        internal static FB_CallJoined _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_CallJoined._parse_metadata(session, data);
            return new FB_CallJoined()
            {
                author = author,
                thread = thread,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody created a group poll.
    /// </summary>
    public class FB_PollCreated : FB_ThreadEvent
    {
        /// The new poll
        public FB_Poll poll { get; set; }
        /// When the pall was created
        public long at { get; set; }

        internal static FB_PollCreated _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_PollCreated._parse_metadata(session, data);
            var poll_json = JToken.Parse(data?.get("untypedData")?.get("question_json")?.Value<string>());
            var poll = FB_Poll._from_graphql(poll_json, session);
            return new FB_PollCreated()
            {
                author = author,
                thread = thread,
                poll = poll,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody voited in a group poll.
    /// </summary>
    public class FB_PollVoted : FB_ThreadEvent
    {
        /// The updated poll
        public FB_Poll poll { get; set; }
        /// Ids of the voted options
        public List<string> added_ids { get; set; }
        /// Ids of the un-voted options
        public List<string> removed_ids { get; set; }
        /// When the pall was voted in
        public long at { get; set; }

        internal static FB_PollVoted _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_PollVoted._parse_metadata(session, data);
            var poll_json = JToken.Parse(data?.get("untypedData")?.get("question_json")?.Value<string>());
            var poll = FB_Poll._from_graphql(poll_json, session);
            var added_options = JToken.Parse(data?.get("untypedData")?.get("added_option_ids")?.Value<string>());
            var removed_options = JToken.Parse(data?.get("untypedData")?.get("removed_option_ids")?.Value<string>());
            return new FB_PollVoted()
            {
                author = author,
                thread = thread,
                poll = poll,
                added_ids = added_options.Select(x => x.ToString()).ToList(),
                removed_ids = removed_options.Select(x => x.ToString()).ToList(),
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody created plan in a group.
    /// </summary>
    public class FB_PlanCreated : FB_ThreadEvent
    {
        /// The new plan
        public FB_Plan plan { get; set; }
        /// When the plan was created
        public long at { get; set; }

        internal static FB_PlanCreated _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_PlanCreated._parse_metadata(session, data);
            var plan = FB_Plan._from_pull(data?.get("untypedData"), session);
            return new FB_PlanCreated()
            {
                author = author,
                thread = thread,
                plan = plan,
                at = at
            };
        }
    }

    /// <summary>
    /// A plan ended.
    /// </summary>
    public class FB_PlanEnded : FB_ThreadEvent
    {
        /// The ended plan
        public FB_Plan plan { get; set; }
        /// When the plan ended
        public long at { get; set; }

        internal static FB_PlanEnded _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_PlanEnded._parse_metadata(session, data);
            var plan = FB_Plan._from_pull(data?.get("untypedData"), session);
            return new FB_PlanEnded()
            {
                author = author,
                thread = thread,
                plan = plan,
                at = at
            };
        }
    }

    /// <summary>
    /// A plan was updated.
    /// </summary>
    public class FB_PlanEdited : FB_ThreadEvent
    {
        /// The updated plan
        public FB_Plan plan { get; set; }
        /// When the plan was updated
        public long at { get; set; }

        internal static FB_PlanEdited _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_PlanEdited._parse_metadata(session, data);
            var plan = FB_Plan._from_pull(data?.get("untypedData"), session);
            return new FB_PlanEdited()
            {
                author = author,
                thread = thread,
                plan = plan,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody removed a plan in a group.
    /// </summary>
    public class FB_PlanDeleted : FB_ThreadEvent
    {
        /// The removed plan
        public FB_Plan plan { get; set; }
        /// When the plan was removed
        public long at { get; set; }

        internal static FB_PlanDeleted _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_PlanDeleted._parse_metadata(session, data);
            var plan = FB_Plan._from_pull(data?.get("untypedData"), session);
            return new FB_PlanDeleted()
            {
                author = author,
                thread = thread,
                plan = plan,
                at = at
            };
        }
    }

    /// <summary>
    /// Somebody responded to a plan in a group.
    /// </summary>
    public class FB_PlanResponded : FB_ThreadEvent
    {
        /// The plan that was responded to
        public FB_Plan plan { get; set; }
        /// Whether the author will go to the plan or not
        public bool take_part { get; set; }
        /// When the user responded
        public long at { get; set; }

        internal static FB_PlanResponded _parse(Session session, JToken data)
        {
            (FB_User author, FB_Thread thread, long at) = FB_PlanResponded._parse_metadata(session, data);
            var plan = FB_Plan._from_pull(data?.get("untypedData"), session);
            var take_part = data.get("untypedData")?.get("guest_status")?.Value<string>() == "GOING";
            return new FB_PlanResponded()
            {
                author = author,
                thread = thread,
                plan = plan,
                take_part = take_part,
                at = at
            };
        }
    }

    internal class DeltaType
    {
        public static FB_Event parse_delta(Session session, JToken data)
        {
            var type_ = data?.get("type")?.Value<string>();
            if (type_ == "change_thread_theme")
                return FB_ColorSet._parse(session, data);
            else if (type_ == "change_thread_icon")
                return FB_EmojiSet._parse(session, data);
            else if (type_ == "change_thread_nickname")
                return FB_NicknameSet._parse(session, data);
            else if (type_ == "change_thread_admins")
            {
                var event_type = data?.get("untypedData")?.get("ADMIN_EVENT")?.Value<string>();
                if (event_type == "add_admin")
                    return FB_AdminsAdded._parse(session, data);
                else if (event_type == "remove_admin")
                    return FB_AdminsRemoved._parse(session, data);
            }
            else if (type_ == "change_thread_approval_mode")
                return FB_ApprovalModeSet._parse(session, data);
            else if (type_ == "instant_game_update")
            {
                // TODO: This
            }
            else if (type_ == "messenger_call_log")  // Previously "rtc_call_log"
            {
                var event_type = data?.get("untypedData")?.get("event")?.Value<string>();
                if (event_type == "group_call_started")
                    return FB_CallStarted._parse(session, data);
                else if (new string[] { "group_call_ended", "one_on_one_call_ended" }.Contains(event_type))
                    return FB_CallEnded._parse(session, data);
            }
            else if (type_ == "participant_joined_group_call")
                return FB_CallJoined._parse(session, data);
            else if (type_ == "group_poll")
            {
                var event_type = data?.get("untypedData")?.get("event_type")?.Value<string>();
                if (event_type == "question_creation")
                    return FB_PollCreated._parse(session, data);
                else if (event_type == "update_vote")
                    return FB_PollVoted._parse(session, data);
            }
            else if (type_ == "lightweight_event_create")
                return FB_PlanCreated._parse(session, data);
            else if (type_ == "lightweight_event_notify")
                return FB_PlanEnded._parse(session, data);
            else if (type_ == "lightweight_event_update")
                return FB_PlanEdited._parse(session, data);
            else if (type_ == "lightweight_event_delete")
                return FB_PlanDeleted._parse(session, data);
            else if (type_ == "lightweight_event_rsvp")
                return FB_PlanResponded._parse(session, data);
            return new FB_UnknownEvent() { source = "Delta type", data = data };
        }
    }
}
