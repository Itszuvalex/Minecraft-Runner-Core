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

namespace MinecraftRunnerCore
{
    class MinecraftRunner
    {
        private const string InstallerJarRegexString = "forge-(.*?)-(.*?)-installer\\.jar";
        private static Regex InstallerJarRegex = new Regex(InstallerJarRegexString);
        private const string UniversalJarRegexString = "forge-(.*?)universal.jar";
        private static Regex UniversalJarRegex = new Regex(UniversalJarRegexString);
        private const string MinecraftServerFolderName = "mcserver";
        private string RootDirectory { get; }
        private string MinecraftServerFolder { get; }
        public MinecraftRunner(string rootDirectory)
        {
            RootDirectory = rootDirectory;
            MinecraftServerFolder = Path.Combine(RootDirectory, MinecraftServerFolderName);
        }

        public async Task StartAsync(CancellationToken token)
        {
            var MCVER = "1.12.2";
            var FORGEVER = "14.23.5.2838";
            var launchwrapper = "1.12";
            Install(MCVER, FORGEVER, launchwrapper, force: false);

            await Task.Factory.StartNew(delegate
            {
                while (!token.IsCancellationRequested)
                {
                    MainLoopIteration();
                }
                Console.WriteLine("Task Cancelled");
            }, token, TaskCreationOptions.LongRunning);
        }

        private void MainLoopIteration()
        {
        }

        private void Install(string mcversion, string forgeversion, string launchwrapperversion, bool force)
        {
            if (!Directory.Exists(MinecraftServerFolder))
                throw new DirectoryNotFoundException(MinecraftServerFolder);

            if (File.Exists(Path.Combine(MinecraftServerFolder, "eula.txt")) && !force)
            {
                AcceptEula().Wait();
                Console.WriteLine("Already Installed");
                return;
            }

            var matches = Directory.EnumerateFiles(MinecraftServerFolder).Where((file) => InstallerJarRegex.Match(file).Success);
            if (matches.Count() > 0)
            {
                Process installer = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = String.Format("-jar \"{0}\" --installServer", matches.First()),
                        WorkingDirectory = MinecraftServerFolder
                    }
                };
                installer.Start();
                installer.WaitForExit();
            }
            else
            {
                const string installerJarName = "forge-universal.jar";
                string installerJarFile = Path.Combine(MinecraftServerFolder, installerJarName);
                string universalJarName = String.Format("forge-{0}-{1}-universal.jar", mcversion, forgeversion);
                string installerNetPath = String.Format("https://files.minecraftforge.net/maven/net/minecraftforge/forge/{0}-{1}/{2}", mcversion, forgeversion, universalJarName);
                var downloadInstallerTask = DownloadFile(installerJarName, installerNetPath, returnIfExists: true);

                string serverJarName = String.Format("minecraft_server.{0}.jar", mcversion);
                string serverNetPath = String.Format("https://s3.amazonaws.com/Minecraft.Download/versions/{0}/{1}", mcversion, serverJarName);
                var downloadServerTask = DownloadFile(serverJarName, serverNetPath, returnIfExists: true);

                string launchwrapperJarName = String.Format("launchwrapper-{0}.jar", launchwrapperversion);
                string launchwrapperNetPath = String.Format("https://libraries.minecraft.net/net/minecraft/launchwrapper/{0}/{1}", launchwrapperversion, launchwrapperJarName);
                string launchwrapperLocalPath = Path.Combine(MinecraftServerFolder, "libraries", "net", "minecraft", "launchwrapper", launchwrapperversion, launchwrapperJarName);
                var launchwrapperDownloadTask = DownloadFile(launchwrapperLocalPath, launchwrapperNetPath, returnIfExists: true);

                Task.WaitAll(new Task[] { downloadInstallerTask, downloadServerTask, launchwrapperDownloadTask });
            }

            HandleEula();
            Console.WriteLine("Installation Completed");
        }
        
        private void HandleEula()
        {
            var matches = Directory.EnumerateFiles(MinecraftServerFolder).Where((file) => UniversalJarRegex.Match(file).Success);
            if (matches.Count() == 0)
                throw new FileNotFoundException(String.Format("Cannot find ForgeUniversal file matching regex={0}", UniversalJarRegexString));
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = String.Format("-jar \"{0}\" -Xmx2G nogui", matches.First()),
                    WorkingDirectory = MinecraftServerFolder,
                }
            };

            process.Start();

            Task acceptEulaTask = AcceptEula();

            process.WaitForExit();
            acceptEulaTask.Wait();
        }

        private async Task AcceptEula()
        {
            var eulaTxtPath = Path.Combine(MinecraftServerFolder, "eula.txt");

            await Task.Factory.StartNew(() =>
            {
                while (!File.Exists(eulaTxtPath)) Thread.Sleep(100);
                string eulaContents = null;
                while (String.IsNullOrEmpty(eulaContents))
                {
                    try
                    {
                        while (String.IsNullOrEmpty((eulaContents = File.ReadAllText(eulaTxtPath)))) Thread.Sleep(100);
                    }
                    catch
                    {
                        Thread.Sleep(200);
                    }
                }
                File.WriteAllText(eulaTxtPath, eulaContents.Replace("false", "true"));
            });
        }

        private async Task DownloadFile(string localPath, string netPath, bool returnIfExists)
        {
            if (File.Exists(localPath) && returnIfExists) return;

            Directory.CreateDirectory(localPath);

            var message = Program.HttpClient.GetAsync(netPath);
            using var stream = File.OpenWrite(localPath);
            var body = await message.Result.Content.ReadAsStreamAsync();
            await body.CopyToAsync(stream);
        }
    }
}
