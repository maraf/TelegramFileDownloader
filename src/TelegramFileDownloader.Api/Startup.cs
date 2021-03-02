using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramFileDownloader.Api
{
    public class Startup
    {
        private static TelegramBotClient client;
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            string token = configuration.GetValue<string>("Telegram:Token");
            client = new TelegramBotClient(token);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    var me = await client.GetMeAsync();
                    await context.Response.WriteAsync($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");
                });

                endpoints.MapGet("/latest-image", async context =>
                {
                    var updates = await client.GetUpdatesAsync();
                    if (updates.Length == 0)
                    {
                        context.Response.StatusCode = 404;
                        return;
                    }

                    var message = updates.Last().Message;
                    if (message.Photo == null && message.Photo.Length == 0)
                    {
                        context.Response.StatusCode = 404;
                        return;
                    }

                    string fileId = message.Photo.OrderByDescending(p => p.Width).First().FileId;
                    await DownloadFileAsync(context, fileId);
                });
            });
        }

        private static async Task DownloadFileAsync(HttpContext context, string fileId)
        {
            var file = await client.GetFileAsync(fileId);
            await client.DownloadFileAsync(file.FilePath, context.Response.Body);
        }
    }
}
