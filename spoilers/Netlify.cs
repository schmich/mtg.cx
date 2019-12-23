using System;
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

        public async Task<List<Deploy>> GetDeploys() {
            var url = BuildURL($"/sites/{HttpUtility.UrlEncode(_siteId)}/deploys");
            using (var response = await _client.GetAsync(url)) {
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<Deploy>>(json);
            }
        }

        public async Task<List<File>> GetFiles(string deployId = default) {
            var query = (deployId == default) ? default : new[] { ("deploy_id", deployId) };
            var url = BuildURL($"/sites/{HttpUtility.UrlEncode(_siteId)}/files", query);
            using (var response = await _client.GetAsync(url)) {
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<File>>(json);
            }
        }

        public async Task<byte[]> GetFileContents(string path, string deployId = default) {
            var query = (deployId == default) ? default : new[] { ("deploy_id", deployId) };
            var url = BuildURL($"/sites/{HttpUtility.UrlEncode(_siteId)}/files{path}", query);
            using (var request = new HttpRequestMessage(HttpMethod.Get, url)) {
                request.Content = new StringContent("", Encoding.UTF8, "application/vnd.bitballoon.v1.raw");
                using (var response = await _client.SendAsync(request)) {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
        }

        async Task<string> CreateDeployment(string message, Dictionary<string, byte[]> files) {
            var deploys = await GetDeploys();
            var latestDeploy = deploys.Where(d => d.State == "ready").OrderByDescending(d => d.CreatedAt).First();
            var latestFiles = await GetFiles(deployId: latestDeploy.Id);

            var fileHashes = latestFiles.ToDictionary(file => file.Path, file => file.SHA1Hash);
            foreach (var entry in files) {
                fileHashes[entry.Key] = SHA1Hash(entry.Value);
            }

            dynamic body = new {
                title = message,
                files = fileHashes
            };

            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

            var url = BuildURL($"/sites/{HttpUtility.UrlEncode(_siteId)}/deploys");
            using (var response = await _client.PostAsync(url, content)) {
                response.EnsureSuccessStatusCode();

                var json = JToken.Parse(await response.Content.ReadAsStringAsync());
                // TODO: Look at json["required"] to determine the files that need to actually be updated.
                return (string)json["id"];
            }
        }

        async Task UploadFile(string deployId, string path, byte[] content) {
            var url = BuildURL($"/deploys/{HttpUtility.UrlEncode(deployId)}/files{path}");
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

        string BuildURL(string path, IEnumerable<(string Name, string Value)> query = default) {
            if (query == default) {
                query = new (string, string)[0];
            }

            query = query.Concat(new[] { ("access_token", _accessToken) });

            var queryPart = "?" + string.Join("&", query.Select(q => $"{HttpUtility.UrlEncode(q.Name)}={HttpUtility.UrlEncode(q.Value)}"));
            return $"https://api.netlify.com/api/v1{path}{queryPart}";
        }

        string _accessToken;
        string _siteId;

        HttpClient _client = new HttpClient(new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.All
        });
    }

    class Deploy
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("state")]
        public string State;

        [JsonProperty("created_at")]
        public DateTime CreatedAt;
    }

    class File
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("site_id")]
        public string SiteId;

        [JsonProperty("deploy_id")]
        public string DeployId;

        [JsonProperty("path")]
        public string Path;

        [JsonProperty("sha")]
        public string SHA1Hash;

        [JsonProperty("mime_type")]
        public string MimeType;

        [JsonProperty("size")]
        public long Size;
    }
}