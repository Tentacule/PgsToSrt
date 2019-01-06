using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace PgsToSrt
{
    class Program
    {
        private static ServiceProvider _servicesProvider;

        static void Main(string[] args)
        {
            var options = Parser.Default.ParseArguments<CommandLineOptions>(args);
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            Console.WriteLine($"PgsToSrt {version}");
            Console.WriteLine();

            _servicesProvider = new ServiceCollection()
               .AddLogging(builder =>
               {
                   builder.SetMinimumLevel(LogLevel.Trace);
                   builder.AddNLog();
               })
               .AddTransient<Runner>()
               .BuildServiceProvider();

            var runner = _servicesProvider.GetRequiredService<Runner>();
            var values = options as Parsed<CommandLineOptions>;

            if (runner.Run(values))
                Console.Write("Done.");

            Console.ReadLine();
        }
    }
}