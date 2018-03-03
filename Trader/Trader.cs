using System;
using System.Threading.Tasks;
using Trader.Broker;
using Trader.Reporter;

namespace Trader
{
    public class Trader
    {
        private readonly Config config;
        private readonly IBroker broker;
        private readonly IReporter reporter;
        private bool initialized = false;
        public Sample High { get; set; }
        public Sample Low { get; set; }
        public Sample Current { get; set; }
        public Sample LastSale { get; set; }
        public bool Bullish { get; set; }

        public Trader(Config config, IBroker broker, IReporter reporter)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.broker = broker ?? throw new ArgumentNullException(nameof(broker));
            this.reporter = reporter ?? throw new ArgumentNullException(nameof(reporter)); ;
        }

        public async Task Initialize()
        {
            Bullish = await broker.Initialize(config.Asset1, config.Asset2);
            await reporter.ReportInitial(Bullish);
            initialized = true;
        }

        public async Task Trade()
        {
            if (!initialized)
                throw new InvalidOperationException("Trader must be Initialized before it can Trade");

            Current = await broker.CheckPrice();
            await reporter.ReportNewPrice(broker, Current);
            LastSale = LastSale ?? Current;
            High = High == null || High.Value < Current.Value ? Current : High;
            Low = Low == null || Low.Value > Current.Value ? Current : Low;

            if (Math.Abs(Current.Value - LastSale.Value) < Current.Value * config.NoiseThreshold)
                return; // The current activity is too small for us to care

            double timeSensitiveThreshold = CalcThresholdWithDecay(Current, LastSale);

            if (Bullish)
            {
                var thresholdValue = High.Value - (timeSensitiveThreshold * (High.Value - Low.Value));
                if (Current.Value < thresholdValue)
                {
                    var fee = await broker.Sell(Current);
                    await reporter.ReportSell(broker, Current);
                    Bullish = false;
                    Low = null;
                    LastSale = Current;
                }
            }
            else // if bearish
            {
                var thresholdValue = Low.Value + (timeSensitiveThreshold * (High.Value - Low.Value));
                if (Current.Value > thresholdValue)
                {
                    var fee = await broker.Buy(Current);
                    await reporter.ReportBuy(broker, Current);
                    Bullish = true;
                    High = null;
                    LastSale = Current;
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
