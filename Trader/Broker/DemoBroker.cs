using System;
using System.Threading.Tasks;
using Trader.Exchange;
using Trader.Time;

namespace Trader.Broker
{
    [BrokerType(Brokers.Demo)]
    public class DemoBroker : IBroker
    {
        private double asset1;
        private double asset2;
        private double fees;
        private bool initialized;
        private readonly IExchange exchange;

        public DemoBroker(IExchange exchange)
        {
            asset1 = 10;
            asset2 = 10;
            this.exchange = exchange;
            initialized = false;
        }

        public double Asset1Holdings { get => asset1; }
        public double Asset2Holdings { get => asset2; }

        public double Fees { get => fees; }

        public async Task<bool> Initialize(Assets asset1, Assets asset2)
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

        public Task<double> Buy(Sample rate)
        {
            if (rate == null)
                throw new ArgumentNullException(nameof(rate));
            if (!initialized)
                throw new InvalidOperationException("Broker cannot Buy until Initialized!");

            var fee = asset2 * exchange.TakerFeeRate;
            asset1 += ((asset2 - fee) / rate.Value);
            fees += fee;
            asset2 = 0;
            return Task.FromResult(fee);
        }

        public Task<double> Sell(Sample rate)
        {
            if (rate == null)
                throw new ArgumentNullException(nameof(rate));
            if (!initialized)
                throw new InvalidOperationException("Broker cannot Sell until Initialized!");

            var fee = (asset1 * rate.Value) * exchange.TakerFeeRate;
            asset2 += (asset1 - (fee / rate.Value)) * rate.Value;
            fees += fee;
            asset1 = 0;

            return Task.FromResult(fee);
        }

        public async Task<Sample> CheckPrice()
        {
            if (!initialized)
                throw new InvalidOperationException("Broker cannot CheckPrice until Initialized!");

            return await exchange.GetCurrentPrice();
        }

        public double GetTotalValue(Sample rate)
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