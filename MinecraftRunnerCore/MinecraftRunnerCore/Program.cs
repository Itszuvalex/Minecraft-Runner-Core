using System;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftRunnerCore
{
    class Program
    {
        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var cancellationToken = new CancellationTokenSource();
            var runner = new MinecraftRunner();
            Console.CancelKeyPress += new ConsoleCancelEventHandler(delegate (object sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine("Cancellation received");
                runner.StopLoop();
                e.Cancel = true;
            });

            runner.StartLoop();

            while (runner.ActiveLoop)
            { Thread.Sleep(1000); }

            Console.WriteLine("Ended loop");
        }
    }
}
