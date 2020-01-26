using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MinecraftRunnerCore.Server
{
    class MessageHandler
    {
        private const string messageRegexString = "\\[.*\\] \\[.*INFO\\] \\[.*DedicatedServer\\]: <.*>";
        private static Regex messageRegex = new Regex(messageRegexString);
        private const string tpsRegexString = "\\[.*\\] \\[.*INFO\\] \\[.*DedicatedServer\\]: Dim";
        private static Regex tpsRegex = new Regex(tpsRegexString);
        private const string playerRegexString = "\\[.*\\] \\[.*INFO\\] \\[.*DedicatedServer\\]: There are";
        private static Regex playerRegex = new Regex(playerRegexString);
        private const string doneRegexString = "\\[.*\\] \\[.*INFO\\] \\[.*DedicatedServer\\]: Done";
        private static Regex doneRegex = new Regex(doneRegexString);
        
        private MinecraftServer Server { get; }

        public MessageHandler(MinecraftServer server)
        {
            Server = server;
        }

        public async Task HandleMessageAsync(MinecraftServer sender, string message)
        {
            return;
        }
    }
}
