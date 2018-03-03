using System;

namespace Trader.Exchange
{
    public class ExchangeTypeAttribute : Attribute
    {
        public Exchanges Exchange { get; private set; }

        public ExchangeTypeAttribute(Exchanges exchange)
        {
            this.Exchange = exchange;
        }
    }
}