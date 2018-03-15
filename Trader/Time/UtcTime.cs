using System;
using System.Threading.Tasks;

namespace Trader.Time
{
    public class UtcTime : ITime
    {
        public DateTime Now => DateTime.UtcNow;

        public long UnixNow => ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();

        public Task Wait(int milliseconds)
        {
            return Task.Delay(milliseconds);
        }
    }
}
