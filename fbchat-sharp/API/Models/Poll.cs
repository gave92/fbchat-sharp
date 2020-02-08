using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Represents a poll
    /// </summary>
    public class FB_Poll
    {
        /// The session to use when making requests
        public Session session { get; set; }
        /// ID of the poll
        public string uid { get; set; }
        /// Title of the poll
        public string title { get; set; }
        /// List of `PollOption`, can be fetched with :func:`fbchat-sharp.Client.fetchPollOptions`
        public List<FB_PollOption> options { get; set; }
        /// Options count
        public int options_count { get; set; }

        /// <summary>
        /// Represents a poll
        /// </summary>
        /// <param name="session"></param>
        /// <param name="uid"></param>
        /// <param name="title"></param>
        /// <param name="options"></param>
        /// <param name="options_count"></param>        
        public FB_Poll(Session session, string uid = null, string title = null, List<FB_PollOption> options = null, int options_count = 0)
        {
            this.session = session;
            this.title = title;
            this.options = options ?? new List<FB_PollOption>();
            this.options_count = options_count;
            this.uid = uid;
        }

        public static FB_Poll _from_graphql(JToken data, Session session)
        {
            return new FB_Poll(
                session: session,
                uid: data.get("id")?.Value<string>(),
                title: data.get("title")?.Value<string>() ?? data.get("text")?.Value<string>(),
                options: data.get("options")?.Select((m) => FB_PollOption._from_graphql(m))?.ToList(),
                options_count: data.get("total_count")?.Value<int>() ?? 0);
        }

        /// <summary>
        /// Fetches list of`PollOption` objects from the poll
        /// </summary>
        /// <returns></returns>
        public async Task<List<FB_PollOption>> fetchOptions()
        {
            /*
             * Fetches list of`PollOption` objects from the poll
             * :rtype: list
             * :raises: FBchatException if request failed
             * */

            var data = new Dictionary<string, object>()
            {
                { "question_id", this.uid }
            };
            var j = await this.session._payload_post("/ajax/mercury/get_poll_options", data);
            return j.Select((m) => FB_PollOption._from_graphql(m)).ToList();
        }

        /// <summary>
        /// Updates a poll vote
        /// </summary>
        /// <param name="option_ids">List of the option IDs to vote</param>
        /// <param name="new_options">List of the new option names</param>
        /// <returns></returns>
        public async Task updateVote(List<string> option_ids = null, List<string> new_options = null)
        {
            /*
             * Update the user's poll vote.
             * Args:
             *   option_ids: Option ids to vote for / keep voting for
             *   new_options: New options to add
             * Example:
             *   options = poll.fetch_options()
             *   # Add option
             *   poll.set_votes([o.id for o in options], new_options=["New option"])
             *   # Remove vote from option
             *   poll.set_votes([o.id for o in options if o.text != "Option 1"])
             * */
            var data = new Dictionary<string, object>() { { "question_id", this.uid } };

            if (option_ids != null)
            {
                foreach (var obj in option_ids.Select((x, index) => new { option_id = x, i = index }))
                    data[string.Format("selected_options[{0}]", obj.i)] = obj.option_id;
            }

            if (new_options != null)
            {
                foreach (var obj in new_options.Select((x, index) => new { option_text = x, i = index }))
                    data[string.Format("new_options[{0}]", obj.i)] = obj.option_text;
            }

            var j = await this.session._payload_post("/messaging/group_polling/update_vote/?dpr=1", data);
            if (j.get("status")?.Value<string>() != "success")
                throw new FBchatFacebookError(
                    string.Format("Failed updating poll vote: {0}", j.get("errorTitle")),
                    fb_error_message: j.get("errorMessage")?.Value<string>()
                );
        }
    }

    /// <summary>
    /// Represents a poll option
    /// </summary>
    public class FB_PollOption
    {
        /// Text of the poll option
        public string text { get; set; }
        /// Whether vote when creating or client voted
        public bool vote { get; set; }
        /// ID of the users who voted for this poll option
        public List<string> voters { get; set; }
        /// Votes count
        public int votes_count { get; set; }
        /// ID of the poll option
        public string uid { get; set; }

        /// <summary>
        /// Represents a poll option
        /// </summary>
        /// <param name="text"></param>
        /// <param name="vote"></param>
        /// <param name="voters"></param>
        /// <param name="votes_count"></param>
        /// <param name="uid"></param>
        public FB_PollOption(string text = null, bool vote = false, List<string> voters = null, int votes_count = 0, string uid = null)
        {
            this.text = text;
            this.vote = vote;
            this.voters = voters ?? new List<string>();
            this.votes_count = votes_count;
            this.uid = uid;
        }

        public static FB_PollOption _from_graphql(JToken data)
        {
            bool vote = false;
            if (data.get("viewer_has_voted") == null)
                vote = false;
            else if (data.get("viewer_has_voted")?.Type == JTokenType.Boolean)
                vote = data.get("viewer_has_voted")?.Value<bool>() ?? false;
            else
                vote = data.get("viewer_has_voted")?.Value<string>() == "true";

            return new FB_PollOption(
                uid: data.get("id")?.Value<string>(),
                text: data.get("text")?.Value<string>(),
                vote: vote,
                voters: (data.get("voters")?.Type == JTokenType.Object ?
                    data.get("voters")?.get("edges").Select((m) => m.get("node")?.get("id")?.Value<string>()) : data.get("voters")?.ToObject<List<string>>()).ToList(),
                votes_count: (data.get("voters")?.Type == JTokenType.Object ?
                    data.get("voters")?.get("count")?.Value<int>() : data.get("total_count")?.Value<int>()) ?? 0
            );
        }
    }
}
