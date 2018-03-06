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
    [ExchangeType(Exchanges.Binance)]
    public class Binance : IExchange
    {
        private readonly IWebSocket websocket;
        private readonly ITime time;
        private string tradingPair;
        private bool connected = false;
        private IEnumerable<(Assets, Assets)> validTradingPairs = new List<(Assets, Assets)> {
            (Assets.BTC, Assets.USDT),
            (Assets.ETH, Assets.USDT)
        };

        public Binance(IWebSocket websocket, ITime time)
        {
            this.websocket = websocket ?? throw new ArgumentNullException(nameof(websocket));
            this.time = time ?? throw new ArgumentNullException(nameof(time));
        }

        public double TakerFeeRate => 0.001;

        public async Task Initialize(Assets asset1, Assets asset2)
        {
            if (!validTradingPairs.Contains((asset1, asset2)))
            {
                throw new ArgumentException($"Trading pair {asset1.ToString()}/{asset2.ToString()} is not available on Binance");
            }

            tradingPair = asset1.ToString().ToLower() + asset2.ToString().ToLower();
            await this.websocket.Connect($"wss://stream.binance.com:9443/ws/{tradingPair}@ticker");
            connected = true;
        }

        public async Task<Sample> GetCurrentPrice()
        {
            if (!connected)
            {
                throw new InvalidOperationException("Binance cannot GetCurrentPrice until Initialized!");
            }

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
                    Console.WriteLine($"SOCKET ERROR: {e.Message}");
                    Console.WriteLine("Attempting reconnect and trying again");
                    await this.websocket.Connect($"wss://stream.binance.com:9443/ws/{tradingPair}@ticker");
                    continue;
                }

                JObject message = JsonConvert.DeserializeObject(json) as JObject;
                double price = 0;
                if (message != null &&
                    message.ContainsKey("e") &&
                    message.GetValue("e").ToString() == "24hrTicker" &&
                    message.ContainsKey("b") &&
                    Double.TryParse(message.GetValue("b").ToString(), out price))
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

        public void Dispose()
        {
            connected = false;
            this.websocket.Dispose();
        }
    }
}
