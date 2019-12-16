using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace Spoilers.Scryfall
{
    class ScryfallClient
    {
        public async Task<List<Card>> FetchSetCards(string setId) {
            var cards = new List<Card>();

            string query = HttpUtility.UrlEncode($"e:{setId}");
            string url = $"https://api.scryfall.com/cards/search?order=set&unique=cards&q={query}";

            while (true) {
                using (var response = await GetAsyncThrottled(url)) {
                    response.EnsureSuccessStatusCode();

                    string body = await response.Content.ReadAsStringAsync();
                    var results = JsonConvert.DeserializeObject<CardSearchResults>(body);

                    cards.AddRange(results.Cards);

                    if (!results.HasMore) {
                        break;
                    }

                    url = results.NextPageURL;
                }
            }

            return cards;
        }

        Task<HttpResponseMessage> GetAsyncThrottled(string url) {
            return _throttle.Invoke(() => _client.GetAsync(url));
        }

        HttpClient _client = new HttpClient(new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.All
        });

        Throttle _throttle = new Throttle(TimeSpan.FromMilliseconds(500));
    }

    class Images
    {
        [JsonProperty("small")]
        public string SmallURL;

        [JsonProperty("normal")]
        public string MediumURL;

        [JsonProperty("large")]
        public string LargeURL;
    }

    class Card
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("image_uris")]
        public Images Images;

        [JsonProperty("mana_cost")]
        public string ManaCost;

        [JsonProperty("type_line")]
        public string TypeLine;

        [JsonProperty("preview")]
        public Preview Preview;
    }

    class Preview
    {
        [JsonProperty("source")]
        public string SourceName;

        [JsonProperty("source_uri")]
        public string SourceURL;

        [JsonProperty("previewed_at")]
        public string PreviewedAt;
    }

    class CardSearchResults
    {
        [JsonProperty("has_more")]
        public bool HasMore;

        [JsonProperty("next_page")]
        public string NextPageURL;

        [JsonProperty("data")]
        public List<Card> Cards = new List<Card>();
    }
}