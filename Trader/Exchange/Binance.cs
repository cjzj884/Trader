using BinanceExchange.API.Client.Interfaces;
using BinanceExchange.API.Enums;
using BinanceExchange.API.Models.Request;
using BinanceExchange.API.Models.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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
        private readonly IBinanceClient binanceClient;
        private string tradingPair;
        private bool connected = false;
        private decimal minQuantity;

        public Binance(IWebSocket websocket, IBinanceClient binanceClient, ITime time)
        {
            this.websocket = websocket ?? throw new ArgumentNullException(nameof(websocket));
            this.time = time ?? throw new ArgumentNullException(nameof(time));
            this.binanceClient = binanceClient ?? throw new ArgumentNullException(nameof(binanceClient));
        }

        public decimal TakerFeeRate => 0.001M;

        public async Task<(decimal, decimal)> Initialize(Assets asset1, Assets asset2)
        {
            tradingPair = asset1.ToString().ToUpper() + asset2.ToString().ToUpper();
            var exchangeMetadata = await binanceClient.GetExchangeInfo();
            var pairInfo = exchangeMetadata.Symbols.Where(s => s.Symbol == tradingPair).FirstOrDefault();
            if (pairInfo == null)
            {
                throw new ArgumentException($"Trading pair {tradingPair} is not available on Binance");
            }
            
            minQuantity = (pairInfo.Filters.FirstOrDefault(f => f.FilterType == ExchangeInfoSymbolFilterType.LotSize) as ExchangeInfoSymbolFilterLotSize).MinQty;

            var asset1Balance = await GetAssetBalance(asset1);
            var asset2Balance = await GetAssetBalance(asset2);

            await this.websocket.Connect($"wss://stream.binance.com:9443/ws/{tradingPair.ToLower()}@ticker");
            
            connected = true;
            return (asset1Balance, asset2Balance);
        }

        public async Task<decimal> GetAssetBalance(Assets assetType)
        {
            var account = await binanceClient.GetAccountInformation();
            return account.Balances.FirstOrDefault(b => b.Asset == assetType.ToString())?.Free ?? 0;
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
                    await this.websocket.Connect($"wss://stream.binance.com:9443/ws/{tradingPair.ToLower()}@ticker");
                    continue;
                }

                JObject message = JsonConvert.DeserializeObject(json) as JObject;
                if (message != null &&
                    message.ContainsKey("e") &&
                    message.GetValue("e").ToString() == "24hrTicker" &&
                    message.ContainsKey("b") &&
                    decimal.TryParse(message.GetValue("b").ToString(), out decimal price))
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

        public async Task<Order> Buy(Sample rate, decimal quantity)
        {
            rate = rate ?? throw new ArgumentNullException(nameof(rate));
            if (quantity <= 0)
            {
                throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
            }

            if (!connected)
            {
                throw new InvalidOperationException("Cannot Buy until Initialized");
            }

            // This quantity is coming in as asset 2, divide by rate to get us asset 1
            quantity = quantity / rate.Value;
            // Deduct the approximate fee we'll need - the API isn't very smart about implicitly including that.
            var calculatedQuantity = quantity - (this.TakerFeeRate * quantity);
            // We're not allowed to sell micro-quantities, cut off the trailing decimals
            calculatedQuantity = calculatedQuantity - (calculatedQuantity % minQuantity);

            if (calculatedQuantity <= 0)
            {
                return null;
            }

            var response = await binanceClient.CreateOrder(new CreateOrderRequest() {
                Symbol = tradingPair,
                Quantity = Math.Round(calculatedQuantity, 20),
                Side = OrderSide.Buy,
                Type = OrderType.Market
            });
            return new Order()
            {
                Id = response.ClientOrderId,
                Fulfilled = false
            };
        }

        public async Task<Order> Sell(Sample rate, decimal quantity)
        {
            rate = rate ?? throw new ArgumentNullException(nameof(rate));
            if (quantity <= 0)
            {
                throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
            }

            if (!connected)
            {
                throw new InvalidOperationException("Cannot Sell until Initialized");
            }

            // We're not allowed to sell micro-quantities, cut off the trailing decimals
            var calculatedQuantity = quantity - (quantity % minQuantity);

            if (calculatedQuantity <= 0)
            {
                return null;
            }

            var response = await binanceClient.CreateOrder(new CreateOrderRequest()
            {
                Symbol = tradingPair,
                Quantity = Math.Round(calculatedQuantity, 20),
                Side = OrderSide.Sell,
                Type = OrderType.Market
            });
            return new Order()
            {
                Id = response.ClientOrderId,
                Fulfilled = false
            };
        }

        public async Task<Order> CheckOrder(Order order)
        {
            order = order ?? throw new ArgumentNullException(nameof(order));
            if (order.Id == null || order.Id == string.Empty)
                throw new ArgumentException("Order's ID must not be null or empty", nameof(order));

            if (!connected)
                throw new InvalidOperationException("Cannot CheckOrder until exchange has been Initialized");

            var updatedOrder = await binanceClient.QueryOrder(new QueryOrderRequest() { OriginalClientOrderId = order.Id, Symbol = tradingPair });
            return new Order()
            {
                Id = order.Id,
                Fulfilled = updatedOrder.Status == OrderStatus.Filled
            };
        }

        public void Dispose()
        {
            connected = false;
            this.websocket.Dispose();
        }
    }
}
