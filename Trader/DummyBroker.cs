using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

namespace Trader
{
    public class DummyBroker : IBroker
    {
        private double fiat;
        private double crypto;
        private IWebSocket socket;

        public DummyBroker(double initialFiat, double initialCrypto, IWebSocket socket)
        {
            fiat = initialFiat;
            crypto = initialCrypto;
            this.socket = socket;
        }

        public double FiatValue { get => fiat; }

        public double CryptoValue { get => crypto; }
        
        public async Task<bool> Initialize(string tradingPair)
        {
            this.Dispose(); // Is this weird? This feels a little weird
            
            await socket.Connect("wss://ws-feed.gdax.com");
            dynamic subscribeMessage = new ExpandoObject();
            subscribeMessage.type = "subscribe";
            subscribeMessage.product_ids = new List<string>() { tradingPair };
            subscribeMessage.channels = new List<string>() { "ticker" };
            string subscribeMessageString = JsonConvert.SerializeObject(subscribeMessage);
            await socket.SendMessage(subscribeMessageString);

            var startTime = DateTime.UtcNow;
            var endTime = startTime + TimeSpan.FromMinutes(20);
            Sample startSample = null;
            Sample endSample = null;

            while (DateTime.UtcNow < endTime)
            {
                var sample = await CheckPrice();
                startSample = startSample ?? sample;
                endSample = sample;
            }

            return startSample.Value < endSample.Value;
        }

        public Task<double> Buy(Sample rate)
        {
            crypto += (fiat / rate.Value);
            var fee = crypto * 0.003;
            crypto -= fee;
            fiat = 0;
            return Task.FromResult(fee);
        }

        public Task<double> Sell(Sample rate)
        {
            fiat += (rate.Value * crypto);
            var fee = fiat * 0.003;
            fiat -= fee;
            crypto = 0;

            return Task.FromResult(fee);
        }

        public async Task<Sample> CheckPrice()
        {
            while (true)
            {
                dynamic message = JsonConvert.DeserializeObject(await socket.ReceiveMessage());

                if (message.type != "ticker")
                {
                    Console.WriteLine("Got unknown message:");
                    Console.WriteLine(message);
                    continue;
                }

                return new Sample() { Value = message.price, DateTime = DateTime.UtcNow };
            }
        }

        public void Dispose()
        {
            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }
        }
    }
}