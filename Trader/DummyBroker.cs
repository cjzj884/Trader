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

            socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri("wss://ws-feed.gdax.com"), CancellationToken.None);
            dynamic subscribeMessage = new ExpandoObject();
            subscribeMessage.type = "subscribe";
            subscribeMessage.product_ids = new List<string>() { tradingPair };
            subscribeMessage.channels = new List<string>() { "ticker" };
            string subscribeMessageString = JsonConvert.SerializeObject(subscribeMessage);

            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(subscribeMessageString)), WebSocketMessageType.Text, true, CancellationToken.None);

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
                var buffer = new ArraySegment<byte>(new byte[1024]);
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
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
    }
}