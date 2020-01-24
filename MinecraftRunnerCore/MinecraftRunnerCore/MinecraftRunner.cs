using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftRunnerCore
{
    class MinecraftRunner
    {
        private Task LoopTask { get; set; }
        private CancellationTokenSource LoopToken { get; set; }

        public bool ActiveLoop => LoopTask != null;

        public void StartLoop()
        {
            CloseLoop();
            InstallIfNecessary();

            LoopToken = new CancellationTokenSource();
            LoopTask = Task.Factory.StartNew(delegate
            {
                while (!LoopToken.Token.IsCancellationRequested)
                {
                    MainLoopIteration();      
                }
                Console.WriteLine("Task Cancelled");
            }, LoopToken.Token, TaskCreationOptions.LongRunning);
        }

        public void StopLoop()
        {
            CloseLoop();
        }

        private void CloseLoop()
        {
            if (LoopTask == null) return;

            LoopToken?.Cancel();
            LoopToken?.Dispose();
            LoopToken = null;
            LoopTask = null;
        }

        private void MainLoopIteration()
        {
        }

        private void InstallIfNecessary()
        {

        }
    }
}
