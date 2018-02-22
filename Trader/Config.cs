using System;
using System.Configuration;

namespace Trader
{
    public class Config
    {
        public Config()
        {
            Reload();
        }

        public void Reload()
        {
            // TODO add some sanity checks
            try
            {
                ConfigurationManager.RefreshSection("appSettings");
                NoiseThreshold = double.Parse(ConfigurationManager.AppSettings["NoiseThreshold"]);
                SwingThreshold = double.Parse(ConfigurationManager.AppSettings["SwingThreshold"]);
                MinSwingThreshold = double.Parse(ConfigurationManager.AppSettings["MinSwingThreshold"]);
                SwingThresholdDecayInterval = TimeSpan.FromDays(int.Parse(ConfigurationManager.AppSettings["SwingThresholdDecayIntervalDays"]));
                Broker = (Brokers)Enum.Parse(typeof(Brokers), ConfigurationManager.AppSettings["Broker"]);
                TradingPair = ConfigurationManager.AppSettings["TradingPair"];
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception while updating config values: {e.Message}");
                throw;
            }
        }

        public double NoiseThreshold { get; private set; }
        public double SwingThreshold { get; private set; }
        public double MinSwingThreshold { get; private set; }
        public TimeSpan SwingThresholdDecayInterval { get; private set; }
        public Brokers Broker { get; private set; }
        public string TradingPair { get; private set; }
    }
}
