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
        private static readonly Regex messageRegex = new Regex(messageRegexString);
        private const string tpsRegexString = "\\[.*\\] \\[.*INFO\\] \\[.*DedicatedServer\\]: Dim";
        private static readonly Regex tpsRegex = new Regex(tpsRegexString);
        private const string playerRegexString = "\\[.*\\] \\[.*INFO\\] \\[.*DedicatedServer\\]: There are";
        private static readonly Regex playerRegex = new Regex(playerRegexString);
        private const string doneRegexString = "\\[.*\\] \\[.*INFO\\] \\[.*DedicatedServer\\]: Done";
        private static readonly Regex doneRegex = new Regex(doneRegexString);
        private const string playerLeftRegexString = "\\[.*\\] \\[.*INFO\\] \\[.*DedicatedServer\\]: (?<player>[^<>]+) left the game";
        private static readonly Regex PlayerLeftRegex = new Regex(playerLeftRegexString);
        private const string playerJoinedRegexString = "\\[.*\\] \\[.*INFO\\] \\[.*DedicatedServer\\]: (?<player>[^<>]+) joined the game";
        private static readonly Regex PlayerJoinedRegex = new Regex(playerJoinedRegexString);
        private static readonly Regex TpsRegex = new Regex("Dim\\s*(?<dim>[+-]?\\d*)\\s+(?<dimname>\\(.*\\))?\\s+:\\s+Mean tick time: .* Mean TPS: (?<tps>[\\d.]*)");
        private static readonly Regex PlayerCountRegex = new Regex("There are (?<players>\\d*)\\/(?<playermax>\\d*)");

        public delegate void PlayerMessageEventHandler(object sender, string message);
        public event PlayerMessageEventHandler PlayerMessageEvent;
        public delegate void TpsMessageEventHandler(object sender, string dim, string tps);
        public event TpsMessageEventHandler TpsMessageEvent;
        public delegate void PlayersEventHandler(object sender, int players);
        public event PlayersEventHandler PlayersEvent;
        public delegate void DoneMessageEventHandler(object sender, string message);
        public event DoneMessageEventHandler DoneMessageEvent;
        public delegate void PlayerStateChangeHandler(object sender, string player);
        public event PlayerStateChangeHandler PlayerJoinedEvent;
        public event PlayerStateChangeHandler PlayerLeftEvent;

        private MinecraftServer Server { get; }

        public MessageHandler(MinecraftServer server)
        {
            Server = server;
        }

        public async Task HandleMessageAsync(MinecraftServer sender, string message)
        {
            if (doneRegex.IsMatch(message))
            {
                HandleDoneMessageInternal(message);
                return;
            }
            if (messageRegex.IsMatch(message))
            {
                HandlePlayerMessageInternal(message);
                return;
            }
            if (tpsRegex.IsMatch(message))
            {
                HandleTpsMessageInternal(message);
                return;
            }
            if (playerRegex.IsMatch(message))
            {
                HandlePlayerInternal(message);
                return;
            }
            if(PlayerJoinedRegex.IsMatch(message))
            {
                HandlePlayerJoinedInternal(message);
                return;
            }
            if(PlayerLeftRegex.IsMatch(message))
            {
                HandlePlayerLeftInternal(message);
                return;
            }
        }

        private async Task HandleDoneMessageInternal(string message) => DoneMessageEvent?.Invoke(this, message);
        private async Task HandlePlayerMessageInternal(string message) => PlayerMessageEvent?.Invoke(this, message);
        private async Task HandleTpsMessageInternal(string message)
        {
            Match match = TpsRegex.Match(message);
            if(!match.Success)
            {
                Console.WriteLine("Error matching TpsRegex.");
                return;
            }

            string dim = match.Groups.GetValueOrDefault("dim").Value;
            string tps = match.Groups.GetValueOrDefault("tps").Value;
            TpsMessageEvent?.Invoke(this, dim, tps);
        }

        private async Task HandlePlayerInternal(string message)
        {
            Console.WriteLine(string.Format("Received Players message = {0}", message));
            Match match = PlayerCountRegex.Match(message);
            if(!match.Success)
            {
                Console.WriteLine("Error matching PlayerCountRegex.");
                return;
            }

            string players = match.Groups.GetValueOrDefault("players").Value;
            var playerCount = int.Parse(players);
            PlayersEvent?.Invoke(this, playerCount);
        }

        private async Task HandlePlayerJoinedInternal(string message)
        {
            Console.WriteLine(string.Format("Received Player joined message = {0}", message));
            Match match = PlayerJoinedRegex.Match(message);
            if(!match.Success)
            {
                Console.WriteLine("Error matching PlayerJoinedRegex.");
                return;
            }

            string player = match.Groups.GetValueOrDefault("player").Value;
            PlayerJoinedEvent?.Invoke(this, player);
        }

        private async Task HandlePlayerLeftInternal(string message)
        {
            Console.WriteLine(string.Format("Received Player left message = {0}", message));
            Match match = PlayerLeftRegex.Match(message);
            if(!match.Success)
            {
                Console.WriteLine("Error matching PlayerLeftRegex.");
                return;
            }

            string player = match.Groups.GetValueOrDefault("player").Value;
            PlayerLeftEvent?.Invoke(this, player);
        }
    }
}
