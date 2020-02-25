using MinecraftRunnerCore.Messages;
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
        private WebSocketWrapper Socket { get; set; }
        private CancellationTokenSource CancellationSource { get; set; }
        public delegate void HubConnectionEstablishedEventHandler(ServerHub sender);
        public event HubConnectionEstablishedEventHandler HubConnectionEstablished;
        public delegate void KeepAliveEventHandler(ServerHub sender);
        public event KeepAliveEventHandler KeepAlive;
        public delegate void ServerIdEventHandler(ServerId message);
        public event ServerIdEventHandler ServerIdReceived;
        public delegate void ChatMessageEventHandler(ChatMessage message);
        public event ChatMessageEventHandler ChatMessageReceived;
        public delegate void ServerCommandEventHandler(ServerCommand message);
        public event ServerCommandEventHandler ServerCommandReceived;
        private CancellableRunLoop ConnectLoop { get; }
        private Cache Cache { get; }
        static readonly TimeSpan ConnectionRetryTime = TimeSpan.FromSeconds(30);

        public Uri HubUri { get; }

        public ServerHub(Uri uri, Cache cache)
        {
            HubUri = uri;
            Cache = cache;
            ConnectLoop = new CancellableRunLoop();
            ConnectLoop.LoopIterationEvent += ConnectLoop_LoopIterationEvent;
            Socket = new WebSocketWrapper(HubUri);
            Socket.KeepAlive += Socket_KeepAlive;
            Socket.OnConnected += Socket_OnConnected;
            Socket.DataReceived += Socket_DataReceived;
        }

        private void Socket_DataReceived(WebSocketWrapper wrapper, ArraySegment<byte> data)
        {
            try
            {
                MessageHeader message = JsonSerializer.Deserialize<MessageHeader>(data, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                if (message == null)
                    return;

                switch (message.Type)
                {
                    case ServerId.TypeString:
                        if(IMessage.TryParseMessage<ServerId>(data, out ServerId id))
                        {
                            ServerIdReceived?.Invoke(id);
                        }
                        else
                        {
                            Console.WriteLine(String.Format("Unable to parse ServerId out of ServerId header."));
                        }
                        break;
                    case ChatMessage.TypeString:
                        if(IMessage.TryParseMessage<ChatMessage>(data, out ChatMessage chatMessage))
                        {
                            ChatMessageReceived?.Invoke(chatMessage);
                        }
                        else
                        {
                            Console.WriteLine(String.Format("Unable to parse ChatMessage out of ChatMessage header."));
                        }
                        break;
                    case ServerCommand.TypeString:
                        if(IMessage.TryParseMessage<ServerCommand>(data, out ServerCommand serverCommand))
                        {
                            ServerCommandReceived?.Invoke(serverCommand);
                        }
                        else
                        {
                            Console.WriteLine(String.Format("Unable to parse ServerCommand out of ServerCommand header."));
                        }
                        break;
                    default:
                        Console.WriteLine(String.Format("Unhandled Hub Message of type = {0}", message.Type));
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Error deserializing Json message = {0}", e.ToString()));
            }
        }

        private void Socket_OnConnected(WebSocketWrapper wrapper)
        {
            HubConnectionEstablished?.Invoke(this);
        }

        private void Socket_KeepAlive(WebSocketWrapper wrapper)
        {
            KeepAlive?.Invoke(this);
        }

        private void ConnectLoop_LoopIterationEvent(CancellationToken token)
        {
            try
            {
                if (!Socket.IsConnected)
                {
                    Console.WriteLine("Connection Loop: Starting to attempt to connect.");
                    Socket.ConnectAsync().Wait();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error connecting during Connection Loop = {0}", e.ToString());
            }
            token.ThrowIfCancellationRequested();
            const int iterations = 100;
            for(int i = 0; i < iterations && !token.IsCancellationRequested; ++i)
            {
                Thread.Sleep(ConnectionRetryTime.Divide(iterations));
                token.ThrowIfCancellationRequested();
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

        public async Task SendMessage<T>(T message) where T:IMessage
        {
            if (!Socket.IsConnected) return;
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

            await Socket.SendAsync(json);
        }
    }
}
