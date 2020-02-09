using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftRunnerCore.Utility
{
    class CancellableRunLoop
    {
        public bool Running => RunTask != null;
        private Task RunTask { get; set; }
        private CancellationTokenSource CancellationSource { get; set; }
        public delegate void LoopIteration(CancellationToken token);
        public event LoopIteration LoopIterationEvent;

        public CancellableRunLoop()
        {
        }

        public void Start()
        {
            if (Running) return;

            CancellationSource = new CancellationTokenSource();
            RunTask = Task.Factory.StartNew(() =>
            {
                while(!CancellationSource.IsCancellationRequested)
                {
                    try
                    {
                        LoopIterationEvent?.Invoke(CancellationSource.Token);
                    }
                    catch (OperationCanceledException e)
                    {
                        if (e.CancellationToken != CancellationSource.Token) throw;
                        Console.WriteLine("Received OperationCancelledException from within the run loop.");
                    }
                }
            }, CancellationSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Stop()
        {
            if (!Running) return;

            CancellationSource.Cancel();
            RunTask.Wait();
            CancellationSource.Dispose();
            CancellationSource = null;
            RunTask.Dispose();
            RunTask = null;
        }
    }
}
