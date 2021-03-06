using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using IoFile = System.IO.File;

namespace TelegramFileDownloader
{
    public class Worker : IHostedService
    {
        private readonly ILogger<Worker> log;
        private readonly TelegramBotClient client;
        private readonly StorageOptions options;

        public Worker(ILogger<Worker> log, TelegramBotClient client, IOptions<StorageOptions> options)
        {
            this.log = log;
            this.client = client;
            this.options = options.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            client.OnMessage += OnMessage;
            client.StartReceiving(cancellationToken: cancellationToken);

            await ProcessUpdatesAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            client.StopReceiving();
            return Task.CompletedTask;
        }

        private async Task ProcessUpdatesAsync()
        {
            var updates = await client.GetUpdatesAsync();
            foreach (var update in updates)
                await SaveFileFromMessageAsync(update.Message);
        }

        private async Task SaveFileFromMessageAsync(Message message)
        {
            if (message.Photo == null || message.Photo.Length == 0)
                return;

            string fileId = message.Photo.OrderByDescending(p => p.Width).First().FileId;
            await SaveFileAsync(fileId);
        }

        private async Task SaveFileAsync(string fileId)
        {
            var file = await client.GetFileAsync(fileId);
            var filePath = Path.Combine(options.RootPath, Path.GetFileName(file.FilePath));
            using (var fileContent = IoFile.OpenWrite(filePath))
                await client.DownloadFileAsync(file.FilePath, fileContent);
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            _ = SaveFileFromMessageAsync(e.Message);
        }
    }
}
