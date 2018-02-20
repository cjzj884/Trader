using System;
using System.Threading.Tasks;

namespace Trader
{
    class Program
    {
        private static readonly Config config = new Config();

        static void Main(string[] args)
        {
            Console.WriteLine("Starting up");
            Run().Wait();
        }

        private static async Task Run()
        {
            var config = new Config();
            var broker = ResolveBroker(config.Broker);
            
            var bullish = await broker.Initialize(config.TradingPair);
            Console.WriteLine($"Warmup complete, starting out bullish={bullish}");

            Sample high = null;
            Sample low = null;
            Sample current = null;
            Sample lastSale = null;

            while (true)
            {
                current  = await broker.CheckPrice();
                lastSale = lastSale ?? current;
                high     = high == null || high.Value < current.Value ? current : high;
                low      = low == null || low.Value > current.Value ? current : low;
                    
                if ((high.Value - low.Value) < current.Value * config.NoiseThreshold)
                    continue; // The current activity is too small for us to care

                double timeSensitiveThreshold = CalcThresholdWithDecay(current, lastSale);

                if (bullish)
                {
                    var thresholdValue = high.Value - (timeSensitiveThreshold * (high.Value - low.Value));
                    if (current.Value < thresholdValue)
                    {
                        await broker.Sell(current);
                        Console.WriteLine($"{DateTime.Now}: Executing buy @ {current.Value:0.####}: Fiat={broker.FiatValue:0.####}, Crypto={broker.CryptoValue:0.####}");
                        Console.WriteLine($"{DateTime.Now}: Low={low.Value}@{low.DateTime}, High={high.Value}@{high.DateTime}");
                        bullish = false;
                        low = null;
                        lastSale = current;
                    }
                }
                else // if bearish
                {
                    var thresholdValue = low.Value + (timeSensitiveThreshold * (high.Value - low.Value));
                    if (current.Value > thresholdValue)
                    {
                        await broker.Buy(current);
                        Console.WriteLine($"{DateTime.Now}: Executing sell @ {current.Value:0.####}: Fiat={broker.FiatValue:0.####}, Crypto={broker.CryptoValue:0.####}");
                        Console.WriteLine($"{DateTime.Now}: Low={low.Value}@{low.DateTime}, High={high.Value}@{high.DateTime}");
                        bullish = true;
                        high = null;
                        lastSale = current;
                    }
                }
            }
        }

        private static double CalcThresholdWithDecay(Sample current, Sample lastSale)
        {
            var decay = ((double)(current.DateTime - lastSale.DateTime).Ticks) / config.SwingThresholdDecayInterval.Ticks;
            decay = decay > 1 ? 1 : decay;
            var timeSensitiveThreshold = config.SwingThreshold - (decay * (config.SwingThreshold - config.MinSwingThreshold));
            return timeSensitiveThreshold;
        }

        private static IBroker ResolveBroker(Brokers brokerType)
        {
            return new DummyBroker(0, 4);
        }
    }
}
