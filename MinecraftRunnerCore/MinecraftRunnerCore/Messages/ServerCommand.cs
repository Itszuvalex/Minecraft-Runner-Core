using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MinecraftDiscordBotCore.Models.Messages
{
    public class ServerCommand : IMessage
    {
        public const string TypeString = "cmd";
        public string Type => TypeString;
        public string Command { get; set; }
    }
}
