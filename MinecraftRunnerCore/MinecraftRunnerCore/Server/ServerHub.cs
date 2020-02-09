using MinecraftDiscordBotCore.Models.Messages;
using MinecraftRunnerCore.Utility;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

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
        public delegate void KeepAliveEventHandler(ServerHub sender);
        public event KeepAliveEventHandler KeepAlive;
        private CancellableRunLoop ConnectLoop { get; }

        public bool IsConnected => Socket != null && Socket.State == WebSocketState.Open;
        public Uri HubUri { get; set; }
        static readonly int ConnectionRetryTimeMS = Convert.ToInt32(TimeSpan.FromSeconds(30).TotalMilliseconds);
        private System.Timers.Timer KeepAliveTimer { get; set; }

        public ServerHub()
        {
            KeepAliveTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30).TotalMilliseconds);
            KeepAliveTimer.Elapsed += Timer_KeepAlive;
            KeepAliveTimer.AutoReset = true;
            KeepAliveTimer.Enabled = true;
            ConnectLoop = new CancellableRunLoop();
            ConnectLoop.LoopIterationEvent += ConnectLoop_LoopIterationEvent;
        }

        private void ConnectLoop_LoopIterationEvent(CancellationToken token)
        {
            try
            {
                if (!IsConnected)
                {
                    Console.WriteLine("Connection Loop: Starting to attempt to connect.");
                    ConnectAsync().Wait();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error connecting during Connection Loop = {0}", e.ToString());
            }
            token.ThrowIfCancellationRequested();
            Thread.Sleep(ConnectionRetryTimeMS);
        }

        private void Timer_KeepAlive(object timer, ElapsedEventArgs args)
        {
            if(IsConnected)
            {
                KeepAlive?.Invoke(this);
            }
        }

        public void BeginConnectionLoop()
        {
            if (ConnectLoop.Running) return;
            Console.WriteLine("Starting Discord connection loop.");
            ConnectLoop.Start();
        }

        public void EndConnectionLoop()
        {
            if (!ConnectLoop.Running) return;
            Console.WriteLine("Ending Discord connection loop.");
            ConnectLoop.Stop();
            Console.WriteLine("Discord connection loop ended.");
        }

        public async Task ConnectAsync()
        {
            Console.WriteLine("ConnectAsync");
            if (Socket == null)
            {
                Socket = new ClientWebSocket();
                CancellationSource = new CancellationTokenSource();
            }

            Task connectTask; 
            try
            {
                connectTask = Socket.ConnectAsync(HubUri, CancellationSource.Token);
                connectTask.Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Connection task threw exception on connect = {0}", e.ToString()));
                await CloseAsync();
                return;
            }

            if (!connectTask.IsCompletedSuccessfully)
            {
                Console.WriteLine("Connection task completed unsuccessfully.");
                await CloseAsync();
                return;
            }

            Console.WriteLine(String.Format("Connection task completed successfully.  Socket State = {0}", Socket.State.ToString()));

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
                            Console.WriteLine("Received close from hub.");
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
                catch (Exception e)
                {
                    Console.WriteLine(String.Format("Received exception during receive = {0}", e));
                    await CloseAsync(status);
                    return;
                }
            },
            TaskCreationOptions.LongRunning);
        }

        public async Task CloseAsync(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure)
        {
            Console.WriteLine("CloseAsync");
            if (Socket == null) return;

            try
            {
                Console.WriteLine("CloseAsync : Close socket");
                await Socket.CloseAsync(status, "Minecraft Server closing", CancellationSource.Token);
            }
            finally
            {
                Console.WriteLine("CloseAsync : Clear data");
                CancellationSource?.Cancel();
                Socket?.Dispose();
                Socket = null;
                CancellationSource?.Dispose();
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
