using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Reddit.Controllers;
using Spoilers.Scryfall;

namespace Spoilers
{
    class Spoilers
    {
        public static async Task<List<Spoiler>> Update(List<Spoiler> spoilers) {
            Console.WriteLine("Updating previous Reddit post stats.");

            var reddit = await Reddit.CreateClient();
            var oldSpoilers = spoilers.ToDictionary(s => s.Id, s => s);

            // TODO: Don't update post stats after a few days.
            var updatePosts = oldSpoilers.Values.SelectMany(p => p.Posts).ToDictionary(p => p.Id, p => p);
            var postIds = updatePosts.Values.Select(p => $"t3_{p.Id}");
            foreach (var batch in postIds.Batch(size: 25)) {
                foreach (var updatedPost in reddit.GetPosts(batch.ToList())) {
                    updatePosts[updatedPost.Id].Title = updatedPost.Title;
                    updatePosts[updatedPost.Id].Comments = updatedPost.Listing.NumComments;
                    updatePosts[updatedPost.Id].Score = updatedPost.Listing.Score;
                }
            }

            Console.WriteLine($"Fetch Scryfall cards.");

            var scryfall = new ScryfallClient();
            var cards = await scryfall.FetchSetCards("thb");

            var subredditNames = new[] {
                "magictcg",
                "magicarena",
                "edh",
                "spikes",
                "modernmagic"
            };

            var subreddits = new Dictionary<string, Subreddit>(
                subredditNames.Select(name => KeyValuePair.Create(name.ToLowerInvariant(), reddit.Subreddit(name)))
            );

            foreach (var card in cards) {
                Console.WriteLine($"Process {card.Name}.");

                var spoiler = oldSpoilers.GetValueOrDefault(card.Id);
                if (spoiler == null) {
                    spoiler = new Spoiler {
                        Id = card.Id,
                        Name = card.Name,
                        ManaCost = card.ManaCost,
                        TypeLine = card.TypeLine,
                        Preview = new Preview {
                            SourceName = card.Preview.SourceName,
                            URL = card.Preview.SourceURL,
                            PreviewedAt = card.Preview.PreviewedAt
                        }
                    };

                    spoilers.Add(spoiler);
                } else {
                    spoiler.Name = card.Name;
                    spoiler.ManaCost = card.ManaCost;
                    spoiler.TypeLine = card.TypeLine;
                    spoiler.Preview = new Preview {
                        SourceName = card.Preview.SourceName,
                        URL = card.Preview.SourceURL,
                        PreviewedAt = card.Preview.PreviewedAt
                    };
                }

                var previewDate = DateTime.Parse(card.Preview.PreviewedAt);
                if (DateTime.Now - previewDate > TimeSpan.FromDays(2)) {
                    continue;
                }

                var oldPosts = spoiler.Posts.ToDictionary(p => p.Subreddit.ToLowerInvariant(), p => p);

                foreach (var (name, subreddit) in subreddits) {
                    if (oldPosts.ContainsKey(name)) {
                        continue;
                    }

                    string query = card.Name;
                    var postSources = new Func<List<Post>>[] {
                        () => subreddit.Search(q: query, sort: "relevance", limit: 5, t: "day"),
                        () => subreddit.Posts.Hot,
                        () => subreddit.Posts.New
                    };

                    Post post = null;
                    foreach (var getPosts in postSources) {
                        var candidates = getPosts();
                        post = GetMatchingPost(card.Name, candidates);
                        if (post != null) {
                            break;
                        }
                    }

                    if (post != null) {
                        spoiler.Posts.Add(new RedditPost {
                            Id = post.Id,
                            Title = post.Title,
                            Subreddit = post.Subreddit,
                            Score = post.Listing.Score,
                            Comments = post.Listing.NumComments,
                            CreatedAt = new DateTimeOffset(post.Listing.CreatedUTC).ToUnixTimeSeconds()
                        });
                    }
                }
            }

            return spoilers.OrderByDescending(s => s.Preview.PreviewedAt).ToList();
        }

        static Post GetMatchingPost(string cardName, IEnumerable<Post> posts) {
            var distances = new List<(int Distance, Post Post)>();

            foreach (var post in posts) {
                int distance = TitleDistance(cardName, post.Title);
                distances.Add((distance, post));
            }

            return distances
                .Where(e => e.Distance <= 3)
                .OrderBy(e => e.Distance)
                .Select(e => e.Post)
                .Where(IsSpoilerPost)
                .OrderByDescending(p => p.Listing.Score + p.Listing.NumComments)
                .FirstOrDefault();
        }

        static int TitleDistance(string p, string q) {
            p = NormalizeTitle(p);
            q = NormalizeTitle(q);
            return LevenshteinDistance(p, q);
        }

        static string NormalizeTitle(string s) {
            return Nonalpha.Replace(Trivial.Replace(Trailing.Replace(Brackets.Replace(s, ""), ""), ""), "").Trim().ToLower();
        }

        static int LevenshteinDistance(string p, string q)
        {
            int plen = p.Length;
            int qlen = q.Length;

            var distance = new int[plen + 1, qlen + 1];

            if (plen == 0) {
                return qlen;
            }

            if (qlen == 0) {
                return plen;
            }

            for (int i = 0; i <= plen; distance[i, 0] = i++) {}
            for (int j = 0; j <= qlen; distance[0, j] = j++) {}

            for (int i = 1; i <= plen; i++) {
                for (int j = 1; j <= qlen; j++) {
                    int cost = (q[j - 1] == p[i - 1]) ? 0 : 1;

                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[plen, qlen];
        }

        static bool IsSpoilerPost(Post post) {
            return post.Listing.NumComments > 10
                && post.Listing.Score > 10
                && !TitleExclusion.IsMatch(post.Title);
        }

        static readonly Regex Brackets = new Regex(@"[\(\[\{<].*?[\)\]\}>]", RegexOptions.Compiled);
        static readonly Regex Trailing = new Regex(@"\s+[\-–—]+\s+.*$", RegexOptions.Compiled);
        static readonly Regex Trivial = new Regex(@"\b(a|an|of|the|on|by|and|at|be|do|is|it|in|to)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex Nonalpha = new Regex(@"[^a-z]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex TitleExclusion = new Regex(@"\bart\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    class Spoiler
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("cost")]
        public string ManaCost;

        [JsonProperty("type")]
        public string TypeLine;

        [JsonProperty("posts")]
        public List<RedditPost> Posts = new List<RedditPost>();

        [JsonProperty("preview")]
        public Preview Preview;
    }

    class Preview
    {
        [JsonProperty("source")]
        public string SourceName;

        [JsonProperty("url")]
        public string URL;

        [JsonProperty("previewed_at")]
        public string PreviewedAt;
    }

    class RedditPost
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("title")]
        public string Title;

        [JsonProperty("subreddit")]
        public string Subreddit;

        [JsonProperty("score")]
        public int Score;

        [JsonProperty("comments")]
        public int Comments;

        [JsonProperty("created_at")]
        public long CreatedAt;
    }

    static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size) {
            if (size <= 0) {
                throw new ArgumentOutOfRangeException(nameof(size), "Must be greater than zero.");
            }

            using (var it = source.GetEnumerator()) {
                while (it.MoveNext()) {
                    int i = 0;
                    IEnumerable<T> batch() {
                        do {
                            yield return it.Current;
                        } while (++i < size && it.MoveNext());
                    };

                    yield return batch();
                    while (++i < size && it.MoveNext());
                }
            }
        }
    }
}