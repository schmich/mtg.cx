using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Spoilers.Twitter
{
    class TwitterClient
    {
        public async Task<string> FetchMediaURL(Uri tweetURL) {
            return await _throttle.Invoke(async () => {
                var builder = new UriBuilder(tweetURL);
                builder.Host = "twitter.com";
                tweetURL = builder.Uri;

                using (var response = await _client.GetAsync(tweetURL)) {
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

        Regex _imagePattern = new Regex("<meta.*?property=\"og:image\".*?content=\"(?<url>.*?)\"", RegexOptions.IgnoreCase);

        HttpClient _client = new HttpClient(new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.All
        });

        Throttle _throttle = new Throttle(TimeSpan.FromMilliseconds(250));
    }
}