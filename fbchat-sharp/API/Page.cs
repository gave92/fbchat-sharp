using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Facebook messenger page class
    /// </summary>
    public class FB_Page : FB_Thread
    {
        /// The page's custom url
        public string url { get; set; }
        /// The name of the page's location city
        public string city { get; set; }
        /// Amount of likes the page has
        public int likes { get; set; }
        /// Some extra information about the page
        public string sub_title { get; set; }
        /// The page's category
        public string category { get; set; }

        /// <summary>
        /// Represents a Facebook page. Inherits `Thread`
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="photo"></param>
        /// <param name="name"></param>
        /// <param name="message_count"></param>
        /// <param name="plan"></param>
        /// <param name="url"></param>
        /// <param name="city"></param>
        /// <param name="likes"></param>
        /// <param name="sub_title"></param>
        /// <param name="category"></param>
        public FB_Page(string uid, string photo = null, string name = null, int message_count = 0, FB_Plan plan = null, string url = null, string city = null, int likes = 0, string sub_title = null, string category = null)
            : base(ThreadType.PAGE, uid, photo, name, message_count: message_count, plan: plan)
        {
            // Represents a Facebook page. Inherits `Thread`
            this.url = url;
            this.city = city;
            this.likes = likes;
            this.sub_title = sub_title;
            this.category = category;
        }

        public static FB_Page _from_graphql(JToken data)
        {
            if (data["profile_picture"] == null || data["profile_picture"].Type == JTokenType.Null)
                data["profile_picture"] = new JObject(new JProperty("uri", ""));
            if (data["city"] == null || data["city"].Type == JTokenType.Null)
                data["city"] = new JObject(new JProperty("name", ""));

            FB_Plan plan = null;
            if (data["event_reminders"] != null && data["event_reminders"]?["nodes"] != null)
                plan = FB_Plan._from_graphql(data["event_reminders"]["nodes"][0]);

            return new FB_Page(
                uid: data["id"]?.Value<string>(),
                url: data["url"]?.Value<string>(),
                city: data["city"]?["name"]?.Value<string>(),
                category: data["category_type"]?.Value<string>(),
                photo: data["profile_picture"]?["uri"]?.Value<string>(),
                name: data["name"]?.Value<string>(),
                message_count: data["messages_count"]?.Value<int>() ?? 0,
                plan: plan
            );
        }
    }
}
