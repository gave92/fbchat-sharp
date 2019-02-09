using fbchat_sharp.API;
using Polenter.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace examples
{
    // Like FBClient_Simple but also saves session cookies
    public class FBClient_Wpf : MessengerClient
    {
        private static readonly string appName = "FBChat-Sharp";
        private static readonly string sessionFile = "SESSION_COOKIES.dat";

        protected override async Task DeleteCookiesAsync()
        {
            try
            {
                var file = Path.Combine(UserDataFolder, sessionFile);
                File.Delete(file);
            }
            catch (Exception ex)
            {
                this.Log(ex.ToString());
            }
        }      

        protected override async Task<List<Cookie>> ReadCookiesFromDiskAsync()
        {
            try
            {
                var file = Path.Combine(UserDataFolder, sessionFile);
                using (var fileStream = File.OpenRead(file))
                {
                    var settings = new SharpSerializerBinarySettings(BinarySerializationMode.Burst);
                    var serializer = new SharpSerializer(settings);
                    return (List<Cookie>)serializer.Deserialize(fileStream);
                }
            }
            catch (Exception ex)
            {
                this.Log(string.Format("Problem reading cookies from disk: {0}", ex.ToString()));
                return null;
            }
        }

        protected override async Task WriteCookiesToDiskAsync(List<Cookie> cookieJar)
        {
            var file = Path.Combine(UserDataFolder, sessionFile);

            using (var fileStream = File.Create(file))
            {
                try
                {
                    var settings = new SharpSerializerBinarySettings(BinarySerializationMode.Burst);
                    var serializer = new SharpSerializer(settings);
                    serializer.Serialize(cookieJar, fileStream);
                    await fileStream.FlushAsync();
                }
                catch (Exception ex)
                {
                    this.Log(string.Format("Problem writing cookies to disk: {0}", ex.ToString()));
                }
            }
        }

        /// <summary>
        /// Get the current user data folder
        /// </summary>
        private static string UserDataFolder
        {
            get
            {
                string folderBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string dir = Path.Combine(folderBase, appName.ToUpper());
                return CheckDir(dir);
            }
        }

        /// <summary>
        /// Check the specified folder, and create if it doesn't exist.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private static string CheckDir(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }
    }
}
