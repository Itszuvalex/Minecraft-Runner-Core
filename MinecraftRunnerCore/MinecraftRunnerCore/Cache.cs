using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MinecraftRunnerCore
{
    public class Cache
    {
        public Guid Guid
        {
            get
            {
                if (string.IsNullOrEmpty(_guid))
                {
                    _guid = Guid.Empty.ToString();
                }
                return Guid.Parse(_guid);
            }
            set
            {
                _guid = value.ToString();
            }
        }
        private string _guid { get; set; }
        private string CacheFile;
        public Cache()
        {
        }

        public static Cache FromFile(string file)
        {
            string contents = File.ReadAllText(file);
            var cache = JsonSerializer.Deserialize<Cache>(contents, new JsonSerializerOptions { PropertyNameCaseInsensitive = true});
            if (cache != null)
            {
                cache.CacheFile = file;
            }
            return cache;
        }

        public void Flush()
        {
            var data = JsonSerializer.Serialize<Cache>(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CacheFile, data);
            Console.WriteLine("Flushing cache");
        }
    }
}
