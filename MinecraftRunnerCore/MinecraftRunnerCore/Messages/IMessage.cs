using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MinecraftDiscordBotCore.Models.Messages
{
    public interface IMessage
    {
        public string Type { get; }

        public static bool TryParseMessage<T>(ArraySegment<byte> data, out T outMessage) where T : IMessage
        {
            try
            {
                outMessage = JsonSerializer.Deserialize<T>(data, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Error parsing message = {0}", e));
                outMessage = default;
                return false;
            }
        }
    }
}
