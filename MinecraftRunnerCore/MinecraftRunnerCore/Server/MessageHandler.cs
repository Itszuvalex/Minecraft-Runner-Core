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

        public delegate void PlayerMessageEventHandler(object sender, string message);
        public event PlayerMessageEventHandler PlayerMessageEvent;
        public delegate void TpsMessageEventHandler(object sender, string message);
        public event TpsMessageEventHandler TpsMessageEvent;
        public delegate void PlayersEventHandler(object sender, string message);
        public event PlayersEventHandler PlayersEvent;
        public delegate void DoneMessageEventHandler(object sender, string message);
        public event DoneMessageEventHandler DoneMessageEvent;

        private MinecraftServer Server { get; }

        public MessageHandler(MinecraftServer server)
        {
            Server = server;
        }

        public async Task HandleMessageAsync(MinecraftServer sender, string message)
        {
            if (doneRegex.IsMatch(message))
            {
                DoneMessageEvent?.Invoke(this, message);
                return;
            }
            if (messageRegex.IsMatch(message))
            {
                PlayerMessageEvent?.Invoke(this, message);
                return;
            }
            if (tpsRegex.IsMatch(message))
            {
                TpsMessageEvent?.Invoke(this, message);
                return;
            }
            if (playerRegex.IsMatch(message))
            {
                PlayersEvent?.Invoke(this, message);
            }
        }
    }
}
