using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MinecraftRunnerCore.Server;

namespace MinecraftRunnerCore
{
    class MinecraftRunner
    {
        private const string MinecraftServerFolderName = "mcserver";
        private string RootDirectory { get; }
        private MinecraftServer Server { get; }
        private ServerHub Hub { get; }
        private Settings Settings { get; }
        public string MinecraftServerFolder { get; }
        private CancellationToken Token { get; }
        public MinecraftRunner(string rootDirectory, Settings settings, CancellationToken token)
        {
            RootDirectory = rootDirectory;
            MinecraftServerFolder = Path.Combine(RootDirectory, MinecraftServerFolderName);
            Hub = new ServerHub(new Uri(settings.HubUrl));
            Settings = settings;
            Server = new MinecraftServer(this, Hub, MinecraftServerFolder, settings);
            Token = token;
        }

        public async Task StartAsync()
        {
            Hub.BeginConnectionLoop();
            Server.StartRunLoop();

            await Task.Factory.StartNew(() =>
            {
                while(!Token.IsCancellationRequested)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }, TaskCreationOptions.LongRunning);

            await Server.StopRunLoopAsync();
            Hub.EndConnectionLoop();
        }
    }
}
