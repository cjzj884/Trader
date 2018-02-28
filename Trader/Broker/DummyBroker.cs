using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Trader.Networking;
using Trader.Time;

namespace Trader.Broker
{
    [BrokerType(Brokers.GDAXReadOnly)]
    public class DummyBroker : IBroker
    {
        private double fiat;
        private double crypto;
        private double fees;
        private string tradingPair;
        private bool connected;
        private readonly IWebSocket socket;
        private readonly ITime time;

        public DummyBroker(IWebSocket socket, ITime time)
        {
            fiat = 10;
            crypto = 10;
            this.socket = socket;
            this.time = time;
            connected = false;
        }

        public double FiatValue { get => fiat; }

        public double CryptoValue { get => crypto; }

        public double Fees { get => fees; }

        public async Task<bool> Initialize(string tradingPair)
        {
            this.tradingPair = tradingPair ?? throw new ArgumentNullException(nameof(tradingPair));
            if (tradingPair == string.Empty)
            {
                throw new ArgumentException($"{nameof(tradingPair)} cannot be an empty string", nameof(tradingPair));
            }
            await this.OpenSocketAndSubscribe(tradingPair);

            var startTime = time.Now;
            var endTime = startTime + TimeSpan.FromMinutes(10);
            Sample startSample = null;
            Sample endSample = null;

            while (time.Now < endTime)
            {
                var sample = await CheckPrice();
                startSample = startSample ?? sample;
                endSample = sample;
            }

            return startSample.Value < endSample.Value;
        }

        public Task<double> Buy(Sample rate)
        {
            if (rate == null)
                throw new ArgumentNullException(nameof(rate));
            if (!connected)
                throw new InvalidOperationException("Broker cannot Buy until Initialized!");

            var fee = fiat * 0.003;
            crypto += ((fiat - fee) / rate.Value);
            fees += fee;
            fiat = 0;
            return Task.FromResult(fee);
        }

        public Task<double> Sell(Sample rate)
        {
            if (rate == null)
                throw new ArgumentNullException(nameof(rate));
            if (!connected)
                throw new InvalidOperationException("Broker cannot Sell until Initialized!");

            var fee = (crypto * rate.Value) * 0.003;
            fiat += (crypto - (fee / rate.Value)) * rate.Value;
            fees += fee;
            crypto = 0;

            return Task.FromResult(fee);
        }

        public async Task<Sample> CheckPrice()
        {
            if (!connected)
                throw new InvalidOperationException("Broker cannot CheckPrice until Initialized!");

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
                    connected = false;
                    Console.WriteLine($"SOCKET ERROR: {e.Message}");
                    Console.WriteLine("Attempting reconnect and trying again");
                    await OpenSocketAndSubscribe(this.tradingPair);
                    continue;
                }

                JObject message = JsonConvert.DeserializeObject(json) as JObject;
                double price = 0;
                if (message != null &&
                    message.ContainsKey("type") &&
                    message.GetValue("type").ToString() == "ticker" &&
                    message.ContainsKey("price") &&
                    Double.TryParse(message.GetValue("price").ToString(), out price))
                {
                    sample = new Sample() { Value = price, DateTime = time.Now };
                }
                else
                {
                    Console.WriteLine("Got non-ticker/unparseable message:");
                    Console.WriteLine(message);
                }
            } while (sample == null);
            return sample;
        }

        public double GetTotalValue(Sample rate)
        {
            if (rate == null)
                throw new ArgumentNullException(nameof(rate));

            return FiatValue + (CryptoValue * rate.Value);
        }

        public void Dispose()
        {
            if (socket != null)
            {
                socket.Dispose();
            }
            connected = false;
        }

        private async Task OpenSocketAndSubscribe(string tradingPair)
        {
            await socket.Connect("wss://ws-feed.gdax.com");
            connected = true;
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