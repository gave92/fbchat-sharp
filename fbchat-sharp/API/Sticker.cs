using Newtonsoft.Json.Linq;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Represents a Facebook sticker that has been sent to a Facebook thread as an attachment
    /// </summary>
    public class FB_Sticker : FB_Attachment
    {
        /// The sticker-pack's ID
        public string pack { get; set; }
        /// Whether the sticker is animated
        public bool is_animated { get; set; }
        /// If the sticker is animated, the following should be present
        /// URL to a medium spritemap
        public string medium_sprite_image { get; set; }
        /// URL to a large spritemap
        public string large_sprite_image { get; set; }
        /// The amount of frames present in the spritemap pr. row
        public int frames_per_row { get; set; }
        /// The amount of frames present in the spritemap pr. coloumn
        public int frames_per_col { get; set; }
        /// The frame rate the spritemap is intended to be played in
        public float frame_rate { get; set; }
        /// URL to the sticker's image
        public string url { get; set; }
        /// Width of the sticker
        public float width { get; set; }
        /// Height of the sticker
        public float height { get; set; }
        /// The sticker's label/name
        public string label { get; set; }

        public FB_Sticker(string uid = null) : base(uid)
        {
        }

        public static FB_Sticker _from_graphql(JToken data)
        {
            if (data == null)
            {
                return null;
            }
            var sticker = new FB_Sticker(uid: data.get("id")?.Value<string>());
            if (data.get("pack") != null)
            {
                sticker.pack = data.get("pack")?.get("id")?.Value<string>();
            }
            if (data.get("sprite_image") != null)
            {
                sticker.is_animated = true;
                sticker.medium_sprite_image = data.get("sprite_image")?.get("uri")?.Value<string>();
                sticker.large_sprite_image = data.get("sprite_image_2x")?.get("uri")?.Value<string>();
                sticker.frames_per_row = data.get("frames_per_row")?.Value<int>() ?? 0;
                sticker.frames_per_col = data.get("frames_per_column")?.Value<int>() ?? 0;
                sticker.frame_rate = data.get("frame_rate")?.Value<float>() ?? 0;
            }
            sticker.url = data.get("url")?.Value<string>();
            sticker.width = data.get("width")?.Value<int>() ?? 0;
            sticker.height = data.get("height")?.Value<int>() ?? 0;
            if (data.get("label") != null)
            {
                sticker.label = data.get("label")?.Value<string>();
            }
            return sticker;
        }
    }
}
