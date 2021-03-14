using Neptuo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TelegramFileDownloader
{
    public class Synchronizer
    {
        private Dictionary<string, SemaphoreSlim> storage = new Dictionary<string, SemaphoreSlim>();
        private object storageLock = new object();

        public async Task WaitAsync(string identifier)
        {
            Ensure.NotNull(identifier, "identifier");
            if (!storage.TryGetValue(identifier, out var semaphore))
            {
                lock (storageLock)
                {
                    if (!storage.TryGetValue(identifier, out semaphore))
                        storage[identifier] = semaphore = new SemaphoreSlim(1, 1);
                }
            }

            await semaphore.WaitAsync();
        }

        public void Relese(string identifier)
        {
            if (!storage.TryGetValue(identifier, out var semaphore))
                return;

            semaphore.Release();
        }
    }
}
