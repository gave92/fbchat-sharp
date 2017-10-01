using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace fbchat_sharp.API
{
    internal class GraphQL_JSON_Decoder
    {
        public static string graphql_color_to_enum(string color)
        {
            if (color == null)
                return null;
            if (color.Length == 0)
                return ThreadColor.MESSENGER_BLUE;
            // return "#{}".format(color[2:].lower()))
            return ThreadColor.MESSENGER_BLUE;
        }

        public static Dictionary<string, string> get_customization_info(JToken thread)
        {
            return new Dictionary<string, string>();

            /*
             if thread is None or thread.get("customization_info") is None:
        return {}
info = thread["customization_info"]

rtn = {
        "emoji": info.get("emoji"),
        "color": graphql_color_to_enum(info.get("outgoing_bubble_color"))
    }
    if thread.get("thread_type") == "GROUP" or thread.get("is_group_thread") or thread.get("thread_key", { }).get("thread_fbid"):
        rtn["nicknames"] = {}
        for k in info.get("participant_customizations", []):
            rtn["nicknames"][k["participant_id"]] = k.get("nickname")
    elif info.get("participant_customizations"):
        uid = thread.get("thread_key", {}).get("other_user_id") or thread.get("id")
        pc = info["participant_customizations"]
        if len(pc) > 0:
            if pc[0].get("participant_id") == uid:
                rtn["nickname"] = pc[0].get("nickname")
            else:
                rtn["own_nickname"] = pc[0].get("nickname")
        if len(pc) > 1:
            if pc[1].get("participant_id") == uid:
                rtn["nickname"] = pc[1].get("nickname")
            else:
                rtn["own_nickname"] = pc[1].get("nickname")
    return rtn
             */
        }

        public static FB_Message graphql_to_message(JToken message)
        {
            if (message["message_sender"] == null)
                message["message_sender"] = new JObject(new JProperty("id", 0));
            if (message["message"] == null)
                message["message"] = new JObject(new JProperty("text", "" ));
            bool is_read = false;
            if (message["unread"] != null)
                is_read = !message["unread"].Value<bool>();

            return new FB_Message(
                uid: message["message_id"]?.Value<string>(),
                author: message["message_sender"]["id"]?.Value<string>(),
                timestamp: message["timestamp_precise"]?.Value<string>(),
                is_read: is_read,
                reactions: new List<string>(),
                text: message["message"]["text"]?.Value<string>(),
                mentions: new List<FB_Mention>(),
                sticker: message["sticker"]?.Value<JObject>(),
                attachments: message["blob_attachments"]?.Value<JArray>(),
                extensible_attachment: null);
        }

        public static FB_User graphql_to_user(JToken user)
        {
            if (user["profile_picture"] == null)
                user["profile_picture"] = new JObject(new JProperty("uri", ""));

            return new FB_User(
                uid: user["id"]?.Value<string>(),
                url: user["url"]?.Value<string>(),
                first_name: user["first_name"]?.Value<string>(),
                last_name: user["last_name"]?.Value<string>(),
                is_friend: user["is_viewer_friend"]?.Value<bool>() ?? false,
                gender: user["gender"]?.Value<string>(),
                affinity: user["viewer_affinity"]?.Value<float>() ?? 0,
                nickname: "",
                color: ThreadColor.MESSENGER_BLUE,
                emoji: "",
                own_nickname: "",
                photo: user["profile_picture"]["uri"]?.Value<string>(),
                name: user["name"]?.Value<string>(),
                message_count: user["messages_count"]?.Value<int>() ?? 0);
        }

        public static FB_Group graphql_to_group(JToken group)
        {
            if (group["image"] == null)
                group["image"] = new JObject(new JProperty("uri", ""));

            return new FB_Group(
                uid: group["thread_key"]["thread_fbid"].Value<string>(),
                participants: new HashSet<string>(group["all_participants"]["nodes"].Select(node => node["messaging_actor"]["id"].Value<string>())),
                nicknames: new Dictionary<string, string>(),
                color: ThreadColor.MESSENGER_BLUE,
                emoji: "",
                photo: group["image"]["uri"].Value<string>(),
                name: group["name"].Value<string>(),
                message_count: group["messages_count"].Value<int>());
        }

        public static FB_Page graphql_to_page(JToken page)
        {
            if (page["profile_picture"] == null)
                page["profile_picture"] = new JObject(new JProperty("uri", ""));
            if (page["city"] == null)
                page["city"] = new JObject(new JProperty("name", ""));

            return new FB_Page(
                uid: page["id"].Value<string>(),
                url: page["url"].Value<string>(),
                city: page["city"]["name"].Value<string>(),
                category: page["category_type"].Value<string>(),
                photo: page["profile_picture"]["uri"].Value<string>(),
                name: page["name"].Value<string>(),
                message_count: page["messages_count"].Value<int>()
            );
        }

        public static string graphql_queries_to_json(List<GraphQL> queries)
        {
            /*
             * Queries should be a list of GraphQL objects
             */
            var rtn = new Dictionary<string, object>();
            foreach (var obj in queries.Select((x, i) => new { query = x, index = i }))
                rtn[string.Format("q{0}", obj.index)] = obj.query.value;
            return JsonConvert.SerializeObject(rtn);
        }

        public static List<JToken> graphql_response_to_json(string content)
        {
            var json_array = content.Split(new[] { "\n{", " {" }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.StartsWith("{") ? s : "{" + s);
            var rtn = new List<JToken>(Enumerable.Repeat(default(JToken), json_array.Count()));

            foreach (var json_string in json_array)
            {
                var x = JToken.Parse(json_string);
                if (x["error_results"] != null)
                {
                    rtn.RemoveAt(rtn.Count - 1);
                    continue;
                }
                Utils.check_json(x);
                string key = x.Value<JObject>().Properties().Where(k => k.Name.StartsWith("q")).First().Name;
                JToken value = x[key];
                Utils.check_json(value);
                if (value["response"] != null)
                    rtn[int.Parse(key.Substring(1))] = value["response"];
                else
                    rtn[int.Parse(key.Substring(1))] = value["data"];
            }

            return rtn;
        }
    }

    internal class GraphQL
    {
        public Dictionary<string, object> value;

        public GraphQL(string query = null, string doc_id = null, Dictionary<string, string> param = null)
        {
            if (query != null)
                this.value = new Dictionary<string, object>(){
                    { "priority", 0},
                {"q", query},
                {"query_params", param }
            };
            else if (doc_id != null)
                this.value = new Dictionary<string, object>() {
                    { "doc_id", doc_id },
                {"query_params", param }
            };
            else
                throw new Exception("A query or doc_id must be specified");
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
QueryFragment FGroup: MessageThread {
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
Query SearchUser(<search> = '', <limit> = 1) {
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
Query SearchFGroup(<search> = '', <limit> = 1, <pic_size> = 32) {
        viewer() {
            message_threads.with_thread_name(<search>).last(<limit>) as groups {
                nodes {
                    @FGroup
                }
            }
        }
    }
" + FRAGMENT_GROUP;

        public static string SEARCH_PAGE = @"
Query SearchPage(<search> = '', <limit> = 1) {
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
Query SearchThread(<search> = '', <limit> = 1) {
        entities_named(<search>) {
            search_results.first(<limit>) as threads {
                nodes {
                    __typename,
                    @User,
                    @FGroup,
                    @Page
                }
            }
        }
    }
" + FRAGMENT_USER + FRAGMENT_GROUP + FRAGMENT_PAGE;
    }
}
