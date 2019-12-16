using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Spoilers
{
    class ImgurClient
    {
        public async Task<string> FetchMediaURL(Uri imgurURL) {
            return await _throttle.Invoke(async () => {
                var builder = new UriBuilder(imgurURL);
                builder.Host = "imgur.com";
                imgurURL = builder.Uri;

                using (var response = await _client.GetAsync(imgurURL)) {
                    if (response.StatusCode != HttpStatusCode.OK) {
                        return null;
                    }

                    string body = await response.Content.ReadAsStringAsync();
                    var match = _imagePattern.Match(body);
                    if (!match.Success) {
                        return null;
                    }

                    return match.Groups["url"].Captures[0].Value;
                }
            });
        }

        Regex _imagePattern = new Regex("<link.*?rel=\"image_src\".*?href=\"(?<url>.*?)\"", RegexOptions.IgnoreCase);

        HttpClient _client = new HttpClient(new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.All
        });

        Throttle _throttle = new Throttle(TimeSpan.FromMilliseconds(250));
    }
}