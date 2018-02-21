using Autofac;
using System;
using System.Linq;

namespace Trader
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting up");
<<<<<<< HEAD
            Run().Wait();
        }

        private static async Task Run()
        {
            var config = new Config();
            var broker = ResolveBroker(config.Broker);
            
            var bullish = await broker.Initialize(config.TradingPair);
            Console.WriteLine($"Warmup complete, starting out bullish={bullish}");
            var initialPrice = await broker.CheckPrice();
            if (bullish)
            {
                await broker.Buy(initialPrice);
            }
            else
            {
                await broker.Sell(initialPrice);
            }

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
                        Console.WriteLine($"{DateTime.Now}: Executing sell @ {current.Value:0.####}: Fiat={broker.FiatValue:0.####}, Crypto={broker.CryptoValue:0.####}, Low={low.Value}@{low.DateTime}, High={high.Value}@{high.DateTime}");
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
                        Console.WriteLine($"{DateTime.Now}: Executing buy @ {current.Value:0.####}: Fiat={broker.FiatValue:0.####}, Crypto={broker.CryptoValue:0.####}, Low={low.Value}@{low.DateTime}, High={high.Value}@{high.DateTime}");
                        bullish = true;
                        high = null;
                        lastSale = current;
                    }
                }
=======
            using (var container = ConfigureDependencies())
            {
                new Trader(new Config(), container).Run().Wait();
>>>>>>> c07ad1b3a0eae830b7904911c696c8466f46dac1
            }
        }

        private static IContainer ConfigureDependencies()
        {
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyTypes(typeof(Program).Assembly)
                .AssignableTo<IBroker>()
                .Keyed<IBroker>(t => GetBrokerType(t));

            return builder.Build();
        }

        private static Brokers GetBrokerType(Type type)
        {
            var att = type.GetCustomAttributes(true).OfType<BrokerTypeAttribute>().FirstOrDefault();
            if (att == null)
            {
                throw new Exception("Somone forgot to put the MessageHandlerAttribute on an IMessageHandler!");
            }

            return att.Broker;
        }
    }
}
