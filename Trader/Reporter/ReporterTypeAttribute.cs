using System;

namespace Trader.Reporter
{
    public class ReporterTypeAttribute : Attribute
    {
        public ReporterTypeAttribute(Reporters reporter)
        {
            Reporter = reporter;
        }

        public Reporters Reporter { get; private set; }
    }
}
