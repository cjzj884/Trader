using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Trader
{

    [BrokerType(Brokers.GDAXReadOnly)]
    public class DummyBroker : IBroker
    {
        private double fiat;
        private double crypto;
        private string tradingPair;
        private readonly IWebSocket socket;

        public DummyBroker(IWebSocket socket)
        {
            fiat = 0;
            crypto = 10;
            this.socket = socket;
        }

        public double FiatValue { get => fiat; }

        public double CryptoValue { get => crypto; }
        
        public async Task<bool> Initialize(string tradingPair)
        {
            this.tradingPair = tradingPair;
            await this.OpenSocketAndSubscribe(tradingPair);

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
            Sample sample = null;
            do
            {
                string json = null;
                try
                {
                    json = await socket.ReceiveMessage();
                }
                catch (WebSocketException e)
                {
                    Console.WriteLine($"SOCKET ERROR: {e.Message}");
                    Console.WriteLine("Attempting reconnect and trying again");
                    await OpenSocketAndSubscribe(this.tradingPair);
                    continue;
                }

                dynamic message = JsonConvert.DeserializeObject(json);

                if (message.type == "ticker")
                {
                    sample = new Sample() { Value = message.price, DateTime = DateTime.UtcNow };
                }
                else
                {
                    Console.WriteLine("Got unknown message:");
                    Console.WriteLine(message);
                }
            } while (sample == null);
            return sample;
        }

        public void Dispose()
        {
            if (socket != null)
            {
                socket.Dispose();
            }
        }

        private async Task OpenSocketAndSubscribe(string tradingPair)
        {
            await socket.Connect("wss://ws-feed.gdax.com");
            dynamic subscribeMessage = new {
                type = "subscribe",
                product_ids = new List<string>() { tradingPair },
                channels = new List<string>() { "ticker" }
            };
            string subscribeMessageString = JsonConvert.SerializeObject(subscribeMessage);

            await socket.SendMessage(subscribeMessageString);
        }
    }
}