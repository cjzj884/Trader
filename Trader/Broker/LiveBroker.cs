using System;
using System.Threading.Tasks;
using Trader.Exchange;
using Trader.Time;

namespace Trader.Broker
{
    [BrokerType(Brokers.Live)]
    public class LiveBroker : IBroker
    {
        private decimal asset1;
        private Assets asset1Type;
        private decimal asset2;
        private Assets asset2Type;
        private bool initialized;
        private readonly IExchange exchange;
        private readonly ITime time;

        public LiveBroker(IExchange exchange, ITime time)
        {
            this.exchange = exchange;
            this.time = time;
            initialized = false;
        }

        public decimal Asset1Holdings => asset1;

        public decimal Asset2Holdings => asset2;

        public async Task<bool> InitializeAsync(Assets asset1Type, Assets asset2Type)
        {
            this.asset1Type = asset1Type;
            this.asset2Type = asset2Type;
            (this.asset1, this.asset2) = await exchange.Initialize(asset1Type, asset2Type);

            DateTime startTime;
            Sample startSample = null;
            Sample endSample = null;

            do
            {
                var sample = await exchange.GetCurrentPrice();

                if (startSample == null)
                {
                    startSample = sample;
                    startTime = sample.DateTime;
                }

                endSample = sample;
            } while (endSample.DateTime - startSample.DateTime < TimeSpan.FromMinutes(10));

            initialized = true;
            return startSample.Value < endSample.Value;
        }

        public async Task<Sample> CheckPriceAsync()
        {
            if (!initialized)
                throw new InvalidOperationException("Broker cannot CheckPrice until Initialized!");

            return await exchange.GetCurrentPrice();
        }

        public async Task Buy(Sample rate)
        {
            rate = rate ?? throw new ArgumentNullException(nameof(rate));
            if (!initialized)
            {
                throw new InvalidOperationException("Broker cannot Buy until Initialized!");
            }

            Order order = await this.exchange.Buy(rate, asset2);
            if (order == null)
            {
                return; // nothing to do; already all-in on Asset 1
            }

            do
            {
                await time.Wait(1000);
                order = await this.exchange.CheckOrder(order);
            } while (!order.Fulfilled);

            this.asset1 = await this.exchange.GetAssetBalance(asset1Type);
            this.asset2 = await this.exchange.GetAssetBalance(asset2Type);
        }

        public async Task Sell(Sample rate)
        {
            rate = rate ?? throw new ArgumentNullException(nameof(rate));
            if (!initialized)
            {
                throw new InvalidOperationException("Broker cannot Sell until Initialized!");
            }

            Order order = await this.exchange.Sell(rate, asset1);
            if (order == null)
            {
                return; // nothing to do; already all-in on Asset 2
            }

            do
            {
                await time.Wait(1000);
                order = await this.exchange.CheckOrder(order);
            } while (!order.Fulfilled);

            this.asset1 = await this.exchange.GetAssetBalance(asset1Type);
            this.asset2 = await this.exchange.GetAssetBalance(asset2Type);
        }

        public decimal GetTotalValue(Sample sample)
        {
            if (sample == null)
                throw new ArgumentNullException(nameof(sample));

            return Asset2Holdings + (Asset1Holdings * sample.Value);
        }

        public void Dispose()
        {
            exchange.Dispose();
            initialized = false;
        }
    }
}
