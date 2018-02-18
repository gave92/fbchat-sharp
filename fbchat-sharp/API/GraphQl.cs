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
            try
            {
                return string.Format("#{0}", color.Skip(1).ToString());
            }
            catch
            {
                throw new FBchatException(string.Format("Could not get ThreadColor from color: {0}", color));
            }
        }

        public static Dictionary<string, string> get_customization_info(JToken thread)
        {
            return new Dictionary<string, string>();

            /*
            if thread is None or thread['customization_info') is None:
        return {}
    info = thread['customization_info']

    rtn = {
        'emoji': info['emoji'),
        'color': graphql_color_to_enum(info['outgoing_bubble_color'))
    }
    if thread['thread_type') in ('GROUP', 'ROOM') or thread['is_group_thread') or thread['thread_key', {})['thread_fbid'):
        rtn['nicknames'] = {}
        for k in info['participant_customizations', []):
            rtn['nicknames'][k['participant_id']] = k['nickname')
    elif info['participant_customizations'):
        uid = thread['thread_key', {})['other_user_id') or thread['id')
        pc = info['participant_customizations']
        if len(pc) > 0:
            if pc[0]['participant_id') == uid:
                rtn['nickname'] = pc[0]['nickname')
            else:
                rtn['own_nickname'] = pc[0]['nickname')
        if len(pc) > 1:
            if pc[1]['participant_id') == uid:
                rtn['nickname'] = pc[1]['nickname')
            else:
                rtn['own_nickname'] = pc[1]['nickname')
    return rtn
             */
        }

        public static FB_Sticker graphql_to_sticker(JToken s)
        {
            if (s == null || s.Type == JTokenType.Null)
            {
                return null;
            }
            var sticker = new FB_Sticker(uid: s["id"].Value<string>());
            if (s["pack"] != null)
            {
                sticker.pack = s["pack"]["id"].Value<string>();
            }
            if (s["sprite_image"] != null && s["sprite_image"].Type != JTokenType.Null)
            {
                sticker.is_animated = true;
                sticker.medium_sprite_image = s["sprite_image"]["uri"].Value<string>();
                sticker.large_sprite_image = s["sprite_image_2x"]["uri"].Value<string>();
                sticker.frames_per_row = s["frames_per_row"].Value<int>();
                sticker.frames_per_col = s["frames_per_column"].Value<int>();
                sticker.frame_rate = s["frame_rate"].Value<float>();
            }
            sticker.url = s["url"].Value<string>();
            sticker.width = s["width"].Value<int>();
            sticker.height = s["height"].Value<int>();
            if (s["label"] != null)
            {
                sticker.label = s["label"].Value<string>();
            }
            return sticker;
        }

        public static FB_Attachment graphql_to_attachment(JToken a)
        {
            var _type = a["__typename"].Value<string>();
            if (new string[] { "MessageImage", "MessageAnimatedImage" }.Contains(_type))
            {
                return new FB_ImageAttachment(
                    original_extension: a["original_extension"]?.Value<string>() ?? a["filename"]?.Value<string>()?.Split('-')[0],
                    width: a["original_dimensions"]["x"].Value<int>(),
                    height: a["original_dimensions"]["y"].Value<int>(),
                    is_animated: _type == "MessageAnimatedImage",
                    thumbnail_url: a["thumbnail"]["uri"]?.Value<string>(),
                    preview: a["preview"],
                    large_preview: a["large_preview"],
                    animated_preview: a["animated_image"],
                    uid: a["legacy_attachment_id"]?.Value<string>());
            }
            else if (_type == "MessageVideo")
            {
                return new FB_VideoAttachment(
                    width: a["original_dimensions"]["x"].Value<int>(),
                    height: a["original_dimensions"]["y"].Value<int>(),
                    duration: a["playable_duration_in_ms"].Value<int>(),
                    preview_url: a["playable_url"]?.Value<string>(),
                    small_image: a["chat_image"],
                    medium_image: a["inbox_image"],
                    large_image: a["large_image"],
                    uid: a["legacy_attachment_id"]?.Value<string>());
            }
            else if (_type == "MessageFile")
            {
                return new FB_FileAttachment(
                    url: a["url"].Value<string>(),
                    name: a["filename"]?.Value<string>(),
                    is_malicious: a["is_malicious"].Value<bool>(),
                    uid: a["message_file_fbid"]?.Value<string>());
            }
            else
            {
                return new FB_Attachment(uid: a["legacy_attachment_id"]?.Value<string>());
            }
        }

        public static FB_Message graphql_to_message(string thread_id, JToken message)
        {
            if (message["message_sender"] == null || message["message_sender"].Type == JTokenType.Null)
                message["message_sender"] = new JObject(new JProperty("id", 0));
            if (message["message"] == null || message["message"].Type == JTokenType.Null)
                message["message"] = new JObject(new JProperty("text", ""));

            var rtn = new FB_Message(
                text: message["message"]["text"]?.Value<string>(),
                mentions: new List<FB_Mention>(),
                sticker: graphql_to_sticker(message["sticker"]));

            rtn.thread_id = thread_id;
            rtn.uid = message["message_id"]?.Value<string>();
            rtn.author = message["message_sender"]["id"]?.Value<string>();
            rtn.timestamp = message["timestamp_precise"]?.Value<string>();
            if (message["unread"] != null)
                rtn.is_read = !message["unread"].Value<bool>();
            rtn.reactions = new Dictionary<string, MessageReaction>();

            foreach (var r in message["message_reactions"])
            {
                rtn.reactions.Add(r["user"]["id"]?.Value<string>(), Constants.REACTIONS[r["reaction"].Value<string>()]);
            }

            if (message["blob_attachments"] != null)
            {
                rtn.attachments = new List<FB_Attachment>();
                foreach (var attachment in message["blob_attachments"])
                {
                    rtn.attachments.Add(graphql_to_attachment(attachment));
                }
            }
            // TODO: This is still missing parsing:
            // message.get('extensible_attachment')
            return rtn;
        }

        public static FB_User graphql_to_user(JToken user)
        {
            if (user["profile_picture"] == null || user["profile_picture"].Type == JTokenType.Null)
                user["profile_picture"] = new JObject(new JProperty("uri", ""));
            var c_info = get_customization_info(user);

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
            if (group["image"] == null || group["image"].Type == JTokenType.Null)
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

        public static FB_Room graphql_to_room(JToken room)
        {
            if (room["image"] == null || room["image"].Type == JTokenType.Null)
                room["image"] = new JObject(new JProperty("uri", ""));
            var c_info = get_customization_info(room);

            return new FB_Room(
                room["thread_key"]["thread_fbid"].Value<string>(),
                participants: new HashSet<string>(room["all_participants"]["nodes"].Select(node => node["messaging_actor"]["id"].Value<string>())),
                nicknames: null,
                color: ThreadColor.MESSENGER_BLUE,
                emoji: "",
                photo: room["image"]["uri"].Value<string>(),
                name: room["name"].Value<string>(),
                message_count: room["messages_count"].Value<int>(),
                admins: new HashSet<string>(room["thread_admins"].Select(node => node["id"].Value<string>())),
                approval_mode: room["approval_mode"].Value<bool>(),
                approval_requests: new HashSet<string>(room["thread_queue_metadata"]["approval_requests"]["nodes"].Select(node => node["id"].Value<string>())),
                join_link: room["joinable_mode"]["link"].Value<string>(),
                privacy_mode: room["privacy_mode"].Value<bool>());
        }

        public static FB_Page graphql_to_page(JToken page)
        {
            if (page["profile_picture"] == null || page["profile_picture"].Type == JTokenType.Null)
                page["profile_picture"] = new JObject(new JProperty("uri", ""));
            if (page["city"] == null || page["city"].Type == JTokenType.Null)
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

        public static FB_Thread graphql_to_thread(JToken thread)
        {
            if (thread["thread_type"].Value<string>().Equals("GROUP"))
            {
                return GraphQL_JSON_Decoder.graphql_to_group(thread);
            }
            else if (thread["thread_type"].Value<string>().Equals("ROOM"))
            {
                return GraphQL_JSON_Decoder.graphql_to_room(thread);
            }
            else if (thread["thread_type"].Value<string>().Equals("ONE_TO_ONE"))
            {
                var participants = thread["all_participants"]["nodes"].Select(node => node["messaging_actor"]);
                var user = participants.Single(p => p["id"].Value<string>() == thread["thread_key"]["other_user_id"].Value<string>());

                if (user["big_image_src"] == null || user["big_image_src"].Type == JTokenType.Null)
                    user["big_image_src"] = new JObject(new JProperty("uri", ""));

                return new FB_User(
                    uid: user["id"].Value<string>(),
                    url: user["url"]?.Value<string>(),
                    name: user["name"]?.Value<string>(),
                    first_name: user["short_name"]?.Value<string>(),
                    last_name: user["name"]?.Value<string>()?.Replace(user["short_name"]?.Value<string>(), "")?.Trim(),
                    is_friend: user["is_viewer_friend"]?.Value<bool>() ?? false,
                    gender: user["gender"]?.Value<string>(),
                    nickname: "",
                    color: ThreadColor.MESSENGER_BLUE,
                    emoji: "",
                    own_nickname: "",
                    affinity: 0,
                    photo: user["big_image_src"]["uri"]?.Value<string>(),
                    message_count: thread["messages_count"]?.Value<int>() ?? 0
                    );
            }
            else
            {
                throw new FBchatException(string.Format("Unknown thread type: {0}", thread));
            }
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

        public GraphQL(string query = null, string doc_id = null, Dictionary<string, object> param = null)
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
                throw new FBchatException("A query or doc_id must be specified");
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
Query SearchGroup(<search> = '', <limit> = 1, <pic_size> = 32) {
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
                    @Group,
                    @Page
                }
            }
        }
    }
" + FRAGMENT_USER + FRAGMENT_GROUP + FRAGMENT_PAGE;
    }
}
