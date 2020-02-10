using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Represents a Facebook attachment.
    /// </summary>
    public class FB_Attachment
    {
        /// <summary>
        /// Unique id of the attachmnt.
        /// </summary>
        public string uid { get; set; }

        /// <summary>
        /// Represents a Facebook attachment.
        /// </summary>
        /// <param name="uid"></param>
        public FB_Attachment(string uid = null)
        {
            this.uid = uid;
        }

        internal static FB_Attachment graphql_to_attachment(JToken data)
        {
            var _type = data.get("__typename")?.Value<string>();
            if (new string[] { "MessageImage", "MessageAnimatedImage" }.Contains(_type))
            {
                return FB_ImageAttachment._from_graphql(data);
            }
            else if (_type == "MessageVideo")
            {
                return FB_VideoAttachment._from_graphql(data);
            }
            else if (_type == "MessageAudio")
            {
                return FB_AudioAttachment._from_graphql(data);
            }
            else if (_type == "MessageFile")
            {
                return FB_FileAttachment._from_graphql(data);
            }
            else
            {
                return new FB_Attachment(uid: data.get("legacy_attachment_id")?.Value<string>());
            }
        }

        internal static FB_Attachment graphql_to_subattachment(JToken data)
        {
            JToken target = data.get("target");
            string type = target != null ? target.get("__typename")?.Value<string>() : null;
            if (type == "Video")
                return FB_VideoAttachment._from_subattachment(data);
            return null;
        }

        internal static FB_Attachment graphql_to_extensible_attachment(JToken data)
        {
            var story = data.get("story_attachment");
            if (story == null)
                return null;

            var target = story.get("target");
            if (target == null)
                return new FB_UnsentMessage(uid: data.get("legacy_attachment_id")?.Value<string>());

            var _type = target.get("__typename")?.Value<string>();
            if (_type == "MessageLocation")
                return FB_LocationAttachment._from_graphql(story);
            else if (_type == "MessageLiveLocation")
                return FB_LiveLocationAttachment._from_graphql(story);
            else if (new string[] { "ExternalUrl", "Story" }.Contains(_type))
                return FB_ShareAttachment._from_graphql(story);

            return null;
        }
    }

    /// <summary>
    /// Represents an unsent message attachment.
    /// </summary>
    public class FB_UnsentMessage : FB_Attachment
    {
        /// <summary>
        /// Represents a Facebook attachment.
        /// </summary>
        /// <param name="uid"></param>
        public FB_UnsentMessage(string uid = null) : base(uid)
        {

        }
    }

    /// <summary>
    /// Represents a shared item (eg. URL) that has been sent as a Facebook attachment.
    /// </summary>
    public class FB_ShareAttachment : FB_Attachment
    {
        /// ID of the author of the shared post
        public string author { get; set; }
        /// Target URL
        public string url { get; set; }
        /// Original URL if Facebook redirects the URL
        public string original_url { get; set; }
        /// Title of the attachment
        public string title { get; set; }
        /// Description of the attachment
        public string description { get; set; }
        /// Name of the source
        public string source { get; set; }
        /// The attached image
        public FB_Image image { get; set; }
        /// URL of the original image if Facebook uses `safe_image`
        public string original_image_url { get; set; }        
        /// A list of attachments
        public List<FB_Attachment> attachments { get; set; }

        /// <summary>
        /// Represents a shared item (eg. URL) that has been sent as a Facebook attachment.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="author"></param>
        /// <param name="url"></param>
        /// <param name="original_url"></param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="source"></param>
        /// <param name="image"></param>
        /// <param name="original_image_url"></param>
        /// <param name="attachments"></param>
        public FB_ShareAttachment(string uid = null, string author = null, string url = null, string original_url = null, string title = null, string description = null, string source = null, FB_Image image = null, string original_image_url = null, List<FB_Attachment> attachments = null) : base(uid)
        {
            this.author = author;
            this.url = url;
            this.original_url = original_url;
            this.title = title;
            this.description = description;
            this.source = source;
            this.image = image;
            this.original_image_url = original_image_url;
            this.attachments = attachments;
        }

        internal static FB_ShareAttachment _from_graphql(JToken data)
        {
            string url = data.get("url")?.Value<string>();
            FB_ShareAttachment rtn = new FB_ShareAttachment(
                uid: data.get("deduplication_key")?.Value<string>(),
                author: data.get("target")?.get("actors")?.FirstOrDefault()?.get("id")?.Value<string>(),
                url: url,
                original_url: (url?.Contains("/l.php?u=") ?? false) ? Utils.get_url_parameter(url, "u") : url,
                title: data.get("title_with_entities")?.get("text")?.Value<string>(),
                description: data.get("description")?.get("text")?.Value<string>(),
                source: data.get("source")?.get("text")?.Value<string>()
                );

            rtn.attachments = data.get("subattachments")?.Select(node => FB_Attachment.graphql_to_subattachment(node))?.ToList();

            JToken media = data.get("media");
            if (media != null && media.get("image") != null)
            {
                JToken image = media.get("image");
                rtn.image = FB_Image._from_uri(image);
                rtn.original_image_url = (rtn.image.url?.Contains("/safe_image.php") ?? false) ? Utils.get_url_parameter(rtn.image.url, "url") : rtn.image.url;
            }

            return rtn;
        }
    }
}
