using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Trader
{
    public class DummyBroker : IBroker
    {
        private double fiat;
        private double crypto;
        private ClientWebSocket socket;
        private string tradingPair;

        public DummyBroker(double initialFiat, double initialCrypto)
        {
            fiat = initialFiat;
            crypto = initialCrypto;
        }

        public double FiatValue { get => fiat; }

        public double CryptoValue { get => crypto; }

        public async Task<bool> Initialize(string tradingPair)
        {
            this.Dispose(); // Is this weird? This feels a little weird
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

        public Task Buy(Sample rate)
        {
            crypto += (fiat / rate.Value);
            fiat = 0;
            return Task.CompletedTask;
        }

        public Task Sell(Sample rate)
        {
            fiat += (rate.Value * crypto);
            crypto = 0;

            return Task.CompletedTask;
        }

        public async Task<Sample> CheckPrice()
        {
            while (true)
            {
                var buffer = new ArraySegment<byte>(new byte[1024]);

                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                }
                catch (WebSocketException e)
                {
                    // Connection disrupted
                    Console.WriteLine($"Warning - connection disrupted: {e.Message}");
                    Console.WriteLine($"Attempting to re-establish...");
                    await this.OpenSocketAndSubscribe(this.tradingPair);
                    return await this.CheckPrice();
                }
                
                dynamic message = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(buffer.Array, 0, result.Count));

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

        private async Task OpenSocketAndSubscribe(string tradingPair)
        {
            socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri("wss://ws-feed.gdax.com"), CancellationToken.None);
            dynamic subscribeMessage = new {
                type = "subscribe",
                product_ids = new List<string>() { tradingPair },
                channels = new List<string>() { "ticker" }
            };
            string subscribeMessageString = JsonConvert.SerializeObject(subscribeMessage);

            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(subscribeMessageString)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}