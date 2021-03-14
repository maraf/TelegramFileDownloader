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
        private readonly Synchronizer synchronizer;

        public Worker(ILogger<Worker> log, TelegramBotClient client, IOptions<StorageOptions> storageOptions, IOptions<TelegramOptions> telegramOptions, Synchronizer synchronizer)
        {
            this.log = log;
            this.client = client;
            this.client = client;
            this.storageOptions = storageOptions.Value;
            this.telegramOptions = telegramOptions.Value;
            this.synchronizer = synchronizer;
        }

        private void Debug(string message, params object[] parameters) => log.LogDebug(message, parameters);
        private void Info(string message, params object[] parameters) => log.LogInformation(message, parameters);
        private void Error(Exception e, string message, params object[] parameters) => log.LogError(e, message, parameters);

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var assemblyName = GetType().Assembly.GetName();
            Info("Running '{app}' version '{version}'.", assemblyName.Name, assemblyName.Version.ToString(3));

            Ensure.Condition.DirectoryExists(storageOptions.RootPath, "rootPath");
            Info("Storing files to '{rootPath}'.", storageOptions.RootPath);

            Info("Start receiving messages.");
            client.OnMessage += OnMessage;
            client.StartReceiving(cancellationToken: cancellationToken);

            //await ProcessUpdatesAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Info("Stop receiving messages.");

            client.StopReceiving();
            return Task.CompletedTask;
        }

        private async Task ProcessUpdatesAsync()
        {
            Debug("Getting updates...");

            var updates = await client.GetUpdatesAsync();
            Info("Found '{updates}' updates.", updates.Length);

            foreach (var update in updates)
                await SaveFileFromMessageAsync(update.Message);
        }

        private async Task SaveFileFromMessageAsync(Message message)
        {
            try
            {
                if (telegramOptions.AllowedSenderId != null && !telegramOptions.AllowedSenderId.Contains(message.From.Id))
                {
                    Info($"Message '{message.MessageId}' skipped, because sender '{message.From.Id}' is not allowed.");
                    return;
                }

                string fileId = null;
                string fileName = message.Caption;
                switch (message.Type)
                {
                    case Telegram.Bot.Types.Enums.MessageType.Photo:
                        fileId = message.Photo.OrderByDescending(p => p.Width).First().FileId;
                        break;
                    case Telegram.Bot.Types.Enums.MessageType.Document:
                        if (telegramOptions.AllowedFileTypes == null || telegramOptions.AllowedFileTypes.Contains(message.Document.MimeType))
                        {
                            fileId = message.Document.FileId;
                        }

                        break;
                }

                if (fileId == null)
                {
                    Info("Message '{messageId}' skipped, because it doesn't contain file or the file is not of supported type.", message.MessageId);
                    return;
                }

                Info("Save file id '{fileId}' from message id '{messageId}'.", fileId, message.MessageId);
                await SaveFileAsync(fileId, fileName);
            }
            catch (Exception e)
            {
                Error(e, "Save file failed for message '{messageId}'.", message.MessageId);
            }
        }

        private async Task SaveFileAsync(string fileId, string fileName = null)
        {
            var file = await client.GetFileAsync(fileId);
            if (file == null)
                return;

            if (telegramOptions.AllowedFileSize != null && file.FileSize > telegramOptions.AllowedFileSize.Value)
            {
                Info("Selected file '{fileId}' of size '{fileSize}B' exceeds max allowed size '{maxSize}B'.", fileId, file.FileSize, telegramOptions.AllowedFileSize.Value);
                return;
            }

            if (fileName != null)
            {
                if (String.IsNullOrEmpty(Path.GetExtension(fileName)))
                    fileName += Path.GetExtension(file.FilePath);
            }
            else
            {
                fileName = Path.GetFileName(file.FilePath);
            }

            var filePath = Path.Combine(storageOptions.RootPath, fileName);
            Info("Saving file '{fileName}'...", fileName);

            try
            {
                await synchronizer.WaitAsync(filePath);

                using (var fileContent = IoFile.OpenWrite(filePath))
                    await client.DownloadFileAsync(file.FilePath, fileContent);

                Info("Saving file '{fileName}' completed.", fileName);
            }
            finally
            {
                synchronizer.Relese(filePath);
            }
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            Info("New message '{messageId}' arrived.", e.Message.MessageId);
            _ = SaveFileFromMessageAsync(e.Message);
        }
    }
}
