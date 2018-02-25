using System;

namespace Trader.Broker
{
    public class BrokerTypeAttribute : Attribute
    {
        public Brokers Broker { get; private set; }

        public BrokerTypeAttribute(Brokers broker)
        {
            this.Broker = broker;
        }
    }
}