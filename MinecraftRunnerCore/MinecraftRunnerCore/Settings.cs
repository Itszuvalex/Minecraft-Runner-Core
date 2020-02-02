using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MinecraftRunnerCore
{
    public class Settings
    {
        public string HubUrl { get; set;  }
        public string McVer { get; set; }
        public string ForgeVer { get; set; }
        public string LaunchWrapperVer { get; set; }

        public static Settings FromFile(string file)
        {
            string contents = File.ReadAllText(file);
            return JsonSerializer.Deserialize<Settings>(contents, new JsonSerializerOptions { PropertyNameCaseInsensitive = true});
        }
    }
}
