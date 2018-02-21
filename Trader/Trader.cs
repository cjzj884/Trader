using Autofac;
using System;
using System.Threading.Tasks;

namespace Trader
{
    public class Trader
    {
        private readonly Config config;
        private readonly IContainer dependencies;

        public Trader(Config config, IContainer dependencies)
        {
            this.config = config;
            this.dependencies = dependencies;
        }

        public async Task Run()
        {
            var config = new Config();
            var broker = dependencies.ResolveKeyed<IBroker>(config.Broker);

            var bullish = await broker.Initialize(config.TradingPair);
            Console.WriteLine($"Warmup complete, starting out bullish={bullish}");

            Sample high = null;
            Sample low = null;
            Sample current = null;
            Sample lastSale = null;
            double totalFees = 0;

            while (true)
            {
                current = await broker.CheckPrice();
                lastSale = lastSale ?? current;
                high = high == null || high.Value < current.Value ? current : high;
                low = low == null || low.Value > current.Value ? current : low;

                if ((high.Value - low.Value) < current.Value * config.NoiseThreshold)
                    continue; // The current activity is too small for us to care

                double timeSensitiveThreshold = CalcThresholdWithDecay(current, lastSale);

                if (bullish)
                {
                    var thresholdValue = high.Value - (timeSensitiveThreshold * (high.Value - low.Value));
                    if (current.Value < thresholdValue)
                    {
                        var fee = await broker.Sell(current);
                        totalFees += fee / current.Value;
                        Console.WriteLine($"{DateTime.Now}: Executing buy @ {current.Value:0.####}: Crypto={broker.CryptoValue:0.####}, Fee={fee}");
                        Console.WriteLine($"{DateTime.Now}: Low={low.Value}@{low.DateTime}, High={high.Value}@{high.DateTime}, total fees={totalFees}");
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
                        var fee = await broker.Buy(current);
                        totalFees += fee;
                        Console.WriteLine($"{DateTime.Now}: Executing sell @ {current.Value:0.####}: Fiat={broker.FiatValue:0.####}, Fee={fee}");
                        Console.WriteLine($"{DateTime.Now}: Low={low.Value}@{low.DateTime}, High={high.Value}@{high.DateTime}, total fees={totalFees}");
                        bullish = true;
                        high = null;
                        lastSale = current;
                    }
                }
            }
        }

        private double CalcThresholdWithDecay(Sample current, Sample lastSale)
        {
            var decay = ((double)(current.DateTime - lastSale.DateTime).Ticks) / config.SwingThresholdDecayInterval.Ticks;
            decay = decay > 1 ? 1 : decay;
            var timeSensitiveThreshold = config.SwingThreshold - (decay * (config.SwingThreshold - config.MinSwingThreshold));
            return timeSensitiveThreshold;
        }
    }
}
