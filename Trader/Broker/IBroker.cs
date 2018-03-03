using System;
using System.Threading.Tasks;
using Trader.Exchange;

namespace Trader.Broker
{
    public interface IBroker : IDisposable
    {
        double Asset1Holdings { get; }
        double Asset2Holdings { get; }

        double Fees { get; }

        double GetTotalValue(Sample sample);

        Task<bool> Initialize(Assets asset1, Assets asset2);

        Task<Sample> CheckPrice();

        Task<double> Buy(Sample rate);

        Task<double> Sell(Sample rate);
    }
}
