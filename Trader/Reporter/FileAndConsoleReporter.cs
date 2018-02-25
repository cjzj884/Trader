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

        public FileAndConsoleReporter()
        {
            var exe = Assembly.GetEntryAssembly();
            var exeDir = exe.Location.Substring(0, exe.Location.LastIndexOf(Path.DirectorySeparatorChar));
            var datetime = DateTime.Now;
            this.file = new FileInfo(exeDir + Path.DirectorySeparatorChar + datetime.ToString("MM-dd-yyyy HH-mm") + "_reporting.csv");
        }

        public async Task ReportBuy(IBroker broker, Sample sample)
        {
            Console.WriteLine($"{DateTime.Now}: Executing buy @ {sample.Value:0.####}: Value={broker.GetTotalValue(sample):0.####}: Fees={broker.Fees}");
            using (var appender = file.AppendText())
            {
                await appender.WriteLineAsync($"{DateTime.Now},{sample.Value},buy,{broker.FiatValue},{broker.CryptoValue},{broker.Fees}");
                await appender.FlushAsync();
            }
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
            Console.WriteLine($"{DateTime.Now}: Executing sell @ {sample.Value:0.####}: Value={broker.GetTotalValue(sample):0.####}: Fees={broker.Fees}");
            using (var appender = file.AppendText())
            {
                await appender.WriteLineAsync($"{DateTime.Now},{sample.Value},sell,{broker.FiatValue},{broker.CryptoValue},{broker.Fees}");
                await appender.FlushAsync();
            }
        }
    }
}
