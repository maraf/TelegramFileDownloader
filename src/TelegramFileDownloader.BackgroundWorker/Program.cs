using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegramFileDownloader
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.Local.json",
                        optional: true,
                        reloadOnChange: true
                    );
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<StorageOptions>(hostContext.Configuration.GetSection("Storage"));
                    services.Configure<TelegramOptions>(hostContext.Configuration.GetSection("Telegram"));
                    services.AddTransient(s => new TelegramBotClient(s.GetRequiredService<IOptions<TelegramOptions>>().Value.Token));
                    services.AddHostedService<Worker>();
                });
    }
}
