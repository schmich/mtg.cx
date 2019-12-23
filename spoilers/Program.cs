using System;
using System.Collections.Generic;
using System.Linq;
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
            var deploys = await netlify.GetDeploys();
            var latestDeploy = deploys.Where(d => d.State == "ready").OrderByDescending(d => d.CreatedAt).First();

            Console.WriteLine("Fetch latest spoilers.json.");

            string spoilersPath = "/spoilers.json";
            var input = Encoding.UTF8.GetString(await netlify.GetFileContents(spoilersPath, deployId: latestDeploy.Id));
            var spoilers = JsonConvert.DeserializeObject<List<Spoiler>>(input);

            spoilers = await Spoilers.Update(spoilers);

            Console.WriteLine("Deploy spoilers.json.");

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
