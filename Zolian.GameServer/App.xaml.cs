using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

using Darkages;
using Darkages.Infrastructure;
using Darkages.Interfaces;
using Darkages.Models;

using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Zolian.GameServer
{
    public partial class App
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            base.OnStartup(e);

            SetCountryCode();
            await Crashes.SetEnabledAsync(true);
            await Analytics.SetEnabledAsync(true);
#if DEBUG
            AppCenter.Start("86ab5446-8d02-48c0-b42d-ecba68f3c91c",
                typeof(Analytics), typeof(Crashes));
#endif
#if RELEASE
            AppCenter.Start("fcfa4f49-6467-4dc0-8bda-cf170ad4acae",
                typeof(Analytics), typeof(Crashes));
#endif
            var providers = new LoggerProviderCollection();
            const string logTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("Zolian_General.txt", LogEventLevel.Verbose, logTemplate)
                .WriteTo.Console(LogEventLevel.Verbose, logTemplate)
                .CreateLogger();

            Win32.AllocConsole();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("ServerConfig.json");

            var config = builder.Build();
            var constants = config.GetSection("ServerConfig").Get<ServerConstants>();

            var serviceProvider = new ServiceCollection()
                .AddOptions()
                .AddSingleton(providers)
                .AddSingleton<ILoggerFactory>(sc =>
                {
                    var providerCollection = sc.GetService<LoggerProviderCollection>();
                    var factory = new SerilogLoggerFactory(null, true, providerCollection);

                    foreach (var provider in sc.GetServices<ILoggerProvider>())
                        factory.AddProvider(provider);

                    return factory;
                })
                .AddLogging(l => l.AddConsole())
                .Configure<ServerOptions>(config.GetSection("Content"))
                .AddSingleton<IServerConstants, ServerConstants>(_ => constants)
                .AddSingleton<IServerContext, ServerSetup>()
                .AddSingleton<IServer, Server>()
                .BuildServiceProvider();

            serviceProvider.GetService<IServer>();
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Crashes.TrackError(e.Exception);
            e.Handled = true;
        }

        private static void SetCountryCode()
        {
            var countryCode = RegionInfo.CurrentRegion.TwoLetterISORegionName;
            AppCenter.SetCountryCode(countryCode);
        }
    }
}
