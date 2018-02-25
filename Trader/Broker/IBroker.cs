using System;
using System.Threading.Tasks;

namespace Trader.Broker
{
    public interface IBroker : IDisposable
    {
        double FiatValue { get; }

        double CryptoValue { get; }

        double Fees { get; }

        double GetTotalValue(Sample sample);

        Task<bool> Initialize(string tradingPair);

        Task<Sample> CheckPrice();

        Task<double> Buy(Sample rate);

        Task<double> Sell(Sample rate);
    }
}
