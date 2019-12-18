using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reddit;

namespace Spoilers
{
    static class Reddit
    {
        public static async Task<RedditClient> CreateClient() {
            string accessToken = await GenerateAccessToken();
            return new RedditClient(accessToken: accessToken);
        }

        static async Task<string> GenerateAccessToken() {
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })) {
                string authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:"));

                var request = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token") {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                        { "grant_type", "https://oauth.reddit.com/grants/installed_client" },
                        { "device_id", "DO_NOT_TRACK_THIS_DEVICE" }
                    }),
                    Headers = {
                        { "Authorization", $"Basic {authToken}" }
                    }
                };

                using (request) {
                    using (var response = await client.SendAsync(request)) {
                        response.EnsureSuccessStatusCode();

                        string body = await response.Content.ReadAsStringAsync();
                        var results = JObject.Parse(body);

                        return (string)results["access_token"];
                    }
                }
            }
        }

        static readonly string ClientId = "0Ry1TaKGFLtP5Q";
    }
}