using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spoilers.Netlify
{
    class NetlifyClient
    {
        public NetlifyClient(string accessToken, string siteId) {
            _accessToken = accessToken;
            _siteId = siteId;
        }

        public async Task Deploy(string message, Dictionary<string, byte[]> files) {
            string deployId = await CreateDeployment(message, files);
            foreach (var (path, content) in files) {
                await UploadFile(deployId, path, content);
            }
        }

        public async Task<byte[]> GetFile(string path) {
            var url = URL($"/sites/{HttpUtility.UrlEncode(_siteId)}/files{path}");
            using (var request = new HttpRequestMessage(HttpMethod.Get, url)) {
                request.Content = new StringContent("", Encoding.UTF8, "application/vnd.bitballoon.v1.raw");
                using (var response = await _client.SendAsync(request)) {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
        }

        async Task<string> CreateDeployment(string message, Dictionary<string, byte[]> files) {
            dynamic body = new {
                title = message,
                files = files.ToDictionary(entry => entry.Key, entry => SHA1Hash(entry.Value))
            };

            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

            var url = URL($"/sites/{HttpUtility.UrlEncode(_siteId)}/deploys");
            using (var response = await _client.PostAsync(url, content)) {
                response.EnsureSuccessStatusCode();

                var json = JToken.Parse(await response.Content.ReadAsStringAsync());
                return (string)json["id"];
            }
        }

        async Task UploadFile(string deployId, string path, byte[] content) {
            var url = URL($"/deploys/{HttpUtility.UrlEncode(deployId)}/files{path}");
            using (var response = await _client.PutAsync(url, new ByteArrayContent(content))) {
                response.EnsureSuccessStatusCode();
            }
        }

        string SHA1Hash(byte[] bytes) {
            using (SHA1Managed sha1 = new SHA1Managed()) {
                var hash = sha1.ComputeHash(bytes);
                return string.Join("", hash.Select(b => b.ToString("x2")));
            }
        }

        string URL(string path) {
            return $"https://api.netlify.com/api/v1{path}?access_token={HttpUtility.UrlEncode(_accessToken)}";
        }

        string _accessToken;
        string _siteId;

        HttpClient _client = new HttpClient(new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.All
        });
    }
}