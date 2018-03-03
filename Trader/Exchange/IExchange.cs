using System;
using System.Threading.Tasks;

namespace Trader.Exchange
{
    public interface IExchange : IDisposable
    {
        double TakerFeeRate { get; }

        Task Initialize(Assets asset1, Assets asset2);

        Task<Sample> GetCurrentPrice();
    }
}
