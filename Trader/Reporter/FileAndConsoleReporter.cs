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

        public async Task ReportBuy(IBroker broker, Sample sample)
        {
            PrintWithColor(ConsoleColor.Green, $"Executing buy @ {sample.Value:0.####}: Value={broker.GetTotalValue(sample):0.####}: Fees={broker.Fees}");

            using (var appender = file.AppendText())
            {
                await appender.WriteLineAsync($"{DateTime.Now},{sample.Value},buy,{broker.FiatValue},{broker.CryptoValue},{broker.Fees}");
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
                await appender.WriteLineAsync($"{DateTime.Now},{sample.Value},update,{broker.FiatValue},{broker.CryptoValue},{broker.Fees}");
                await appender.FlushAsync();
            }
        }

        public async Task ReportSell(IBroker broker, Sample sample)
        {
            PrintWithColor(ConsoleColor.Red, $"Executing sell @ {sample.Value:0.####}: Value={broker.GetTotalValue(sample):0.####}: Fees={broker.Fees}");
            using (var appender = file.AppendText())
            {
                await appender.WriteLineAsync($"{DateTime.Now},{sample.Value},sell,{broker.FiatValue},{broker.CryptoValue},{broker.Fees}");
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
