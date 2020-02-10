using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MinecraftRunnerCore.Utility
{
    class TpsLRU
    {
        class TpsLRUEntry
        {
            public string Dim { get; private set; }
            public string Tps { get; private set; }
            public DateTime LastUpdate { get; private set; }
            public TpsLRUEntry(string dim, string tps)
            {
                Dim = dim;
                Tps = tps;
                LastUpdate = DateTime.Now;
            }

            public void Refresh(string tps)
            {
                Tps = tps;
                LastUpdate = DateTime.Now;
            }
        }

        public static TimeSpan DefaultCachePurgeTimeSpan = TimeSpan.FromMinutes(1);
        private Dictionary<String, TpsLRUEntry> TpsCache;
        private TimeSpan PurgeTimeSpan { get; }
        public TpsLRU()
            : this(DefaultCachePurgeTimeSpan)
        { }

        public TpsLRU(TimeSpan purgeTimeSpan)
        {
            TpsCache = new Dictionary<string, TpsLRUEntry>();
            PurgeTimeSpan = purgeTimeSpan; 
        }

        public void AddTps(string dim, string tps)
        {
            if(TpsCache.TryGetValue(dim, out TpsLRUEntry entry))
            {
                entry.Refresh(tps);
            }
            else
            {
                TpsCache.Add(dim, new TpsLRUEntry(dim, tps));
            }

            PurgeOldValues();
        }

        private void PurgeOldValues()
        {
            var outOfDate = TpsCache.Values.Where((entry) => (DateTime.Now - entry.LastUpdate) > PurgeTimeSpan).ToArray();
            foreach(var entry in outOfDate)
            {
                Console.WriteLine(String.Format("Purging Dim=\"{0}\" from Tps Cache", entry.Dim));
                TpsCache.Remove(entry.Dim);
            }
        }

        public void Clear()
        {
            TpsCache.Clear();
        }

        public Dictionary<string, float> ToDictionary()
        {
            Dictionary<string, float> ret = new Dictionary<string, float>();
            foreach(var pair in TpsCache)
            {
                ret.Add(pair.Key, float.Parse(pair.Value.Tps));
            }
            return ret;
        }
    }
}
