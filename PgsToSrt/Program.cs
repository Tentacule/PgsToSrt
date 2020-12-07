using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Reflection;

namespace PgsToSrt
{
    class Program
    {

        static void Main(string[] args)
        {
            var options = Parser.Default.ParseArguments<CommandLineOptions>(args);

            if (options is Parsed<CommandLineOptions> values)
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Console.WriteLine($"PgsToSrt {version}");
                Console.WriteLine();

                var servicesProvider = new ServiceCollection()
                    .AddLogging(builder =>
                    {
                        builder.SetMinimumLevel(LogLevel.Trace);
                        builder.AddNLog();
                    })
                    .AddTransient<Runner>()
                    .BuildServiceProvider();

                var runner = servicesProvider.GetRequiredService<Runner>();

                runner.Run(values);
                Console.Write("Done.");
            }
        }
    }
}