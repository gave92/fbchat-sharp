using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace fbchat_sharp.API
{
    public class GraphQL : Dictionary<string, object>
    {
        public static string queries_to_json(List<GraphQL> queries)
        {
            /*
             * Queries should be a list of GraphQL objects
             */
            var rtn = new Dictionary<string, object>();
            foreach (var obj in queries.Select((x, i) => new { query = x, index = i }))
                rtn[string.Format("q{0}", obj.index)] = obj.query;
            return JsonConvert.SerializeObject(rtn);
        }

        public static List<JToken> response_to_json(string content)
        {
            content = Utils.strip_json_cruft(content); // Usually only needed in some error cases
            var rtn = new List<JToken>();

            using (var jsonReader = new JsonTextReader(new StringReader(content)) { CloseInput = false, SupportMultipleContent = true })
            {
                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType == JsonToken.Comment)
                        continue;
                    var x = JToken.ReadFrom(jsonReader);
                    if (x.get("error_results") != null)
                    {
                        continue;
                    }
                    Utils.handle_payload_error(x);
                    // string key = x.Value<JObject>().Properties().Where(k => k.Name.StartsWith("q")).First().Name;
                    string key = x.Value<JObject>().Properties().First().Name;
                    JToken value = x[key];
                    Utils.handle_graphql_errors(value);
                    if (value.get("response") != null)
                        rtn.Insert(Math.Min(rtn.Count, int.Parse(key.Substring(1))), value.get("response"));
                    else
                        rtn.Insert(Math.Min(rtn.Count, int.Parse(key.Substring(1))), value.get("data"));
                }
            }

            return rtn;
        }

        public static GraphQL from_query(string query = null, Dictionary<string, object> param = null)
        {
            return new GraphQL() {
                { "priority", 0},
                { "q", query},
                { "query_params", param } };
        }

        public static GraphQL from_query_id(string query_id = null, Dictionary<string, object> param = null)
        {
            return new GraphQL() {
                { "query_id", query_id},
                { "query_params", param } };
        }

        public static GraphQL from_doc(string doc = null, Dictionary<string, object> param = null)
        {
            return new GraphQL() {
                { "doc", doc},
                { "query_params", param } };
        }

        public static GraphQL from_doc_id(string doc_id = null, Dictionary<string, object> param = null)
        {
            return new GraphQL() {
                { "doc_id", doc_id},
                { "query_params", param } };
        }

        public static string FRAGMENT_USER = @"
QueryFragment User: User {
    id,
    name,
    first_name,
    last_name,
    profile_picture.width(<pic_size>).height(<pic_size>) {
        uri
    },
    is_viewer_friend,
    url,
    gender,
    viewer_affinity
}
";

        public static string FRAGMENT_GROUP = @"
QueryFragment Group: MessageThread {
    name,
    thread_key {
        thread_fbid
    },
    image {
        uri
    },
    is_group_thread,
    all_participants {
        nodes {
            messaging_actor {
                __typename,
                id
            }
        }
    },
    customization_info {
        participant_customizations {
            participant_id,
            nickname
        },
        outgoing_bubble_color,
        emoji
    },
    thread_admins {
        id
    },
    group_approval_queue {
        nodes {
            requester {
                id
            }
        }
    },
    approval_mode,
    joinable_mode {
        mode,
        link
    },
    event_reminders {
        nodes {
            id,
            lightweight_event_creator {
                id
            },
            time,
            location_name,
            event_title,
            event_reminder_members {
                edges {
                    node {
                        id
                    },
                    guest_list_state
                }
            }
        }
    }
}
";

        public static string FRAGMENT_PAGE = @"
QueryFragment Page: Page {
    id,
    name,
    profile_picture.width(32).height(32) {
        uri
    },
    url,
    category_type,
    city {
        name
    }
}
";

        public static string SEARCH_USER = @"
Query SearchUser(<search> = '', <limit> = 10) {
    entities_named(<search>) {
        search_results.of_type(user).first(<limit>) as users {
            nodes {
                @User
            }
        }
    }
}
" + FRAGMENT_USER;

        public static string SEARCH_GROUP = @"
Query SearchGroup(<search> = '', <limit> = 10, <pic_size> = 32) {
    viewer() {
        message_threads.with_thread_name(<search>).last(<limit>) as groups {
            nodes {
                @Group
            }
        }
    }
}
" + FRAGMENT_GROUP;

        public static string SEARCH_PAGE = @"
Query SearchPage(<search> = '', <limit> = 10) {
    entities_named(<search>) {
        search_results.of_type(page).first(<limit>) as pages {
            nodes {
                @Page
            }
        }
    }
}
" + FRAGMENT_PAGE;

        public static string SEARCH_THREAD = @"
Query SearchThread(<search> = '', <limit> = 10) {
    entities_named(<search>) {
        search_results.first(<limit>) as threads {
            nodes {
                __typename,
                @User,
                @Group,
                @Page
            }
        }
    }
}
" + FRAGMENT_USER + FRAGMENT_GROUP + FRAGMENT_PAGE;
    }
}
