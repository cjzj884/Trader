using System;
using System.Threading.Tasks;
using Trader.Exchange;

namespace Trader.Broker
{
    public interface IBroker : IDisposable
    {
        decimal Asset1Holdings { get; }
        decimal Asset2Holdings { get; }

        decimal GetTotalValue(Sample sample);

        Task<bool> InitializeAsync(Assets asset1, Assets asset2);

        Task<Sample> CheckPriceAsync();

        Task Buy(Sample rate);

        Task Sell(Sample rate);
    }
}
