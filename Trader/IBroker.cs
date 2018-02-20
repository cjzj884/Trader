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

        Task Buy(Sample rate);

        Task Sell(Sample rate);
    }
}
