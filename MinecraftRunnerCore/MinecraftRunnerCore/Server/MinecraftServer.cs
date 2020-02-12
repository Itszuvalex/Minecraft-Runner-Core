using MinecraftDiscordBotCore.Models.Messages;
using MinecraftRunnerCore.Server;
using MinecraftRunnerCore.Utility;
using Newtonsoft.Json;
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
        #region Public
        public string MinecraftServerFolder { get; }
        public ServerStatus ServerStatus => Data.Status;
        public bool ServerRunning => ServerProcess != null && !ServerProcess.HasExited;
        public bool LoopRunning => ServerRunLoop.Running;
        public delegate void ServerOutputEventHandler(MinecraftServer server, string output);
        public event ServerOutputEventHandler ServerOutputEvent;
        public bool IsErrored => ConsecutiveErrors >= ConsecutiveErrorMax;
        public int ConsecutiveErrorMax = 10;
        public int ConsecutiveErrors { get; private set; }
        #endregion
        #region Private
        private const string InstallerJarRegexstring = "forge-(.*?)-(.*?)-installer\\.jar";
        private static readonly Regex InstallerJarRegex = new Regex(InstallerJarRegexstring);
        private const string UniversalJarRegexstring = "forge-(.*?)universal.jar";
        private static readonly Regex UniversalJarRegex = new Regex(UniversalJarRegexstring);
        private MinecraftRunner Runner { get; }
        private Process ServerProcess { get; set; }
        private MessageHandler MessageHandler { get; }
        private ServerData Data { get; }
        private ServerHub Hub { get; }
        private CancellableRunLoop ServerRunLoop { get; }
        private Settings Settings { get; }
        private System.Timers.Timer DataUpdateTimer { get; }
        #endregion

        #region Constructor
        public MinecraftServer(MinecraftRunner runner, ServerHub hub, string serverFolder, Settings settings)
        {
            Runner = runner;
            Hub = hub;
            MinecraftServerFolder = serverFolder;
            Settings = settings;
            ConsecutiveErrors = 0;
            MessageHandler = new MessageHandler(this);
            MessageHandler.DoneMessageEvent += MessageHandler_DoneMessageEvent;
            MessageHandler.PlayerMessageEvent += MessageHandler_PlayerMessageEvent;
            MessageHandler.PlayersEvent += MessageHandler_PlayersEvent;
            MessageHandler.TpsMessageEvent += MessageHandler_TpsMessageEvent;
            MessageHandler.PlayerJoinedEvent += MessageHandler_PlayerJoinedEvent;
            MessageHandler.PlayerLeftEvent += MessageHandler_PlayerLeftEvent;
            Hub.HubConnectionEstablished += Hub_HubConnectionEstablished;
            Hub.KeepAlive += Hub_KeepAlive;
            Hub.ChatMessageReceived += Hub_ChatMessageReceived;
            Hub.ServerCommandReceived += Hub_ServerCommandReceived;
            Data = new ServerData(Settings.Name);
            ServerRunLoop = new CancellableRunLoop();
            ServerRunLoop.LoopIterationEvent += ServerRunLoop_LoopIterationEvent; 
            DataUpdateTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30).TotalMilliseconds);
            DataUpdateTimer.Elapsed += DataUpdateTimer_Elapsed;
            DataUpdateTimer.AutoReset = true;
            DataUpdateTimer.Enabled = true;
        }

        private void Hub_ServerCommandReceived(ServerCommand message)
        {
            switch(message.Command)
            {
                case "stoploop":
                    StopRunLoopAsync().Wait();
                    break;
                case "startloop":
                    ConsecutiveErrors = 0;
                    StartRunLoop();
                    break;
                default:
                    WriteInput(message.Command);
                    break;
            }
        }

        private void Hub_ChatMessageReceived(ChatMessage message)
        {
            WriteInput(String.Format("say [Discord/{0}]:{1}", message.Timestamp, message.Message));
        }

        private void DataUpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (ServerStatus != ServerStatus.Stopped)
            {
                RefreshServerData();
            }

            if(ServerStatus == ServerStatus.Running)
            {
                WriteInput("forge tps");
                // WriteInput("list"); // Don't do this since player tracking works
            }
        }

        #endregion
        #region Hub Events
        private void Hub_KeepAlive(ServerHub sender)
        {
            SendServerDataUpdate();
        }

        private void Hub_HubConnectionEstablished(ServerHub sender)
        {
            SendServerDataUpdate();
        }
        #endregion

        #region MessageHandler Events
        private void MessageHandler_PlayerLeftEvent(object sender, string player)
        {
            Data.Players.Remove(player);
        }

        private void MessageHandler_PlayerJoinedEvent(object sender, string player)
        {
            Data.Players.Add(player);
        }

        private void MessageHandler_TpsMessageEvent(object sender, string dim, string tps)
        {
            Data.Tps.AddTps(dim, tps);
        }

        private void MessageHandler_PlayersEvent(object sender, int players)
        {
            // Data.PlayerCount = players;
        }

        private void MessageHandler_PlayerMessageEvent(object sender, string message)
        {
            Console.WriteLine(string.Format("Received Player message = {0}", message));
            var text = message.Substring(message.IndexOf('<'));
            Hub.SendMessage(new ChatMessage { Message = text, Timestamp = JsonConvert.SerializeObject(DateTime.Now) }).Wait();
        }

        private void MessageHandler_DoneMessageEvent(object sender, string message)
        {
            Console.WriteLine("Minecraft server started successfully.");
            SetStatus(ServerStatus.Running);
        }
        #endregion

        #region Run Loop
        private void ServerRunLoop_LoopIterationEvent(CancellationToken token)
        {
            try
            {
                StartServer().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Encountered exception while Starting Server as part of Run Loop = {0}", e));
            }

            if (ServerProcess != null)
            {
                ServerProcess.WaitForExit();
                HandleServerExit(ServerProcess.ExitCode);
            }
            else
            {
                ++ConsecutiveErrors;
            }

            if (ConsecutiveErrors > 0 && !IsErrored)
            {
                Console.WriteLine(string.Format("Minecraft Server has hit an error - Consecutive Errors = {0}, ErrorMax = {1}", ConsecutiveErrors, ConsecutiveErrorMax));
            }

            if(IsErrored)
            {
                Console.WriteLine(string.Format("Minecraft Server is now in Errored State - Consecutive Errors = {0}, ErrorMax = {1}", ConsecutiveErrors, ConsecutiveErrorMax));
                SetStatus(ServerStatus.Error);
                ServerRunLoop.Stop();
            }

            Console.WriteLine("Sleeping for 10s");
            TimeSpan sleep = TimeSpan.FromSeconds(10);
            const int iterations = 100;
            for(int i = 0; i < iterations && !token.IsCancellationRequested; ++i)
            {
                Thread.Sleep(sleep.Divide(iterations));
                token.ThrowIfCancellationRequested();
            }
        }

        public void StartRunLoop()
        {
            if (ServerRunLoop.Running) return;

            Console.WriteLine("Starting Minecraft Server Run Loop");
            ServerRunLoop.Start();
        }

        public void HandleServerExit(int code)
        {
            Console.WriteLine(string.Format("Server process completed with code = {0}", code));

            if(code == 0)
            {
                ConsecutiveErrors = 0;
            }
            else
            {
                ++ConsecutiveErrors;
            }

            SetStatus(ServerStatus.Stopped);

            ServerProcess.Dispose();
            ServerProcess = null;
        }

        public async Task StopRunLoopAsync()
        {
            if (!LoopRunning) return;
            Console.WriteLine("Stopping Minecraft Server Run Loop");
            Stop(true);
            ServerRunLoop.Stop();
            Console.WriteLine("Minecraft Server Run Loop Stopped");
        }

        private async Task StartServer()
        {
            if (ServerRunning) return;

            try
            {
                Install(Settings.McVer, Settings.ForgeVer, Settings.LaunchWrapperVer, force: false);
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Encountered exception when attempting to install = {0}", e));
            }

            string serverJar = GetForgeUniversalJarName();
            Console.WriteLine(String.Format("Starting McServer with jar = {0}", serverJar));

            Task<ProcessStartInfo> startInfo = GetServerStartInfo(serverJar);
            ServerProcess = new Process();
            ServerProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
            ServerProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;

            ServerProcess.StartInfo = await startInfo;
            RefreshServerData();
            SetStatus(ServerStatus.Starting);

            ServerProcess.Start();
            ServerProcess.BeginOutputReadLine();
            ServerProcess.BeginErrorReadLine();
        }
        #endregion

        public void SetStatus(ServerStatus status)
        {
            // Based on what status we're switching to
            switch (status)
            {
                case ServerStatus.Running:
                    break; // Do nothing
                default:
                    // Data.PlayerCount = 0;
                    Data.Players.Clear();
                    Data.Tps.Clear();
                    break;
            }
            RefreshServerData();
            Data.Status = status;
            SendServerDataUpdate();
        }

        private async Task<ProcessStartInfo> GetServerStartInfo(string serverJar)
        {
            string[] arguments = new string[]
            {
                string.Format("-jar \"{0}\"", serverJar),
                "-Xms512M",
                string.Format("-Xmx{0}G", Settings.Ram)
            };

            arguments = arguments.Concat(Settings.ServerArgs).ToArray();
            string args = string.Join(' ', arguments);
            Console.WriteLine(String.Format("Starting server with command: java {0}", args));
            return new ProcessStartInfo
            {
                FileName = "java",
                Arguments = args,
                WorkingDirectory = Runner.MinecraftServerFolder,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
        }

        private void RefreshServerData()
        {
            long memory = 0;
            long memoryMax = 0;
            long storage = 0;
            long storageMax = 0;
            try
            {
                ServerProcess?.Refresh();
                memory = ServerProcess?.PrivateMemorySize64 ?? 0;
                memoryMax = ServerProcess?.VirtualMemorySize64 ?? 0;
                DriveInfo driveInfo = new DriveInfo(Directory.GetDirectoryRoot(Path.GetDirectoryName(Runner.MinecraftServerFolder)));
                storage = driveInfo.TotalSize - driveInfo.TotalFreeSpace;
                storageMax = driveInfo.TotalSize;
            }
            catch (Exception e)
            {
                Console.Write(string.Format("Hit Exception during RefreshServerData = {0}", e));
            }

            Data.Memory = memory;
            Data.MemoryMax = memoryMax;
            Data.Storage = storage;
            Data.StorageMax = storageMax;
        }

        private void SendServerDataUpdate()
        {
            Console.WriteLine("Sending Server Data Update");
            Hub.SendMessage(Data.ToMessage()).Wait();
        }

        private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                _ = MessageHandler?.HandleMessageAsync(this, e.Data);
                ServerOutputEvent?.Invoke(this, e.Data);
            }
            catch
            { }
            Console.WriteLine(string.Format("[Server]: {0}", e.Data));
        }

        private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(string.Format("[Server/Error]: {0}", e.Data));
        }

        public void WriteInput(string message, bool flush = true)
        {
            ServerProcess.StandardInput.WriteLine(message);
            if (flush)
                ServerProcess.StandardInput.Flush();
        }

        public bool Stop(bool forceful = false)
        {
            if (!ServerRunning) return true;

            if (ServerStatus == ServerStatus.Starting)
            {
                ServerProcess.Kill();
            }
            else
            {
                WriteInput("stop");
            }
            bool exited = ServerProcess.WaitForExit(Convert.ToInt32(TimeSpan.FromSeconds(30).TotalMilliseconds));

            if (exited)
            {
                SetStatus(ServerStatus.Stopped);
            }
            else if (forceful)
            {
                ServerProcess.Kill();
                SetStatus(ServerStatus.Stopped);
                return true;
            }

            return exited;
        }

        #region Install
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
            
            if (ServerRunning) 
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
                var downloadInstallerTask = Utilities.DownloadFile(installerJarFile, installerNetPath, returnIfExists: true);

                string serverJarName = string.Format("minecraft_server.{0}.jar", mcversion);
                string serverNetPath = string.Format("https://s3.amazonaws.com/Minecraft.Download/versions/{0}/{1}", mcversion, serverJarName);
                var downloadServerTask = Utilities.DownloadFile(serverJarName, serverNetPath, returnIfExists: true);

                string launchwrapperJarName = string.Format("launchwrapper-{0}.jar", launchwrapperversion);
                string launchwrapperNetPath = string.Format("https://libraries.minecraft.net/net/minecraft/launchwrapper/{0}/{1}", launchwrapperversion, launchwrapperJarName);
                string launchwrapperLocalPath = Path.Combine(MinecraftServerFolder, "libraries", "net", "minecraft", "launchwrapper", launchwrapperversion, launchwrapperJarName);
                var launchwrapperDownloadTask = Utilities.DownloadFile(launchwrapperLocalPath, launchwrapperNetPath, returnIfExists: true);

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
        #endregion
    }
}
