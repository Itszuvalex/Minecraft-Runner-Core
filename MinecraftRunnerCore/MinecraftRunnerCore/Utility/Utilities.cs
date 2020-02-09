using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftRunnerCore
{
    static class Utilities
    {
        public static async Task DownloadFile(string localPath, string netPath, bool returnIfExists)
        {
            if (File.Exists(localPath) && returnIfExists) return;

            Directory.CreateDirectory(localPath);

            var message = Program.HttpClient.GetAsync(netPath);
            using var stream = File.OpenWrite(localPath);
            var body = await message.Result.Content.ReadAsStreamAsync();
            await body.CopyToAsync(stream);
        }
    }
}
