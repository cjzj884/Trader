using System;
using System.Threading.Tasks;

namespace Trader.Time
{
    public interface ITime
    {
        DateTime Now { get; }

        long UnixNow { get; }

        Task Wait(int v);
    }
}
