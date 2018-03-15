using System;
using System.Threading.Tasks;

namespace Trader.Exchange
{
    public interface IExchange : IDisposable
    {
        decimal TakerFeeRate { get; }

        Task<(decimal, decimal)> Initialize(Assets asset1, Assets asset2);

        Task<Sample> GetCurrentPrice();
        Task<Order> CheckOrder(Order order);
        Task<Order> Buy(Sample rate, decimal quantity);
        Task<decimal> GetAssetBalance(Assets asset1Type);
        Task<Order> Sell(Sample rate, decimal quantity);
    }
}
