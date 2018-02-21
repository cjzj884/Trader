using Autofac;
using System;
using System.Linq;

namespace Trader
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting up");
            using (var container = ConfigureDependencies())
            {
                new Trader(new Config(), container).Run().Wait();
            }
        }

        private static IContainer ConfigureDependencies()
        {
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyTypes(typeof(Program).Assembly)
                .AssignableTo<IBroker>()
                .Keyed<IBroker>(t => GetBrokerType(t));

            return builder.Build();
        }

        private static Brokers GetBrokerType(Type type)
        {
            var att = type.GetCustomAttributes(true).OfType<BrokerTypeAttribute>().FirstOrDefault();
            if (att == null)
            {
                throw new Exception("Somone forgot to put the MessageHandlerAttribute on an IMessageHandler!");
            }

            return att.Broker;
        }
    }
}
