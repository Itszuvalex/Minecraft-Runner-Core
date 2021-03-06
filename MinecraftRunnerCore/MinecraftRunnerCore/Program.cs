﻿using MinecraftRunnerCore.Server;
using System;
using System.IO;
using System.Net.Http;
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
            var settings = Settings.FromFile("Settings.json");
            var runner = new MinecraftRunner(Directory.GetCurrentDirectory(), settings, cancellationToken.Token);
            Console.CancelKeyPress += new ConsoleCancelEventHandler(delegate (object sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine("Cancellation received");
                cancellationToken.Cancel();
                e.Cancel = true;
            });
            await runner.StartAsync();
            Console.WriteLine("Closing");
        }
    }
}
