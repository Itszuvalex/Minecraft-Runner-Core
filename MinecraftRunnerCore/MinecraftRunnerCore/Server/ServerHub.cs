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
        public delegate void HubConnectionEstablishedEventHandler(ServerHub sender);
        public event HubConnectionEstablishedEventHandler HubConnectionEstablished;
        private Task ConnectLoopTask { get; set; }
        public bool ConnectLoopActive => ConnectLoopTask != null;
        private CancellationTokenSource ConnectLoopCancellationSource { get; set; }

        public bool IsConnected => Socket != null && Socket.State == WebSocketState.Open;
        public Uri HubUri { get; set; }
        static readonly int ConnectionRetryTimeMS = Convert.ToInt32(TimeSpan.FromSeconds(30).TotalMilliseconds);

        public void BeginConnectionLoop()
        {
            if (ConnectLoopActive) return;
            ConnectLoopCancellationSource = new CancellationTokenSource();
            ConnectLoopTask = Task.Factory.StartNew(async () =>
            {
                while(!ConnectLoopCancellationSource.IsCancellationRequested)
                {
                    try
                    {
                        if (!IsConnected)
                        {
                            await ConnectAsync();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error connecting during Connection Loop = {0}", e.ToString());
                    }
                    ConnectLoopCancellationSource.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(ConnectionRetryTimeMS);
                }
            }, ConnectLoopCancellationSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void EndConnectionLoop()
        {
            if (!ConnectLoopActive) return;
            ConnectLoopCancellationSource.Cancel();
            ConnectLoopTask.Wait();
            ConnectLoopTask.Dispose();
            ConnectLoopTask = null;
            ConnectLoopCancellationSource.Dispose();
            ConnectLoopCancellationSource = null;
        }


        public async Task ConnectAsync()
        {
            if (Socket == null)
            {
                Socket = new ClientWebSocket();
                CancellationSource = new CancellationTokenSource();
            }

            var connectTask = Socket.ConnectAsync(HubUri, CancellationSource.Token);
            connectTask.Wait();
            if (!connectTask.IsCompletedSuccessfully)
            {
                await CloseAsync();
                return;
            }

            HubConnectionEstablished?.Invoke(this);

            await SendMessage(new ServerData("test").ToMessage());

            SocketReceiveTask = Task.Factory.StartNew(async () =>
            {
                WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure;
                if (!IsConnected)
                {
                    await CloseAsync(status);
                    return;
                }

                try
                {
                    ArraySegment<byte> buffer = new byte[4 * 1024];
                    const int bufferMax = 1 << 16;
                    while (!CancellationSource.IsCancellationRequested)
                    {
                        var result = await Socket.ReceiveAsync(buffer, CancellationSource.Token);
                        if (result.CloseStatus.HasValue)
                        {
                            await CloseAsync(status);
                            return;
                        }

                        if (!result.EndOfMessage)
                        {
                            if (buffer.Count >= bufferMax)
                            {
                                status = WebSocketCloseStatus.MessageTooBig;
                                throw new Exception(String.Format("Websocket buffer size has grown greater than {0}", bufferMax));
                            }
                            var temp = new byte[buffer.Count * 2];
                            buffer.CopyTo(temp);
                            buffer = temp;
                        }
                        else
                        {
                            try
                            {
                                IMessage message = JsonSerializer.Deserialize<IMessage>(buffer.Slice(0, result.Count), new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true,
                                });
                                if (message != null)
                                    HubMessageReceived?.Invoke(this, message);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(String.Format("Error deserializing Json message = {0}", e.ToString()));
                            }

                            // Reset buffer size
                            buffer = new byte[4 * 1024];
                        }
                    }
                }
                catch
                {
                    await CloseAsync(status);
                    return;
                }
            },
            TaskCreationOptions.LongRunning);
        }

        public async Task CloseAsync(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure)
        {
            if (Socket == null) return;

            try
            {
                await Socket.CloseAsync(status, "Minecraft Server closing", CancellationSource.Token);
            }
            finally
            {
                CancellationSource.Cancel();
                Socket.Dispose();
                Socket = null;
                CancellationSource.Dispose();
                CancellationSource = null;
            }
        }

        public async Task SendMessage<T>(T message) where T:IMessage
        {
            if (!IsConnected) return;
            byte[] json;
            try
            {
                json = JsonSerializer.SerializeToUtf8Bytes(message);
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Exception when serializing Json: {0}", e.ToString()));
                return;
            }

            try
            {
                await Socket.SendAsync(json, WebSocketMessageType.Text, true, CancellationSource.Token);
            }
            catch(Exception e)
            {
                Console.WriteLine(String.Format("Exception while sending json message: {0}", e.ToString()));
                await CloseAsync();
            }
        }
    }
}
