using System.Threading.Tasks;
using Trader.Broker;

namespace Trader.Reporter
{
    public interface IReporter
    {
        Task ReportBuy(IBroker broker, Sample sample);
        Task ReportAttemptedBuy(IBroker broker, Sample sample);

        Task ReportSell(IBroker broker, Sample sample);
        Task ReportAttemptedSell(IBroker broker, Sample sample);

        Task ReportNewPrice(IBroker broker, Sample sample);
        Task ReportInitial(bool bullish);
    }
}
