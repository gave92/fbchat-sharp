using Newtonsoft.Json.Linq;
using System;

namespace fbchat_sharp.API
{
    /// <summary>
    /// Represents a user location
    /// Latitude and longitude OR address is provided by Facebook
    /// </summary>
    public class FB_LocationAttachment : FB_Attachment
    {
        /// Latitude of the location
        public double latitude { get; set; }
        /// Longitude of the location
        public double longitude { get; set; }
        /// Image showing the map of the location
        public FB_Image image { get; set; }
        /// URL to Bing maps with the location
        public string url { get; set; }
        /// Address of the location
        public string address { get; set; }

        /// <summary>
        /// Represents a user location
        /// Latitude and longitude OR address is provided by Facebook
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="image"></param>
        /// <param name="url"></param>
        /// <param name="address"></param>
        public FB_LocationAttachment(string uid = null, double latitude = 0, double longitude = 0, FB_Image image = null, string url = null, string address = null)
            : base(uid)
        {
            this.latitude = latitude;
            this.longitude = longitude;
            this.image = image;
            this.url = url;
            this.address = address;
        }

        internal static FB_LocationAttachment _from_graphql(JToken data)
        {
            var url = data.get("url")?.Value<string>();
            var address = Utils.get_url_parameter(Utils.get_url_parameter(url, "u"), "where1");
            double latitude = 0, longitude = 0;
            try
            {
                var split = address.Split(new[] { ", " }, StringSplitOptions.None);
                latitude = Double.Parse(split[0]);
                longitude = Double.Parse(split[1]);
                address = null;
            }
            catch (Exception)
            {
            }

            var rtn = new FB_LocationAttachment(
                uid: data.get("deduplication_key")?.Value<string>(),
                latitude: latitude,
                longitude: longitude,
                address: address);

            var media = data.get("media");
            if (media != null && media.get("image") != null)
            {
                rtn.image = FB_Image._from_uri_or_none(media?.get("image"));
            }
            rtn.url = url;

            return rtn;
        }
    }

    /// <summary>
    /// Represents a live user location
    /// </summary>
    public class FB_LiveLocationAttachment : FB_LocationAttachment
    {
        /// Name of the location
        public string name { get; set; }
        /// Timestamp when live location expires
        public string expiration_time { get; set; }
        /// True if live location is expired
        public bool is_expired { get; set; }

        /// <summary>
        /// Represents a live user location
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="image"></param>
        /// <param name="url"></param>
        /// <param name="address"></param>
        /// <param name="name"></param>
        /// <param name="expiration_time"></param>
        /// <param name="is_expired"></param>
        public FB_LiveLocationAttachment(string uid = null, double latitude = 0, double longitude = 0, FB_Image image = null, string url = null, string address = null, string name = null, string expiration_time = null, bool is_expired = false)
            : base(uid, latitude, longitude, image, url, address)
        {
            this.name = name;
            this.expiration_time = expiration_time;
            this.is_expired = is_expired;
        }

        internal static FB_LiveLocationAttachment _from_pull(JToken data)
        {
            return new FB_LiveLocationAttachment(
                uid: data.get("id")?.Value<string>(),
                latitude: ((data.get("stopReason") == null) ? data.get("coordinate")?.get("latitude")?.Value<double>() ?? 0 : 0) / Math.Pow(10, 8),
                longitude: ((data.get("stopReason") == null) ? data.get("coordinate")?.get("longitude")?.Value<double>() ?? 0 : 0) / Math.Pow(10, 8),
                name: data.get("locationTitle")?.Value<string>(),
                expiration_time: data.get("expirationTime")?.Value<string>(),
                is_expired: data.get("stopReason")?.Value<bool>() ?? false);
        }

        internal static new FB_LiveLocationAttachment _from_graphql(JToken data)
        {
            var target = data.get("target");
            var rtn = new FB_LiveLocationAttachment(
                uid: target.get("live_location_id")?.Value<string>(),
                latitude: target.get("coordinate")?.get("latitude")?.Value<double>() ?? 0,
                longitude: target.get("coordinate")?.get("longitude")?.Value<double>() ?? 0,
                name: data.get("title_with_entities")?.get("text")?.Value<string>(),
                expiration_time: target.get("expiration_time")?.Value<string>(),
                is_expired: target.get("is_expired")?.Value<bool>() ?? false);

            var media = data.get("media");
            if (media != null && media.get("image") != null) {
                rtn.image = FB_Image._from_uri_or_none(media?.get("image"));
            }                
            rtn.url = data.get("url")?.Value<string>();

            return rtn;
        }
    }
}

