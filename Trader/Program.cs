using Autofac;
using FluentScheduler;
using System;
using System.Linq;
using Trader.Broker;
using Trader.Networking;
using Trader.Reporter;
using Trader.Time;

namespace Trader
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting up");

            var config = new Config();

            var registry = new Registry();
            registry.Schedule(() => config.Reload()).ToRunEvery(30).Seconds();
            JobManager.Initialize(registry);
            JobManager.JobException += (exInfo) => { throw exInfo.Exception; };

            using (var container = ConfigureDependencies())
            {
                new Trader(config, container).Run().Wait();
            }
            JobManager.StopAndBlock();
        }

        private static IContainer ConfigureDependencies()
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<WebSocket>().As<IWebSocket>();
            builder.RegisterType<UtcTime>().As<ITime>();

            builder.RegisterAssemblyTypes(typeof(Program).Assembly)
                .AssignableTo<IBroker>()
                .Keyed<IBroker>(t => GetBrokerType(t));

            builder.RegisterAssemblyTypes(typeof(Program).Assembly)
                .AssignableTo<IReporter>()
                .Keyed<IReporter>(t => GetReporterType(t));

            return builder.Build();
        }


        private static Reporters GetReporterType(Type type)
        {
            var att = type.GetCustomAttributes(true).OfType<ReporterTypeAttribute>().FirstOrDefault();
            if (att == null)
            {
                throw new Exception($"This reporter ({type.FullName}) doesn't have a ReporterTypeAttribute; it can't be used and I hate it and I'm going to die now");
            }

            return att.Reporter;
        }

        private static Brokers GetBrokerType(Type type)
        {
            var att = type.GetCustomAttributes(true).OfType<BrokerTypeAttribute>().FirstOrDefault();
            if (att == null)
            {
                throw new Exception($"This broker ({type.FullName}) doesn't have a BrokerTypeAttribute; it can't be used and I hate it and I'm going to die now");
            }

            return att.Broker;
        }
    }
}
