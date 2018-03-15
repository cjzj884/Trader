using System;
using System.Configuration;
using Trader.Broker;
using Trader.Exchange;
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
            try
            {
                ConfigurationManager.RefreshSection("appSettings");
                NoiseThreshold = double.Parse(ConfigurationManager.AppSettings["NoiseThreshold"]);
                SwingThreshold = double.Parse(ConfigurationManager.AppSettings["SwingThreshold"]);
                MinSwingThreshold = double.Parse(ConfigurationManager.AppSettings["MinSwingThreshold"]);
                SwingThresholdDecayInterval = TimeSpan.FromDays(int.Parse(ConfigurationManager.AppSettings["SwingThresholdDecayIntervalDays"]));
                Exchange = (Exchanges)Enum.Parse(typeof(Exchanges), ConfigurationManager.AppSettings["Exchange"]);
                Broker = (Brokers)Enum.Parse(typeof(Brokers), ConfigurationManager.AppSettings["Broker"]);
                Reporter = (Reporters)Enum.Parse(typeof(Reporters), ConfigurationManager.AppSettings["Reporter"]);
                Asset1 = (Assets)Enum.Parse(typeof(Assets), ConfigurationManager.AppSettings["Asset1"]);
                Asset2 = (Assets)Enum.Parse(typeof(Assets), ConfigurationManager.AppSettings["Asset2"]);
                ApiKey = ConfigurationManager.AppSettings["ApiKey"];
                ApiKeySecret = ConfigurationManager.AppSettings["ApiKeySecret"];
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
        public Exchanges Exchange { get; set; }
        public Brokers Broker { get; set; }
        public Reporters Reporter { get; set; }
        public Assets Asset1 { get; set; }
        public Assets Asset2 { get; set; }
        public string ApiKey { get; set; }
        public string ApiKeySecret { get; set; }
    }
}
