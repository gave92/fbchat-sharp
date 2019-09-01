using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Facebook messenger file class
    /// </summary>
    public class FB_File
    {
        /// Local or remote file path
        public string path { get; set; }
        /// Local or remote file stream
        public Stream data { get; set; }
        /// Local or remote file type
        public string mimetype { get; set; }

        public FB_File(Stream data = null, string path = null, string mimetype = null)
        {
            this.data = data;
            this.path = path;
            this.mimetype = mimetype;
        }
    }

    /// <summary>
    /// Represents a file that has been sent as a Facebook attachment.
    /// </summary>
    public class FB_FileAttachment : FB_Attachment
    {
        /// Url where you can download the file
        public string url { get; set; }
        /// Size of the file in bytes
        public int size { get; set; }
        /// Name of the file
        public string name { get; set; }
        /// Whether Facebook determines that this file may be harmful
        public bool is_malicious { get; set; }

        /// <summary>
        /// Represents a file that has been sent as a Facebook attachment.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="url"></param>
        /// <param name="size"></param>
        /// <param name="name"></param>
        /// <param name="is_malicious"></param>
        public FB_FileAttachment(string uid = null, string url = null, int size = 0, string name = null, bool is_malicious = false) : base(uid)
        {
            this.url = url;
            this.size = size;
            this.name = name;
            this.is_malicious = is_malicious;
        }

        public static FB_FileAttachment _from_graphql(JToken data)
        {
            return new FB_FileAttachment(
                url: data.get("url")?.Value<string>(),
                uid: data.get("message_file_fbid")?.Value<string>(),
                is_malicious: data.get("is_malicious")?.Value<bool>() ?? false,
                name: data.get("filename").Value<String>()
                );
        }
    }

    /// <summary>
    /// Represents an audio file that has been sent as a Facebook attachment - *Currently Incomplete!*
    /// </summary>
    public class FB_AudioAttachment : FB_Attachment
    {
        /// Name of the file
        public string filename { get; set; }
        /// Url where you can download the file
        public string url { get; set; }
        /// Length of video in milliseconds
        public int duration { get; set; }
        /// Audio type
        public string audio_type { get; set; }

        /// <summary>
        /// Represents an audio file that has been sent as a Facebook attachment - *Currently Incomplete!*
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="filename"></param>
        /// <param name="url"></param>
        /// <param name="duration"></param>
        /// <param name="audio_type"></param>
        public FB_AudioAttachment(string uid = null, string filename = null, string url = null, int duration = 0, string audio_type = null)
            : base(uid)
        {
            this.filename = filename;
            this.url = url;
            this.duration = duration;
            this.audio_type = audio_type;
        }

        public static FB_AudioAttachment _from_graphql(JToken data)
        {
            return new FB_AudioAttachment(
                filename: data.get("filename")?.Value<string>(),
                url: data.get("playable_url")?.Value<string>(),
                uid: null,
                duration: data.get("playable_duration_in_ms")?.Value<int>() ?? 0,
                audio_type: data.get("audio_type")?.Value<string>()
                );
        }
    }
    /// <summary>
    /// Represents an image that has been sent as a Facebook attachment
    /// To retrieve the full image url, use: `fbchat-sharp.Client.fetchImageUrl`, and pass
    /// it the uid of the image attachment
    /// </summary>
    public class FB_ImageAttachment : FB_Attachment
    {
        /// The extension of the original image (eg. 'png')
        public string original_extension { get; set; }
        /// Width of original image
        public int width { get; set; }
        /// Height of original image
        public int height { get; set; }
        /// Whether the image is animated
        public bool is_animated { get; set; }
        /// URL to a thumbnail of the image
        public string thumbnail_url { get; set; }
        /// URL to a medium preview of the image
        public string preview_url { get; set; }
        /// Width of the medium preview image
        public int preview_width { get; set; }
        /// Height of the medium preview image
        public int preview_height { get; set; }
        /// URL to a large preview of the image
        public string large_preview_url { get; set; }
        /// Width of the large preview image
        public int large_preview_width { get; set; }
        /// Height of the large preview image
        public int large_preview_height { get; set; }
        /// URL to an animated preview of the image(eg. for gifs)
        public string animated_preview_url { get; set; }
        /// Width of the animated preview image
        public int animated_preview_width { get; set; }
        /// Height of the animated preview image
        public int animated_preview_height { get; set; }

        /// <summary>
        /// Represents an image that has been sent as a Facebook attachment
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="original_extension"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="is_animated"></param>
        /// <param name="thumbnail_url"></param>
        /// <param name="preview"></param>
        /// <param name="large_preview"></param>
        /// <param name="animated_preview"></param>
        public FB_ImageAttachment(string uid = null, string original_extension = null, int width = 0, int height = 0, bool is_animated = false, string thumbnail_url = null, JToken preview = null, JToken large_preview = null, JToken animated_preview = null)
            : base(uid)
        {
            this.original_extension = original_extension;
            this.width = width;
            this.height = height;
            this.is_animated = is_animated;
            this.thumbnail_url = thumbnail_url;

            if (preview != null)
            {
                this.preview_url = preview.get("uri")?.Value<String>();
                this.preview_width = preview.get("width")?.Value<int>() ?? 0;
                this.preview_height = preview.get("height")?.Value<int>() ?? 0;
            }
            if (large_preview != null)
            {
                this.large_preview_url = large_preview.get("uri")?.Value<String>();
                this.large_preview_width = large_preview.get("width")?.Value<int>() ?? 0;
                this.large_preview_height = large_preview.get("height")?.Value<int>() ?? 0;
            }
            if (animated_preview != null)
            {
                this.animated_preview_url = animated_preview.get("uri")?.Value<String>();
                this.animated_preview_width = animated_preview.get("width")?.Value<int>() ?? 0;
                this.animated_preview_height = animated_preview.get("height")?.Value<int>() ?? 0;
            }
        }

        public static FB_ImageAttachment _from_graphql(JToken data)
        {
            return new FB_ImageAttachment(
                    original_extension: data.get("original_extension")?.Value<string>() ?? data.get("filename")?.Value<string>()?.Split('-')[0],
                    width: data.get("original_dimensions")?.get("width")?.Value<int>() ?? 0,
                    height: data.get("original_dimensions")?.get("height")?.Value<int>() ?? 0,
                    is_animated: data.get("__typename")?.Value<String>() == "MessageAnimatedImage",
                    thumbnail_url: data.get("thumbnail")?.get("uri")?.Value<string>(),
                    preview: data.get("preview") ?? data.get("preview_image"),
                    large_preview: data.get("large_preview"),
                    animated_preview: data.get("animated_image"),
                    uid: data.get("legacy_attachment_id")?.Value<string>());
        }

        public static FB_ImageAttachment _from_list(JToken data)
        {
            data = data["node"];
            return new FB_ImageAttachment(
                width: data?.get("original_dimensions")?.get("x")?.Value<int>() ?? 0,
                height: data?.get("original_dimensions")?.get("y")?.Value<int>() ?? 0,
                thumbnail_url: data?.get("image")?.get("uri")?.Value<string>(),
                large_preview: data?.get("image2"),
                preview: data?.get("image1"),
                uid: data?.get("legacy_attachment_id")?.Value<string>()
            );
        }
    }

    /// <summary>
    /// Represents a video that has been sent as a Facebook attachment
    /// </summary>
    public class FB_VideoAttachment : FB_Attachment
    {
        /// Size of the original video in bytes
        public int size { get; set; }
        /// Width of original video
        public int width { get; set; }
        /// Height of original video
        public int height { get; set; }
        /// Length of video in milliseconds
        public int duration { get; set; }
        /// URL to very compressed preview video
        public string preview_url { get; set; }
        /// URL to a small preview image of the video
        public string small_image_url { get; set; }
        /// Width of the small preview image
        public int small_image_width { get; set; }
        /// Height of the small preview image
        public int small_image_height { get; set; }
        /// URL to a medium preview image of the video
        public string medium_image_url { get; set; }
        /// Width of the medium preview image
        public int medium_image_width { get; set; }
        /// Height of the medium preview image
        public int medium_image_height { get; set; }
        /// URL to a large preview image of the video
        public string large_image_url { get; set; }
        /// Width of the large preview image
        public int large_image_width { get; set; }
        /// Height of the large preview image
        public int large_image_height { get; set; }

        /// <summary>
        /// Represents a video that has been sent as a Facebook attachment
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="size"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="duration"></param>
        /// <param name="preview_url"></param>
        /// <param name="small_image"></param>
        /// <param name="medium_image"></param>
        /// <param name="large_image"></param>
        public FB_VideoAttachment(string uid = null, int size = 0, int width = 0, int height = 0, int duration = 0, string preview_url = null, JToken small_image = null, JToken medium_image = null, JToken large_image = null)
            : base(uid)
        {
            this.size = size;
            this.width = width;
            this.height = height;
            this.duration = duration;
            this.preview_url = preview_url;

            if (small_image != null)
            {
                this.small_image_url = small_image.get("uri")?.Value<string>();
                this.small_image_width = small_image.get("width")?.Value<int>() ?? 0;
                this.small_image_height = small_image.get("height")?.Value<int>() ?? 0;
            }
            if (medium_image != null)
            {
                this.medium_image_url = medium_image.get("uri")?.Value<string>();
                this.medium_image_width = medium_image.get("width")?.Value<int>() ?? 0;
                this.medium_image_height = medium_image.get("height")?.Value<int>() ?? 0;
            }
            if (large_image != null)
            {
                this.large_image_url = large_image.get("uri")?.Value<string>();
                this.large_image_width = large_image.get("width")?.Value<int>() ?? 0;
                this.large_image_height = large_image.get("height")?.Value<int>() ?? 0;
            }
        }

        public static FB_VideoAttachment _from_graphql(JToken data)
        {
            return new FB_VideoAttachment(
                    width: data.get("original_dimensions")?.get("width")?.Value<int>() ?? 0,
                    height: data.get("original_dimensions")?.get("height")?.Value<int>() ?? 0,
                    duration: data.get("playable_duration_in_ms")?.Value<int>() ?? 0,
                    preview_url: data.get("playable_url")?.Value<string>(),
                    small_image: data.get("chat_image"),
                    medium_image: data.get("inbox_image"),
                    large_image: data.get("large_image"),
                    uid: data.get("legacy_attachment_id")?.Value<string>());
        }

        public static FB_VideoAttachment _from_list(JToken data)
        {
            data = data["node"];
            return new FB_VideoAttachment(
                width: data?.get("original_dimensions")?.get("x")?.Value<int>() ?? 0,
                height: data?.get("original_dimensions")?.get("y")?.Value<int>() ?? 0,
                small_image: data?.get("image"),
                medium_image: data?.get("image1"),
                large_image: data?.get("image2"),
                uid: data?.get("legacy_attachment_id")?.Value<string>()
            );
        }

        public static FB_VideoAttachment _from_subattachment(JToken data)
        {
            JToken media = data.get("media");
            return new FB_VideoAttachment(
                duration: media.get("playable_duration_in_ms")?.Value<int>() ?? 0,
                preview_url: media.get("playable_url")?.Value<string>(),
                medium_image: media.get("image"),
                uid: data.get("target")?.get("video_id")?.Value<string>());
        }
    }
}
