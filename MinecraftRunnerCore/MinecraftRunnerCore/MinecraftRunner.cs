using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftRunnerCore
{
    class MinecraftRunner
    {
        public async Task StartAsync(CancellationToken token)
        {
            InstallIfNecessary();

            await  Task.Factory.StartNew(delegate
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

        private void InstallIfNecessary()
        {

        }
    }
}
