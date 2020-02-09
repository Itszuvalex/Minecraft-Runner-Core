using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace MinecraftRunnerCore.Utility
{
    class WebSocketWrapper
    {
        public bool IsConnected => Socket != null && Socket.State == WebSocketState.Open;
        public delegate void OnConnectedEventHandler(WebSocketWrapper wrapper);
        public event OnConnectedEventHandler OnConnected;
        public delegate void KeepAliveEventHandler(WebSocketWrapper wrapper);
        public event KeepAliveEventHandler KeepAlive;
        public delegate void DataReceivedEventHandler(WebSocketWrapper wrapper, ArraySegment<byte> data);
        public event DataReceivedEventHandler DataReceived;
        public const int KeepAliveDefault = 30;
        private ClientWebSocket Socket;
        private CancellationTokenSource CancellationSource { get; set; }
        private Uri TargetUri { get; }
        private CancellableRunLoop ReceiveLoop { get; }
        private System.Timers.Timer KeepAliveTimer { get; set; }

        public WebSocketWrapper(Uri target)
        : this(target, TimeSpan.FromSeconds(KeepAliveDefault))
        { }

        public WebSocketWrapper(Uri target, TimeSpan KeepAlive)
        {
            TargetUri = target;
            ReceiveLoop = new CancellableRunLoop();
            ReceiveLoop.LoopIterationEvent += ReceiveLoop_LoopIterationEvent;
            KeepAliveTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30).TotalMilliseconds);
            KeepAliveTimer.Elapsed += Timer_KeepAlive;
            KeepAliveTimer.AutoReset = true;
            KeepAliveTimer.Enabled = true;
        }

        private void Timer_KeepAlive(object sender, ElapsedEventArgs e)
        {
            if(IsConnected)
            {
                KeepAlive?.Invoke(this);
            }
        }

        public async Task ConnectAsync()
        {
            if(Socket == null)
            {
                Socket = new ClientWebSocket();
                CancellationSource = new CancellationTokenSource();
            }

            Task connectTask; 
            try
            {
                connectTask = Socket.ConnectAsync(TargetUri, CancellationSource.Token);
                connectTask.Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Connection task threw exception on connect = {0}", e.ToString()));
                await CloseAsync("Exception on connect.");
                return;
            }

            if (!connectTask.IsCompletedSuccessfully)
            {
                Console.WriteLine("Connection task completed unsuccessfully.");
                await CloseAsync("Connection task unsuccessful.");
                return;
            }

            OnConnected?.Invoke(this);

            ReceiveLoop.Start();
        }

        private void ReceiveLoop_LoopIterationEvent(CancellationToken token)
        {
            WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure;
            if (!IsConnected)
            {
                CloseAsync("Receive on not connected websocket.", status).Wait();
                return;
            }

            try
            {
                ArraySegment<byte> buffer = new byte[4 * 1024];
                const int bufferMax = 1 << 16;
                while (!CancellationSource.IsCancellationRequested)
                {
                    var result = Socket.ReceiveAsync(buffer, CancellationSource.Token).Result;
                    if (result.CloseStatus.HasValue)
                    {
                        Console.WriteLine("Received close from hub.");
                        CloseAsync("Received close.", status).Wait();
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
                        DataReceived?.Invoke(this, buffer.Slice(0, result.Count));

                        // Reset buffer size
                        buffer = new byte[4 * 1024];
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Received exception during receive = {0}", e));
                CloseAsync("Received exception on receive.", status).Wait();
                return;
            }
        }

        public async Task SendAsync(byte[] data, WebSocketMessageType type = WebSocketMessageType.Text)
        {
            if (!IsConnected) return;
            try
            {
                await Socket.SendAsync(data, type, true, CancellationSource.Token);
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Exception while sending data: {0}", e.ToString()));
                await CloseAsync("Error on send");
            }
        }

        public async Task CloseAsync(string message, WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure)
        {
            if (Socket == null) return;

            ReceiveLoop.Stop();

            try
            {
                await Socket.CloseAsync(status, message, CancellationSource.Token);
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Exception while closing : {0}", e.ToString()));
            }
            finally
            {
                CancellationSource?.Cancel();
                Socket?.Dispose();
                Socket = null;
                CancellationSource?.Dispose();
                CancellationSource = null;
            }
        }
    }
}
