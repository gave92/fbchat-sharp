using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace fbchat_sharp.API.Models
{
    public class State
    {
        public async Task<object> _cleanGet(string url, int timeout = 30)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var header in this._header) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            // this._http_client.Timeout = TimeSpan.FromSeconds(timeout);
            var response = await this._http_client.SendAsync(request);

            if ((int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                return response;
            else
                return await _cleanGet(response.Headers.Location.ToString(), timeout);
        }

        public List<FB_File> get_files_from_paths(Dictionary<string,Stream> file_paths)
        {
            var files = new List<FB_File>();
            foreach (var file_path in file_paths)
            {
                var file = new FB_File();
                file.data = file_path.Value;
                file.path = Path.GetFileName(file_path.Key);
                file.mimetype = MimeMapping.MimeUtility.GetMimeMapping(file.path);
                files.Add(file);
            }
            return files;
        }

        public async Task<List<FB_File>> get_files_from_urls(ISet<string> file_urls)
        {
            var files = new List<FB_File>();
            foreach (var file_url in file_urls)
            {
                var r = (HttpResponseMessage)(await this._cleanGet(file_url));
                // We could possibly use r.headers.get('Content-Disposition'), see
                // https://stackoverflow.com/a/37060758
                var file = new FB_File();
                file.data = await r.Content.ReadAsStreamAsync();
                file.path = Utils.GetFileNameFromUrl(file_url);
                file.mimetype = r.Content.Headers.ContentType.MediaType;
                files.Add(file);
            }
            return files;
        }
    }
}
