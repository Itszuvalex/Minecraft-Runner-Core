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
            using var cancellationToken = new CancellationTokenSource();
            var runner = new MinecraftRunner();
            Console.CancelKeyPress += new ConsoleCancelEventHandler(delegate (object sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine("Cancellation received");
                cancellationToken.Cancel();
                e.Cancel = true;
            });
            await runner.StartAsync(cancellationToken.Token);
            Console.WriteLine("Closing");
        }
    }
}
