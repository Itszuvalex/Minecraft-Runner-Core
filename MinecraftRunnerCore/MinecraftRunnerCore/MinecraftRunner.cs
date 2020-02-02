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
        public MinecraftRunner(string rootDirectory, Settings settings, ServerHub hub)
        {
            RootDirectory = rootDirectory;
            MinecraftServerFolder = Path.Combine(RootDirectory, MinecraftServerFolderName);
            Hub = hub;
            Settings = settings;
            Server = new MinecraftServer(this, Hub, MinecraftServerFolder);
        }

        public async Task StartAsync(CancellationToken token)
        {
            try
            {
                Server.Install(Settings.McVer, Settings.ForgeVer, Settings.LaunchWrapperVer, force: false);
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Encountered exception when attempting to install = {0}", e));
            }

            await Task.Factory.StartNew(delegate
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        MainLoopIteration();
                    }
                    Console.WriteLine("Task Cancelled");
                }
                catch (Exception e)
                {
                    Console.WriteLine(String.Format("Encountered exception during main loop = {0}", e));
                }
                finally
                {
                    Server.Stop();
                }
            }, token, TaskCreationOptions.LongRunning);
        }

        private void MainLoopIteration()
        {
            if (!Server.Running)
            {
                Console.WriteLine("McServer isn't running.");
                Server.StartAsync().Wait();
            }
        }

    }
}
