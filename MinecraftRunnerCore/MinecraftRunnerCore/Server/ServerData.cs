﻿using MinecraftDiscordBotCore.Models.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using MinecraftRunnerCore.Utility;

namespace MinecraftRunnerCore.Server
{
    class ServerData
    {
        public string Name { get; }
        public long Memory { get; set; }
        public long MemoryMax { get; set; }
        public long Storage { get; set; }
        public long StorageMax { get; set; }
        public HashSet<string> Players { get; }
        public int PlayerCount { get; set; }
        public int PlayerMax { get; set; }
        public TpsLRU Tps { get; }
        public string Status { get; set; }
        public int ActiveTime => Convert.ToInt32((DateTime.Now - CreationTime).TotalSeconds);

        private DateTime CreationTime { get; }

        public ServerData(string name)
        {
            Name = name;
            Players = new HashSet<string>();
            Tps = new TpsLRU();
            CreationTime = DateTime.Now;
        }

        public McServerStatus ToMessage()
        {
            return new McServerStatus
            (
                name: Name,
                memory: Memory,
                memorymax: MemoryMax,
                storage: Storage,
                storagemax: StorageMax,
                players: Players.ToArray(),
                playercount: PlayerCount,
                playermax: PlayerMax,
                tps: Tps.ToDictionary(),
                status: Status,
                activeTime: ActiveTime
            );
        }
    }
}
