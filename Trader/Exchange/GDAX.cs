using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Trader.Networking;
using Trader.Time;

namespace Trader.Exchange
{
    [ExchangeType(Exchanges.GDAX)]
    public class GDAX : IExchange
    {
        private readonly IWebSocket websocket;
        private readonly ITime time;
        private bool connected;
        private string tradingPair;
        private IEnumerable<(Assets, Assets)> validTradingPairs = new List<(Assets, Assets)> {
            (Assets.BTC, Assets.USD),
            (Assets.BCH, Assets.USD),
            (Assets.BCH, Assets.BTC),
            (Assets.ETH, Assets.USD),
            (Assets.ETH, Assets.BTC),
            (Assets.LTC, Assets.USD),
            (Assets.LTC, Assets.BTC),
        };

        public double TakerFeeRate => 0.003;

        public GDAX(IWebSocket websocket, ITime time)
        {
            this.websocket = websocket ?? throw new ArgumentNullException(nameof(websocket));
            this.time = time ?? throw new ArgumentNullException(nameof(time));
            this.connected = false;
        }

		public async Task Initialize(Assets asset1, Assets asset2)
        {
            if (!validTradingPairs.Contains((asset1, asset2)))
            {
                throw new ArgumentException($"Trading pair {asset1.ToString()}/{asset2.ToString()} is not available on GDAX");
            }

            tradingPair = asset1.ToString() + "-" + asset2.ToString();
            await OpenSocketAndSubscribe(tradingPair);
        }

		public async Task<Sample> GetCurrentPrice()
        {
            if (!connected)
                throw new InvalidOperationException("GDAX cannot GetCurrentPrice until Initialized!");

            Sample sample = null;
            do
            {
                string json = null;
                try
                {
                    json = await websocket.ReceiveMessage();
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

        private async Task OpenSocketAndSubscribe(string tradingPair)
        {
            await websocket.Connect("wss://ws-feed.gdax.com");
            connected = true;
            dynamic subscribeMessage = new
            {
                type = "subscribe",
                product_ids = new List<string>() { tradingPair },
                channels = new List<string>() { "ticker" }
            };
            string subscribeMessageString = JsonConvert.SerializeObject(subscribeMessage);

            await websocket.SendMessage(subscribeMessageString);
        }

        public void Dispose()
        {
            websocket.Dispose();
            connected = false;
        }
    }
}
