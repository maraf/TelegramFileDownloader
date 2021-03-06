using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neptuo;
using Neptuo.FileSystems;
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
            var assemblyName = GetType().Assembly.GetName();
            log.LogInformation("Running '{app}' version '{version}'.", assemblyName.Name, assemblyName.Version.ToString(3));

            Ensure.Condition.DirectoryExists(options.RootPath, "rootPath");
            log.LogInformation("Storing files to '{rootPath}'.", options.RootPath);

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
            log.LogDebug("Getting updates...");

            var updates = await client.GetUpdatesAsync();
            log.LogInformation("Found '{updates}' updates.", updates.Length);

            foreach (var update in updates)
                await SaveFileFromMessageAsync(update.Message);
        }

        private async Task SaveFileFromMessageAsync(Message message)
        {
            if (message.Photo == null || message.Photo.Length == 0)
                return;

            string fileId = message.Photo.OrderByDescending(p => p.Width).First().FileId;
            log.LogInformation("Save file id '{fileId}' from message id '{messageId}'.", fileId, message.MessageId);

            await SaveFileAsync(fileId);
        }

        private async Task SaveFileAsync(string fileId)
        {
            var file = await client.GetFileAsync(fileId);
            var fileName = Path.GetFileName(file.FilePath);
            var filePath = Path.Combine(options.RootPath, fileName);
            log.LogInformation("Saving file '{fileName}'...", fileName);

            using (var fileContent = IoFile.OpenWrite(filePath))
                await client.DownloadFileAsync(file.FilePath, fileContent);

            log.LogInformation("Saving file '{fileName}' completed.", fileName);
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            _ = SaveFileFromMessageAsync(e.Message);
        }
    }
}
