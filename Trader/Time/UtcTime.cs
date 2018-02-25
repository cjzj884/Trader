using System;

namespace Trader.Time
{
    public class UtcTime : ITime
    {
        public DateTime Now => DateTime.UtcNow;
    }
}
