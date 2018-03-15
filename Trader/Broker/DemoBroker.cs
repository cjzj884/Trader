using System;
using System.Threading.Tasks;
using Trader.Exchange;
using Trader.Time;

namespace Trader.Broker
{
    [BrokerType(Brokers.Demo)]
    public class DemoBroker : IBroker
    {
        private decimal asset1;
        private decimal asset2;
        private bool initialized;
        private readonly IExchange exchange;

        public DemoBroker(IExchange exchange)
        {
            asset1 = 10;
            asset2 = 10;
            this.exchange = exchange;
            initialized = false;
        }

        public decimal Asset1Holdings { get => asset1; }
        public decimal Asset2Holdings { get => asset2; }

        public async Task<bool> InitializeAsync(Assets asset1, Assets asset2)
        {
            await exchange.Initialize(asset1, asset2);

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

        public Task Buy(Sample rate)
        {
            if (rate == null)
                throw new ArgumentNullException(nameof(rate));
            if (!initialized)
                throw new InvalidOperationException("Broker cannot Buy until Initialized!");
            
            asset1 += asset2 / rate.Value;
            asset2 = 0;
            return Task.CompletedTask;
        }

        public Task Sell(Sample rate)
        {
            if (rate == null)
                throw new ArgumentNullException(nameof(rate));
            if (!initialized)
                throw new InvalidOperationException("Broker cannot Sell until Initialized!");
            
            asset2 += asset1 * rate.Value;
            asset1 = 0;

            return Task.CompletedTask;
        }

        public async Task<Sample> CheckPriceAsync()
        {
            if (!initialized)
                throw new InvalidOperationException("Broker cannot CheckPrice until Initialized!");

            return await exchange.GetCurrentPrice();
        }

        public decimal GetTotalValue(Sample rate)
        {
            if (rate == null)
                throw new ArgumentNullException(nameof(rate));

            return Asset2Holdings + (Asset1Holdings * rate.Value);
        }

        public void Dispose()
        {
            exchange.Dispose();
            initialized = false;
        }
    }
}