using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MinecraftDiscordBotCore.Models.Messages
{
    public class ChatMessage : IMessage
    {
        public const string TypeString = "msg";
        public string Type => TypeString;
        public string Timestamp { get; set; }
        public string Message { get; set; }
    }
}
