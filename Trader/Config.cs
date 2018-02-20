using System;

namespace Trader
{
    // TODO - load these from a file. Maybe watch file and hotload changes?
    public class Config
    {
        public readonly double NoiseThreshold = 0.005;
        public readonly double SwingThreshold = 0.20;
        public readonly double MinSwingThreshold = 0.05;
        public readonly TimeSpan SwingThresholdDecayInterval = TimeSpan.FromDays(5);

        public readonly Brokers Broker = Brokers.GDAXReadOnly;
        public readonly string TradingPair = "ETH-USD";
    }
}
