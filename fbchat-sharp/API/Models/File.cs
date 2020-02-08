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
        /// A thumbnail of the image
        public FB_Image thumbnail { get; set; }
        /// A medium preview of the image
        public FB_Image preview { get; set; }
        /// A large preview of the image
        public FB_Image large_preview { get; set; }
        /// An animated preview of the image (e.g. for GIFs)
        public FB_Image animated_preview { get; set; }

        /// <summary>
        /// Represents an image that has been sent as a Facebook attachment
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="original_extension"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="is_animated"></param>
        /// <param name="thumbnail"></param>
        /// <param name="preview"></param>
        /// <param name="large_preview"></param>
        /// <param name="animated_preview"></param>
        public FB_ImageAttachment(string uid = null, string original_extension = null, int width = 0, int height = 0, bool is_animated = false, FB_Image thumbnail = null, FB_Image preview = null, FB_Image large_preview = null, FB_Image animated_preview = null)
            : base(uid)
        {
            this.original_extension = original_extension;
            this.width = width;
            this.height = height;
            this.is_animated = is_animated;
            this.thumbnail = thumbnail;
            this.preview = preview;
            this.large_preview = large_preview;
            this.animated_preview = animated_preview;
        }

        public static FB_ImageAttachment _from_graphql(JToken data)
        {
            return new FB_ImageAttachment(
                    original_extension: data.get("original_extension")?.Value<string>() ?? data.get("filename")?.Value<string>()?.Split('-')[0],
                    width: data.get("original_dimensions")?.get("width")?.Value<int>() ?? 0,
                    height: data.get("original_dimensions")?.get("height")?.Value<int>() ?? 0,
                    is_animated: data.get("__typename")?.Value<String>() == "MessageAnimatedImage",
                    thumbnail: FB_Image._from_uri_or_none(data?.get("thumbnail")),
                    preview: FB_Image._from_uri_or_none(data?.get("preview")) ?? FB_Image._from_uri_or_none(data?.get("preview_image")),
                    large_preview: FB_Image._from_uri_or_none(data?.get("large_preview")),
                    animated_preview: FB_Image._from_uri_or_none(data?.get("animated_image")),
                    uid: data.get("legacy_attachment_id")?.Value<string>());
        }

        public static FB_ImageAttachment _from_list(JToken data)
        {
            return new FB_ImageAttachment(
                width: data?.get("original_dimensions")?.get("x")?.Value<int>() ?? 0,
                height: data?.get("original_dimensions")?.get("y")?.Value<int>() ?? 0,
                thumbnail: FB_Image._from_uri_or_none(data?.get("image")),
                large_preview: FB_Image._from_uri_or_none(data?.get("image2")),
                preview: FB_Image._from_uri_or_none(data?.get("image1")),
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
        /// A small preview image of the video
        public FB_Image small_image { get; set; }
        /// A medium preview image of the video
        public FB_Image medium_image { get; set; }
        /// A large preview image of the video
        public FB_Image large_image { get; set; }

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
        public FB_VideoAttachment(string uid = null, int size = 0, int width = 0, int height = 0, int duration = 0, string preview_url = null, FB_Image small_image = null, FB_Image medium_image = null, FB_Image large_image = null)
            : base(uid)
        {
            this.size = size;
            this.width = width;
            this.height = height;
            this.duration = duration;
            this.preview_url = preview_url;
            this.small_image = small_image;
            this.medium_image = medium_image;
            this.large_image = large_image;
        }

        public static FB_VideoAttachment _from_graphql(JToken data)
        {
            return new FB_VideoAttachment(
                    width: data.get("original_dimensions")?.get("width")?.Value<int>() ?? 0,
                    height: data.get("original_dimensions")?.get("height")?.Value<int>() ?? 0,
                    duration: data.get("playable_duration_in_ms")?.Value<int>() ?? 0,
                    preview_url: data.get("playable_url")?.Value<string>(),
                    small_image: FB_Image._from_uri_or_none(data?.get("chat_image")),
                    medium_image: FB_Image._from_uri_or_none(data?.get("inbox_image")),
                    large_image: FB_Image._from_uri_or_none(data?.get("large_image")),
                    uid: data.get("legacy_attachment_id")?.Value<string>());
        }

        public static FB_VideoAttachment _from_list(JToken data)
        {
            return new FB_VideoAttachment(
                width: data?.get("original_dimensions")?.get("x")?.Value<int>() ?? 0,
                height: data?.get("original_dimensions")?.get("y")?.Value<int>() ?? 0,
                small_image: FB_Image._from_uri_or_none(data?.get("image")),
                medium_image: FB_Image._from_uri_or_none(data?.get("image1")),
                large_image: FB_Image._from_uri_or_none(data?.get("image2")),
                uid: data?.get("legacy_attachment_id")?.Value<string>()
            );
        }

        public static FB_VideoAttachment _from_subattachment(JToken data)
        {
            JToken media = data.get("media");
            return new FB_VideoAttachment(
                duration: media.get("playable_duration_in_ms")?.Value<int>() ?? 0,
                preview_url: media.get("playable_url")?.Value<string>(),
                medium_image: FB_Image._from_uri_or_none(media?.get("image")),
                uid: data.get("target")?.get("video_id")?.Value<string>());
        }
    }
}
