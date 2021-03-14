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
        private readonly StorageOptions storageOptions;
        private readonly TelegramOptions telegramOptions;

        public Worker(ILogger<Worker> log, TelegramBotClient client, IOptions<StorageOptions> storageOptions, IOptions<TelegramOptions> telegramOptions)
        {
            this.log = log;
            this.client = client;
            this.client = client;
            this.storageOptions = storageOptions.Value;
            this.telegramOptions = telegramOptions.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var assemblyName = GetType().Assembly.GetName();
            log.LogInformation("Running '{app}' version '{version}'.", assemblyName.Name, assemblyName.Version.ToString(3));

            Ensure.Condition.DirectoryExists(storageOptions.RootPath, "rootPath");
            log.LogInformation("Storing files to '{rootPath}'.", storageOptions.RootPath);

            log.LogInformation("Start receiving messages.");
            client.OnMessage += OnMessage;
            client.StartReceiving(cancellationToken: cancellationToken);

            //await ProcessUpdatesAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            log.LogInformation("Stop receiving messages.");

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
            try
            {
                if (telegramOptions.AllowedSenderId != null && !telegramOptions.AllowedSenderId.Contains(message.From.Id))
                {
                    log.LogInformation($"Message '{message.MessageId}' skipped, because sender '{message.From.Id}' is not allowed.");
                    return;
                }

                string fileId = null;
                switch (message.Type)
                {
                    case Telegram.Bot.Types.Enums.MessageType.Photo:
                        fileId = message.Photo.OrderByDescending(p => p.Width).First().FileId;
                        break;
                    case Telegram.Bot.Types.Enums.MessageType.Document:
                        fileId = message.Document.FileId;
                        break;
                }

                if (fileId == null)
                {
                    log.LogInformation($"Message '{message.MessageId}' skipped, because it doesn't contain file.");
                    return;
                }

                log.LogInformation("Save file id '{fileId}' from message id '{messageId}'.", fileId, message.MessageId);
                await SaveFileAsync(fileId);
            }
            catch (Exception e)
            {
                log.LogError(e, "Save file failed for message '{messageId}'.", message.MessageId);
            }
        }

        private async Task SaveFileAsync(string fileId)
        {
            var file = await client.GetFileAsync(fileId);
            var fileName = Path.GetFileName(file.FilePath);
            var filePath = Path.Combine(storageOptions.RootPath, fileName);
            log.LogInformation("Saving file '{fileName}'...", fileName);

            using (var fileContent = IoFile.OpenWrite(filePath))
                await client.DownloadFileAsync(file.FilePath, fileContent);

            log.LogInformation("Saving file '{fileName}' completed.", fileName);
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            log.LogInformation("New message '{messageId}' arrived.", e.Message.MessageId);
            _ = SaveFileFromMessageAsync(e.Message);
        }
    }
}
