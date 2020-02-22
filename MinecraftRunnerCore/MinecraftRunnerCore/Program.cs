using MinecraftRunnerCore.Server;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftRunnerCore
{
    class Program
    {
        public static HttpClient HttpClient = new HttpClient();

        static void Main(string[] args) => new Program().MainAsync(args).GetAwaiter().GetResult();

        public async Task MainAsync(string[] args)
        {
            using var cancellationToken = new CancellationTokenSource();
            var settingsRunPath = Path.Combine("Config", "Settings.json");
            if (!File.Exists(settingsRunPath))
            {
                File.Copy("Settings.json", settingsRunPath);
            }

            var cachePath = Path.Combine("Config", "Cache.json");
            var cacheExists = File.Exists(cachePath);
            if(!cacheExists)
            {
                File.WriteAllText(cachePath, "{}");
            }

            Settings settings = Settings.FromFile(settingsRunPath);
            Cache cache = Cache.FromFile(cachePath);
            if (!cacheExists) cache.Flush();
            var runner = new MinecraftRunner(Directory.GetCurrentDirectory(), settings, cache, cancellationToken.Token);
            Console.CancelKeyPress += new ConsoleCancelEventHandler(delegate (object sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine("Cancellation received");
                cancellationToken.Cancel();
                e.Cancel = true;
            });
            AssemblyLoadContext.Default.Unloading += ctx =>
            {
                Console.WriteLine("Cancellation received");
                try
                {
                    cancellationToken.Cancel();
                }
                catch (Exception e) { }
                CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while(runner.Running && !timeout.Token.IsCancellationRequested) { /* Do Nothing */}
            };  
            await runner.StartAsync();
            Console.WriteLine("Closing");
        }
    }
}
