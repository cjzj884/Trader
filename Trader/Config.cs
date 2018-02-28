using System;
using System.Configuration;
using Trader.Broker;
using Trader.Reporter;

namespace Trader
{
    public class Config
    {
        public Config(bool loadNow = true)
        {
            if (loadNow)
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
                Reporter = (Reporters)Enum.Parse(typeof(Reporters), ConfigurationManager.AppSettings["Reporter"]);
                TradingPair = ConfigurationManager.AppSettings["TradingPair"];
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception while updating config values: {e.Message}");
                throw;
            }
        }

        public double NoiseThreshold { get; set; }
        public double SwingThreshold { get; set; }
        public double MinSwingThreshold { get; set; }
        public TimeSpan SwingThresholdDecayInterval { get; set; }
        public Brokers Broker { get; set; }
        public Reporters Reporter { get; set; }
        public string TradingPair { get; set; }
    }
}
