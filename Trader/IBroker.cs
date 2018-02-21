using System;
using System.Threading.Tasks;

namespace Trader
{
    public interface IBroker : IDisposable
    {
        double FiatValue { get; }

        double CryptoValue { get; }

        Task<bool> Initialize(string tradingPair);

        Task<Sample> CheckPrice();

        Task<double> Buy(Sample rate);

        Task<double> Sell(Sample rate);
    }
}
