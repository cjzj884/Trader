using System.Threading.Tasks;
using Trader.Broker;

namespace Trader.Reporter
{
    public interface IReporter
    {
        Task ReportBuy(IBroker broker, Sample sample);

        Task ReportSell(IBroker broker, Sample sample);

        Task ReportNewPrice(IBroker broker, Sample sample);
    }
}
