using MinecraftRunnerCore.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftRunnerCore
{
    class MinecraftServer
    {
        private MinecraftRunner Runner { get; }
        private Process ServerProcess { get; set; }
        private MessageHandler MessageHandler { get; set; }
        private ServerData Data { get; set; }
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
        public MinecraftServer(MinecraftRunner runner)
        {
            Runner = runner;
        }

        public async Task StartAsync(string serverJar)
        {
            if (Running) return;

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

        public void Stop(bool forceful = false)
        {
            if (!Running) return;
            WriteInput("stop");
            bool exited = ServerProcess.WaitForExit(Convert.ToInt32(TimeSpan.FromSeconds(30).TotalMilliseconds));
            if (!exited && forceful)
                ServerProcess.Kill();
        }
    }
}
