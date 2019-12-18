using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Spoilers.Netlify;

namespace Spoilers
{
    class Program
    {
        static async Task<int> Main(string[] args) {
            var accessToken = Environment.GetEnvironmentVariable("netlify_access_token");
            var siteId = Environment.GetEnvironmentVariable("netlify_site_id");

            var netlify = new NetlifyClient(accessToken, siteId);
            string spoilersPath = "/spoilers.json";

            var input = Encoding.UTF8.GetString(await netlify.GetFile(spoilersPath));
            var spoilers = JsonConvert.DeserializeObject<List<Spoiler>>(input);

            spoilers = await Spoilers.Update(spoilers);

            var output = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(spoilers));
            await netlify.Deploy("Update spoilers.json.", new Dictionary<string, byte[]> {
                { spoilersPath, output }
            });

            return 0;
        }
    }

    class Throttle
    {
        public Throttle(TimeSpan period) {
            _period = period;
        }

        public T Invoke<T>(Func<T> func) {
            var delta = DateTime.Now - _lastInvoke;
            if (delta < _period) {
                Thread.Sleep(_period - delta);
            }

            try {
                return func();
            } finally {
                _lastInvoke = DateTime.Now;
            }
        }

        DateTime _lastInvoke = DateTime.MinValue;
        TimeSpan _period;
    }
}
