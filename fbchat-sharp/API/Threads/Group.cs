using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Facebook messenger group class
    /// </summary>
    public class FB_Group : FB_Thread
    {
        /// Unique list (set) of the group thread"s participants
        public ISet<FB_Thread> participants { get; set; }
        /// Dict, containing user nicknames mapped to their IDs
        public Dictionary<string, string> nicknames { get; set; }
        /// A `ThreadColor`. The groups"s message color
        public string color { get; set; }
        /// The groups"s default emoji
        public JToken emoji { get; set; }
        /// Set containing user IDs of thread admins
        public ISet<string> admins { get; set; }
        /// True if users need approval to join
        public bool approval_mode { get; set; }
        /// Set containing user IDs requesting to join
        public ISet<string> approval_requests { get; set; }
        /// Link for joining group
        public string join_link { get; set; }

        /// <summary>
        /// Represents a Facebook group. Inherits `Thread`
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="session"></param>
        /// <param name="photo"></param>
        /// <param name="name"></param>
        /// <param name="message_count"></param>
        /// <param name="last_message_timestamp"></param>
        /// <param name="plan"></param>
        /// <param name="participants"></param>
        /// <param name="nicknames"></param>
        /// <param name="color"></param>
        /// <param name="emoji"></param>
        /// <param name="admins"></param>
        /// <param name="approval_mode"></param>
        /// <param name="approval_requests"></param>
        /// <param name="join_link"></param>
        public FB_Group(string uid, Session session, FB_Image photo = null, string name = null, int message_count = 0, string last_message_timestamp = null, FB_Plan plan = null, ISet<FB_Thread> participants = null, Dictionary<string, string> nicknames = null, string color = null, JToken emoji = null, ISet<string> admins = null, bool approval_mode = false, ISet<string> approval_requests = null, string join_link = null)
            : base(uid, session, photo, name, message_count: message_count, last_message_timestamp: last_message_timestamp, plan: plan)
        {
            this.participants = participants ?? new HashSet<FB_Thread>();
            this.nicknames = nicknames ?? new Dictionary<string, string>();
            this.color = color ?? ThreadColor.MESSENGER_BLUE;
            this.emoji = emoji;
            this.admins = admins ?? new HashSet<string>();
            this.approval_mode = approval_mode;
            this.approval_requests = approval_requests ?? new HashSet<string>();
            this.join_link = join_link;
        }

        /// <summary>
        /// Represents a Facebook group. Inherits `Thread`
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="session"></param>
        public FB_Group(string uid, Session session) :
            base(uid, session)
        {

        }

        internal static FB_Group _from_graphql(Session session, JToken data)
        {
            if (data.get("image") == null)
                data["image"] = new JObject(new JProperty("uri", ""));
            var c_info = FB_Group._parse_customization_info(data);

            var last_message_timestamp = data.get("last_message")?.get("nodes")?.FirstOrDefault()?.get("timestamp_precise")?.Value<string>();
            var plan = data.get("event_reminders")?.get("nodes")?.FirstOrDefault() != null ? FB_Plan._from_graphql(data.get("event_reminders")?.get("nodes")?.FirstOrDefault(), session) : null;

            return new FB_Group(
                uid: data.get("thread_key")?.get("thread_fbid")?.Value<string>(),
                session: session,
                participants: new HashSet<FB_Thread>(FB_Thread._parse_participants(data.get("all_participants"), session)),
                nicknames: (Dictionary<string, string>)c_info.GetValueOrDefault("nicknames"),
                color: (string)c_info.GetValueOrDefault("color"),
                emoji: (JToken)c_info.GetValueOrDefault("emoji"),
                admins: new HashSet<string>(data.get("thread_admins")?.Select(node => node.get("id")?.Value<string>())),
                approval_mode: data.get("approval_mode")?.Value<bool>() ?? false,
                approval_requests: data.get("group_approval_queue") != null ? new HashSet<string>(data.get("group_approval_queue")?.get("nodes")?.Select(node => node.get("requester")?.get("id")?.Value<string>())) : null,
                photo: FB_Image._from_uri_or_none(data?.get("image")),
                name: data.get("name")?.Value<string>(),
                message_count: data.get("messages_count")?.Value<int>() ?? 0,
                last_message_timestamp: last_message_timestamp,
                plan: plan);
        }

        internal override Dictionary<string, object> _to_send_data()
        {
            return new Dictionary<string, object>() { { "thread_fbid", this.uid } };
        }

        /// <summary>
        /// Add users to the group.
        /// If the group's approval mode is set to require admin approval, and you're not an
        /// admin, the participants won't actually be added, they will be set as pending.
        /// In that case, the returned `ParticipantsAdded` event will not be correct.
        /// Args:
        ///     user_ids: One or more user IDs to add
        /// Example:
        ///     >>> group.add_participants(["1234", "2345"])
        /// </summary>
        /// <param name="user_ids">One or more user IDs to add</param>
        /// <returns></returns>
        public async Task<FB_ParticipantsAdded> addParticipants(List<string> user_ids)
        {
            var data = this._to_send_data();

            data["action_type"] = "ma-type:log-message";
            data["log_message_type"] = "log:subscribe";

            var uuser_ids = Utils.require_list<string>(user_ids);

            foreach (var obj in user_ids.Select((x, index) => new { user_id = x, i = index }))
            {
                if (obj.user_id == this.session.user.uid)
                    throw new FBchatUserError(
                            "Error when adding users: Cannot add self to group thread"
                    );
                else
                    data[
                        string.Format("log_message_data[added_participants][{0}]", obj.i)
                    ] = string.Format("fbid:{0}", obj.user_id);
            }
            var req = await this.session._do_send_request(data);
            return FB_ParticipantsAdded._from_send(thread: this, added_ids: user_ids);
        }

        /// <summary>
        /// Remove user from the group.
        /// If the group's approval mode is set to require admin approval, and you're not an
        /// admin, this will fail.
        /// Args:
        ///     user_id: User ID to remove
        /// Example:
        ///     >>> group.remove_participant("1234")
        /// </summary>
        /// <param name="user_id">User ID to remove</param>
        /// <returns></returns>
        public async Task<FB_ParticipantRemoved> removeParticipant(string user_id)
        {
            /*
             * Removes users from a group.
             * :param user_id: User ID to remove
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() { { "uid", user_id }, { "tid", this.uid } };
            var j = await this.session._payload_post("/chat/remove_participants/", data);
            return FB_ParticipantRemoved._from_send(thread: this, removed_id: user_id);
        }

        /// <summary>
        /// Leave the group.
        /// This will succeed regardless of approval mode and admin status.
        /// Example:
        ///     >>> group.leave()
        /// </summary>
        /// <returns></returns>
        public async Task<FB_ParticipantRemoved> leave()
        {
            return await this.removeParticipant(this.session.user.uid);
        }

        private async Task _adminStatus(List<string> admin_ids, bool admin)
        {
            var data = new Dictionary<string, object>() { { "add", admin.ToString() }, { "thread_fbid", this.uid } };

            var uadmin_ids = Utils.require_list<string>(admin_ids);

            foreach (var obj in admin_ids.Select((x, index) => new { admin_id = x, i = index }))
                data[string.Format("admin_ids[{0}]", obj.i)] = obj.admin_id;

            var j = await this.session._payload_post("/messaging/save_admins/?dpr=1", data);
        }


        /// <summary>
        /// Sets specifed users as group admins.
        /// </summary>
        /// <param name="admin_ids">One or more user IDs to set admin</param>
        /// <returns></returns>
        public async Task addAdmins(List<string> admin_ids)
        {
            /*
             * Sets specifed users as group admins.
             * :param admin_ids: One or more user IDs to set admin
             * :raises: FBchatException if request failed
             * */
            await this._adminStatus(admin_ids, true);
        }

        /// <summary>
        /// Removes admin status from specifed users.
        /// </summary>
        /// <param name="admin_ids">One or more user IDs to remove admin</param>
        /// <returns></returns>
        public async Task removeAdmins(List<string> admin_ids)
        {
            /*
             * Removes admin status from specifed users.
             * :param admin_ids: One or more user IDs to remove admin
             * :raises: FBchatException if request failed
             * */
            await this._adminStatus(admin_ids, false);
        }

        /// <summary>
        /// Changes group's approval mode
        /// </summary>
        /// <param name="require_admin_approval">true or false</param>
        /// <returns></returns>
        public async Task changeApprovalMode(bool require_admin_approval)
        {
            /*
             * Changes group's approval mode
             * :param require_admin_approval: true or false
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() { { "set_mode", require_admin_approval ? 1 : 0 }, { "thread_fbid", this.uid } };
            var j = await this.session._payload_post("/messaging/set_approval_mode/?dpr=1", data);
        }

        private async Task _usersApproval(List<string> user_ids, bool approve)
        {
            var uuser_ids = Utils.require_list<string>(user_ids).ToList();

            var data = new Dictionary<string, object>(){
                { "client_mutation_id", "0"},
                { "actor_id", this.session.user.uid },
                { "thread_fbid", this.uid },
                { "user_ids", user_ids },
                { "response", approve ? "ACCEPT" : "DENY"},
                { "surface", "ADMIN_MODEL_APPROVAL_CENTER"}
            };
            var j = await this.session.graphql_request(
                GraphQL.from_doc_id("1574519202665847", new Dictionary<string, object>(){
                { "data", data} })
            );
        }

        /// <summary>
        /// Accepts users to the group from the group's approval
        /// </summary>
        /// <param name="user_ids">One or more user IDs to accept</param>
        /// <returns></returns>
        public async Task acceptUsers(List<string> user_ids)
        {
            /*
             * Accepts users to the group from the group's approval
             * :param user_ids: One or more user IDs to accept
             * :raises: FBchatException if request failed
             * */
            await this._usersApproval(user_ids, true);
        }

        /// <summary>
        /// Denies users from the group 's approval
        /// </summary>
        /// <param name="user_ids">One or more user IDs to deny</param>
        /// <returns></returns>
        public async Task denyUsers(List<string> user_ids)
        {
            /*
             * Denies users from the group 's approval
             * :param user_ids: One or more user IDs to deny
             * :raises: FBchatException if request failed
             * */
            await this._usersApproval(user_ids, false);
        }

        /// <summary>
        /// Changes a thread image from an image id
        /// </summary>
        /// <param name="image_id">ID of uploaded image</param>
        /// <returns></returns>
        public async Task<string> _changeImage(string image_id)
        {
            /*
             * Changes a thread image from an image id
             * :param image_id: ID of uploaded image
             * :raises: FBchatException if request failed
             * */
            var data = new Dictionary<string, object>() { { "thread_image_id", image_id }, { "thread_id", this.uid } };

            var j = await this.session._payload_post("/messaging/set_thread_image/?dpr=1", data);
            return image_id;
        }

        /// <summary>
        /// Changes a thread image from a URL
        /// </summary>
        /// <param name="image_url">URL of an image to upload and change</param>
        /// <returns></returns>
        public async Task<string> changeImageRemote(string image_url)
        {
            /*
             * Changes a thread image from a URL
             * :param image_url: URL of an image to upload and change
             * :raises: FBchatException if request failed
             * */
            var upl = await this.session._upload(await this.session.get_files_from_urls(new HashSet<string>() { image_url }));
            return await this._changeImage(upl[0].mimeKey);
        }

        /// <summary>
        /// Changes a thread image from a local path
        /// </summary>
        /// <param name="image_path">Path of an image to upload and change</param>
        /// <param name="image_stream"></param>
        /// <returns></returns>
        public async Task<string> changeImageLocal(string image_path, Stream image_stream)
        {
            /*
             * Changes a thread image from a local path
             * :param image_path: Path of an image to upload and change
             * :raises: FBchatException if request failed
             * */
            var files = this.session.get_files_from_paths(new Dictionary<string, Stream>() { { image_path, image_stream } });
            var upl = await this.session._upload(files);
            return await this._changeImage(upl[0].mimeKey);
        }
    }
}
