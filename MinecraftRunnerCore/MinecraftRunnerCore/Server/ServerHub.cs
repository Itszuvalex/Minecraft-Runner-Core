using MinecraftDiscordBotCore.Models.Messages;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftRunnerCore.Server
{
    class ServerHub
    {
        private ClientWebSocket Socket { get; set; }
        private Task SocketReceiveTask { get; set; }
        private CancellationTokenSource CancellationSource { get; set; }
        public delegate void HubMessageReceivedEventHandler(object sender, IMessage message);
        public event HubMessageReceivedEventHandler HubMessageReceived;

        public bool IsConnected => Socket != null && Socket.State == WebSocketState.Open;

        public async Task ConnectAsync(Uri uri)
        {
            if (Socket == null)
            {
                Socket = new ClientWebSocket();
                CancellationSource = new CancellationTokenSource();
            }

            await Socket.ConnectAsync(uri, CancellationSource.Token);

            SocketReceiveTask = Task.Factory.StartNew(async () =>
            {
                if (!IsConnected)
                {
                    await CloseAsync();
                    return;
                }

                ArraySegment<byte> buffer = new byte[1024];
                while (!CancellationSource.IsCancellationRequested)
                {
                    var result = await Socket.ReceiveAsync(buffer, CancellationSource.Token);
                    if (result.CloseStatus.HasValue)
                    {
                        await CloseAsync();
                        return;
                    }

                    if (!result.EndOfMessage)
                    {
                        if (buffer.Count >= (1 << 16))
                            throw new Exception(String.Format("Websocket buffer size has grown greater than {0}", (1 << 16)));
                        var temp = new byte[buffer.Count * 2];
                        buffer.CopyTo(temp);
                        buffer = temp;
                    }
                    else
                    {
                        object obj = JsonSerializer.Deserialize<IMessage>(Encoding.UTF8.GetString(buffer));

                        // Reset buffer size
                        buffer = new byte[1024];
                    }
                }
            },
            TaskCreationOptions.LongRunning);
        }

        public async Task CloseAsync()
        {
            if (Socket == null) return;

            await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Minecraft Server closing", CancellationSource.Token);
            CancellationSource.Cancel();
            Socket.Dispose();
            Socket = null;
            CancellationSource.Dispose();
            CancellationSource = null;
        }

        public async Task SendMessage(IMessage message)
        {
            if (!IsConnected) return;
            try
            {
                string json = JsonSerializer.Serialize(message);
                await Socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationSource.Token);
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Exception when serializing Json: {{0}}"), e.ToString());
            }
        }
    }
}
