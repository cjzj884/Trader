using System;
namespace Trader.Time
{
    public interface ITime
    {
        DateTime Now { get; }
    }
}
