using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Trader.Broker;

namespace Trader.Reporter
{
    [ReporterType(Reporters.FileAndConsole)]
    public class FileAndConsoleReporter : IReporter
    {
        private readonly FileInfo file;
        private readonly ConsoleColor originalColor;

        public FileAndConsoleReporter()
        {
            var exe = Assembly.GetEntryAssembly();
            var exeDir = exe.Location.Substring(0, exe.Location.LastIndexOf(Path.DirectorySeparatorChar));
            var datetime = DateTime.Now;
            this.file = new FileInfo(exeDir + Path.DirectorySeparatorChar + datetime.ToString("MM-dd-yyyy HH-mm") + "_reporting.csv");

            this.originalColor = Console.ForegroundColor;
        }

        public Task ReportAttemptedBuy(IBroker broker, Sample sample)
        {
            PrintWithColor(ConsoleColor.White, $"Executing buy @ {sample.Value:0.####}");
            return Task.CompletedTask;
        }

        public Task ReportAttemptedSell(IBroker broker, Sample sample)
        {
            PrintWithColor(ConsoleColor.White, $"Executing sell @ {sample.Value:0.####}");
            return Task.CompletedTask;
        }

        public async Task ReportBuy(IBroker broker, Sample sample)
        {
            PrintWithColor(ConsoleColor.Green, $"Executed buy @ {sample.Value:0.####}: Value={broker.GetTotalValue(sample):0.####} ({broker.Asset1Holdings} asset 1)");

            using (var appender = file.AppendText())
            {
                await appender.WriteLineAsync($"{DateTime.Now},{sample.Value},buy,{broker.Asset2Holdings},{broker.Asset1Holdings}");
                await appender.FlushAsync();
            }
        }

        public Task ReportInitial(bool bullish)
        {
            PrintWithColor(ConsoleColor.Yellow, $"Finished initializing, bullish = {bullish}");
            return Task.CompletedTask;
        }

        public async Task ReportNewPrice(IBroker broker, Sample sample)
        {
            using (var appender = file.AppendText())
            {
                await appender.WriteLineAsync($"{DateTime.Now},{sample.Value},update,{broker.Asset2Holdings},{broker.Asset1Holdings}");
                await appender.FlushAsync();
            }
        }

        public async Task ReportSell(IBroker broker, Sample sample)
        {
            PrintWithColor(ConsoleColor.Red, $"Executed sell @ {sample.Value:0.####}: Value={broker.GetTotalValue(sample):0.####} ({broker.Asset2Holdings} asset 2)");
            using (var appender = file.AppendText())
            {
                await appender.WriteLineAsync($"{DateTime.Now},{sample.Value},sell,{broker.Asset2Holdings},{broker.Asset1Holdings}");
                await appender.FlushAsync();
            }
        }

        private void PrintWithColor(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"{DateTime.Now}: {message}");
            Console.ForegroundColor = originalColor;
        }
    }
}
