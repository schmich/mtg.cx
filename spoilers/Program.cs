using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Spoilers
{
    class Program
    {
        static async Task<int> Main(string[] args) {
            if (args.Length < 2) {
                Console.WriteLine("spoilers <input> <output>");
                return 1;
            }

            string inputFilename = args[0];
            string outputFilename = args[1];

            var spoilers = new List<Spoiler>();
            if (File.Exists(inputFilename)) {
                string content = File.ReadAllText(inputFilename);
                spoilers = JsonConvert.DeserializeObject<List<Spoiler>>(content);
            }

            spoilers = await Spoilers.Update(spoilers);
            string output = JsonConvert.SerializeObject(spoilers);
            File.WriteAllText(outputFilename, output);

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
