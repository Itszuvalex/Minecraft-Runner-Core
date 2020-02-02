using MinecraftRunnerCore.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftRunnerCore
{
    class MinecraftServer
    {
        private const string InstallerJarRegexstring = "forge-(.*?)-(.*?)-installer\\.jar";
        private static Regex InstallerJarRegex = new Regex(InstallerJarRegexstring);
        private const string UniversalJarRegexstring = "forge-(.*?)universal.jar";
        private static Regex UniversalJarRegex = new Regex(UniversalJarRegexstring);
        private MinecraftRunner Runner { get; }
        private Process ServerProcess { get; set; }
        private MessageHandler MessageHandler { get; set; }
        private ServerData Data { get; set; }
        private ServerHub Hub { get; set; }
        public string MinecraftServerFolder { get; }
        public ServerStatus ServerStatus
        {
            get
            {
                if (ServerStatus.TryParse<ServerStatus>(Data?.Status, out ServerStatus status))
                    return status;
                else return ServerStatus.Stopped;
            }
        }
        public bool Running => ServerProcess != null && !ServerProcess.HasExited;
        public delegate void ServerOutputEventHandler(MinecraftServer server, string output);
        public event ServerOutputEventHandler ServerOutputEvent;
        public bool IsErrored => ConsecutiveErrors < ConsecutiveErrorMax;
        public int ConsecutiveErrorMax = 10;
        public int ConsecutiveErrors { get; private set; } 
        public MinecraftServer(MinecraftRunner runner, ServerHub hub, string serverFolder)
        {
            Runner = runner;
            Hub = hub;
            MinecraftServerFolder = serverFolder;
        }

        public async Task StartAsync()
        {
            if (Running) return;


            string serverJar = GetForgeUniversalJarName();
            Console.WriteLine(String.Format("Starting McServer with jar = {0}", serverJar));

            Task<ProcessStartInfo> startInfo = GetServerStartInfo(serverJar);
            if (ServerProcess == null)
            {
                ServerProcess = new Process();
                MessageHandler = new MessageHandler(this);
                Data = new ServerData(name: "test");
                ServerProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
            }

            ServerProcess.StartInfo = await startInfo;
            RefreshServerData();
            SetStatus(ServerStatus.Starting);

            ServerProcess.Start();
        }

        public void SetStatus(ServerStatus status)
        {
            if (Data == null)
                return;

            // Based on what status we're switching to
            switch (status)
            {
                case ServerStatus.Running:
                    break; // Do nothing
                default:
                    Data.PlayerCount = 0;
                    Data.Players.Clear();
                    Data.Tps.Clear();
                    break;
            }
            Data.Status = status.ToString();
            SendServerDataUpdate();
        }

        private async Task<ProcessStartInfo> GetServerStartInfo(string serverJar)
        {
            string[] arguments = new String[]
            {
                String.Format("-jar \"{0}\"", serverJar),
                "-Xms512M",
                String.Format("-Xmx{0}m", "6"),
                "-XX:+UseG1GC",
                "-XX:MaxGCPauseMillis=50",
                "-XX:UseSSE=4",
                "-XX:+UseNUMA",
                "nogui"
            };
            return new ProcessStartInfo
            {
                FileName = "java",
                Arguments = string.Join(' ', arguments),
                WorkingDirectory = Runner.MinecraftServerFolder,
            };
        }

        private void RefreshServerData()
        {
            long memory = ServerProcess?.PrivateMemorySize64 ?? 0;
            long memoryMax = ServerProcess?.VirtualMemorySize64 ?? 0;
            DriveInfo driveInfo = new DriveInfo(Directory.GetDirectoryRoot(Path.GetDirectoryName(Runner.MinecraftServerFolder)));
            long storage = driveInfo.TotalSize - driveInfo.TotalFreeSpace;
            long storageMax = driveInfo.TotalSize;

            Data.Memory = memory;
            Data.MemoryMax = memoryMax;
            Data.Storage = storage;
            Data.StorageMax = storageMax;
        }

        private void SendServerDataUpdate()
        {
            Hub.SendMessage(Data.ToMessage()).Wait();
        }

        private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            _ = MessageHandler?.HandleMessageAsync(this, e.Data);
            ServerOutputEvent?.Invoke(this, e.Data);
        }

        public void WriteInput(string message, bool flush = true)
        {
            ServerProcess.StandardInput.Write(message);
            if (flush)
                ServerProcess.StandardInput.Flush();
        }

        public bool Stop(bool forceful = false)
        {
            if (!Running) return true;
            WriteInput("stop");
            bool exited = ServerProcess.WaitForExit(Convert.ToInt32(TimeSpan.FromSeconds(30).TotalMilliseconds));
            if (!exited && forceful)
            {
                ServerProcess.Kill();
                return true;
            }
            return exited;
        }

        public void Install(string mcversion, string forgeversion, string launchwrapperversion, bool force)
        {
            if (!Directory.Exists(MinecraftServerFolder))
                throw new DirectoryNotFoundException(MinecraftServerFolder);

            if (File.Exists(Path.Combine(MinecraftServerFolder, "eula.txt")) && !force)
            {
                AcceptEula().Wait();
                Console.WriteLine("Already Installed");
                return;
            }
            
            if (Running) 
            {
                if (!force)
                {
                    Console.WriteLine("Already running.  How did we get here if we're not installed?");
                    return;
                }
                if (!Stop())
                    Stop(true);
            }

            if (TryFindInstallerJarName(out string installerJarName))
            {
                Process installer = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = string.Format("-jar \"{0}\" --installServer", installerJarName),
                        WorkingDirectory = MinecraftServerFolder
                    }
                };
                installer.Start();
                installer.WaitForExit();
            }
            else
            {
                installerJarName = "forge-universal.jar";
                string installerJarFile = Path.Combine(MinecraftServerFolder, installerJarName);
                string universalJarName = string.Format("forge-{0}-{1}-universal.jar", mcversion, forgeversion);
                string installerNetPath = string.Format("https://files.minecraftforge.net/maven/net/minecraftforge/forge/{0}-{1}/{2}", mcversion, forgeversion, universalJarName);
                var downloadInstallerTask = DownloadFile(installerJarFile, installerNetPath, returnIfExists: true);

                string serverJarName = string.Format("minecraft_server.{0}.jar", mcversion);
                string serverNetPath = string.Format("https://s3.amazonaws.com/Minecraft.Download/versions/{0}/{1}", mcversion, serverJarName);
                var downloadServerTask = DownloadFile(serverJarName, serverNetPath, returnIfExists: true);

                string launchwrapperJarName = string.Format("launchwrapper-{0}.jar", launchwrapperversion);
                string launchwrapperNetPath = string.Format("https://libraries.minecraft.net/net/minecraft/launchwrapper/{0}/{1}", launchwrapperversion, launchwrapperJarName);
                string launchwrapperLocalPath = Path.Combine(MinecraftServerFolder, "libraries", "net", "minecraft", "launchwrapper", launchwrapperversion, launchwrapperJarName);
                var launchwrapperDownloadTask = DownloadFile(launchwrapperLocalPath, launchwrapperNetPath, returnIfExists: true);

                Task.WaitAll(new Task[] { downloadInstallerTask, downloadServerTask, launchwrapperDownloadTask });
            }

            HandleEula();
            Console.WriteLine("Installation Completed");
        }

        private bool TryFindInstallerJarName(out string jarName)
        {
            jarName = null;
            var candidates = Directory.EnumerateFiles(MinecraftServerFolder).Where((file) => InstallerJarRegex.Match(file).Success);
            if(candidates.Count() > 0)
            {
                jarName = candidates.First();
                return true;
            }
            return false;
        }

        private string GetForgeUniversalJarName()
        {
            var matches = Directory.EnumerateFiles(MinecraftServerFolder).Where((file) => UniversalJarRegex.Match(file).Success);
            if (matches.Count() == 0)
                throw new FileNotFoundException(string.Format("Cannot find ForgeUniversal file matching regex={0}", UniversalJarRegexstring));
            return matches.First();
        }

        private void HandleEula()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = string.Format("-jar \"{0}\" -Xmx2G nogui", GetForgeUniversalJarName()),
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
                while (string.IsNullOrEmpty(eulaContents))
                {
                    try
                    {
                        while (string.IsNullOrEmpty((eulaContents = File.ReadAllText(eulaTxtPath)))) Thread.Sleep(100);
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
