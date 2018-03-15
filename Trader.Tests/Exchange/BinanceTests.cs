using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using BinanceExchange.API.Client.Interfaces;
using BinanceExchange.API.Enums;
using BinanceExchange.API.Models.Request;
using BinanceExchange.API.Models.Response;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Trader.Exchange;
using Trader.Networking;
using Trader.Time;

namespace Trader.Tests.Exchange
{
    [TestClass]
    public class BinanceTests
    {
        #region Ctor

        [TestMethod]
        public void Ctor_WebsocketNull_ThrowsException()
        {
            var e = Expect.Throw<ArgumentNullException>(() => {
                var subject = new Binance(null, Mock.Of<IBinanceClient>(), Mock.Of<ITime>());
            });

            Assert.AreEqual("websocket", e.ParamName);
        }

        [TestMethod]
        public void Ctor_BinanceClientNull_ThrowsException()
        {
            var e = Expect.Throw<ArgumentNullException>(() => {
                var subject = new Binance(Mock.Of<IWebSocket>(), null, Mock.Of<ITime>());
            });

            Assert.AreEqual("binanceClient", e.ParamName);
        }

        [TestMethod]
        public void Ctor_TimeNull_ThrowsException()
        {
            var e = Expect.Throw<ArgumentNullException>(() => {
                var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), null);
            });

            Assert.AreEqual("time", e.ParamName);
        }

        [TestMethod]
        public void Ctor_ArgsOK_MakerFeeIs0003()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            Assert.AreEqual(0.001M, subject.TakerFeeRate);
        }

        #endregion

        #region Initialize
        
        [TestMethod]
        public void Initialize_ValidPair_OpensSocketAndSubscribesAndReturnsBalances()
        {
            var socketMock = new Mock<IWebSocket>();
            var binanceMock = InitBinanceClientMock();

            var subject = new Binance(socketMock.Object, binanceMock.Object, Mock.Of<ITime>());

            var (asset1, asset2) = subject.Initialize(Assets.BTC, Assets.USDT).Result;

            socketMock.Verify(m => m.Connect("wss://stream.binance.com:9443/ws/btcusdt@ticker"));
            Assert.AreEqual(1M, asset1);
            Assert.AreEqual(10000M, asset2);
        }

        [TestMethod]
        public void Initialize_InvalidPair_ThrowsException()
        {
            var socketMock = new Mock<IWebSocket>();
            var binanceMock = InitBinanceClientMock();

            var subject = new Binance(socketMock.Object, binanceMock.Object, Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentException>(async () => {
                await subject.Initialize(Assets.BTC, Assets.DOGE);
            });
            
            Assert.AreEqual("Trading pair BTCDOGE is not available on Binance", e.Message);
        }

        #endregion

        #region GetAssetBalance

        [TestMethod]
        public void GetAssetBalance_UnknownAsset_ReturnsZero()
        {
            var binanceClient = new Mock<IBinanceClient>();
            binanceClient.Setup(m => m.GetAccountInformation(5000))
                .ReturnsAsync(new AccountInformationResponse()
                {
                    Balances = new List<BalanceResponse>() {
                    new BalanceResponse() { Asset = "BTC", Free = 1M }
                }
                });
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());

            var result = subject.GetAssetBalance(Assets.DOGE).Result;

            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void GetAssetBalance_ValidAsset_ReturnsValue() {
            var binanceClient = new Mock<IBinanceClient>();
            binanceClient.Setup(m => m.GetAccountInformation(5000))
                .ReturnsAsync(new AccountInformationResponse() { Balances = new List<BalanceResponse>() {
                    new BalanceResponse() { Asset = "BTC", Free = 1M }
                }});
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());

            var result = subject.GetAssetBalance(Assets.BTC).Result;

            Assert.AreEqual(1M, result);
        }

        #endregion

        #region GetCurrentPrice

        [TestMethod]
        public void GetCurrentPrice_SocketDisconnct_ReconnectsAndRetries()
        {
            var time = DateTime.Now;
            var message = new { e = "24hrTicker", b = 1.25 }.Json();
            var socketMock = new Mock<IWebSocket>();
            var timeMock = new Mock<ITime>();
            var binanceMock = InitBinanceClientMock();

            var subject = new Binance(socketMock.Object, binanceMock.Object, timeMock.Object);
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            socketMock.SetupSequence(m => m.ReceiveMessage())
                .ThrowsAsync(new WebSocketException("Shit's broken yo"))
                .ReturnsAsync(message);
            timeMock.Setup(m => m.Now).Returns(time);
            
            var result = subject.GetCurrentPrice().Result;

            Assert.AreEqual(time, result.DateTime);
            Assert.AreEqual(1.25M, result.Value);
            socketMock.Verify(m => m.Connect("wss://stream.binance.com:9443/ws/btcusdt@ticker"));
        }

        [TestMethod]
        public void GetCurrentPrice_NonTickerMessage_Retries()
        {
            var time = DateTime.Now;
            var message = new { e = "24hrTicker", b = 1.25 }.Json();
            var dummyMessage = new { type = "cat facts" }.Json();
            var socketMock = new Mock<IWebSocket>();
            var timeMock = new Mock<ITime>();
            var binanceMock = InitBinanceClientMock();

            var subject = new Binance(socketMock.Object, binanceMock.Object, timeMock.Object);
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            socketMock.SetupSequence(m => m.ReceiveMessage())
                .ReturnsAsync(dummyMessage)
                .ReturnsAsync(message);
            timeMock.Setup(m => m.Now).Returns(time);

            var result = subject.GetCurrentPrice().Result;

            Assert.AreEqual(time, result.DateTime);
            Assert.AreEqual(1.25M, result.Value);
        }

        [TestMethod]
        public void GetCurrentPrice_NotInitialized_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => {
                await subject.GetCurrentPrice();
            });

            Assert.AreEqual("Binance cannot GetCurrentPrice until Initialized!", exception.Message);
        }

        #endregion

        #region Buy

        [TestMethod]
        public void Buy_NullRate_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentNullException>(async () => {
                await subject.Buy(null, 1);
            });

            Assert.AreEqual("rate", e.ParamName);
        }

        [TestMethod]
        public void Buy_NegativeQuantity_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentException>(async () => {
                await subject.Buy(new Sample(), -1);
            });

            Assert.AreEqual("quantity", e.ParamName);
        }

        [TestMethod]
        public void Buy_ZeroQuantity_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentException>(async () => {
                await subject.Buy(new Sample(), 0);
            });

            Assert.AreEqual("quantity", e.ParamName);
        }

        [TestMethod]
        public void Buy_NotInitialized_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            var e = Expect.ThrowAsync<InvalidOperationException>(async () => {
                await subject.Buy(new Sample(), 1);
            });

            Assert.AreEqual("Cannot Buy until Initialized", e.Message);
        }

        [TestMethod]
        public void Buy_TransactionLessThanMinQuantity_ReturnsNull()
        {
            var binanceClient = InitBinanceClientMock();
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();
            
            var result = subject.Buy(new Sample() { Value = 10000M }, 1).Result;

            Assert.IsNull(result);
            binanceClient.Verify(m => m.CreateOrder(It.IsAny<CreateOrderRequest>()), Times.Never());
        }

        [TestMethod]
        public void Buy_TransactionMoreThanMinQuantity_SubmitsAndReturnsOrder()
        {
            var binanceClient = InitBinanceClientMock();
            binanceClient.Setup(m => m.CreateOrder(It.Is<CreateOrderRequest>(r => 
                r.Symbol == "BTCUSDT" &&
                r.Side == OrderSide.Buy && 
                r.Type == OrderType.Market &&
                r.Quantity == 0.0009M
            ))).ReturnsAsync(new ResultCreateOrderResponse() { ClientOrderId = "123" });
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            var result = subject.Buy(new Sample() { Value = 10000M }, 10).Result;

            Assert.AreEqual("123", result.Id);
        }

        #endregion

        #region Sell

        [TestMethod]
        public void Sell_NullRate_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentNullException>(async () => {
                await subject.Sell(null, 1);
            });

            Assert.AreEqual("rate", e.ParamName);
        }

        [TestMethod]
        public void Sell_NegativeQuantity_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentException>(async () => {
                await subject.Sell(new Sample(), -1);
            });

            Assert.AreEqual("quantity", e.ParamName);
        }

        [TestMethod]
        public void Sell_ZeroQuantity_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentException>(async () => {
                await subject.Sell(new Sample(), 0);
            });

            Assert.AreEqual("quantity", e.ParamName);
        }

        [TestMethod]
        public void Sell_NotInitialized_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            var e = Expect.ThrowAsync<InvalidOperationException>(async () => {
                await subject.Sell(new Sample(), 1);
            });

            Assert.AreEqual("Cannot Sell until Initialized", e.Message);
        }

        [TestMethod]
        public void Sell_TransactionLessThanMinQuantity_ReturnsNull()
        {
            var binanceClient = InitBinanceClientMock();
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            var result = subject.Sell(new Sample() { Value = 0.0001M }, 0.000099M).Result;

            Assert.IsNull(result);
            binanceClient.Verify(m => m.CreateOrder(It.IsAny<CreateOrderRequest>()), Times.Never());
        }

        [TestMethod]
        public void Sell_TransactionMoreThanMinQuantity_SubmitsAndReturnsOrder()
        {
            var binanceClient = InitBinanceClientMock();
            binanceClient.Setup(m => m.CreateOrder(It.Is<CreateOrderRequest>(r =>
                r.Symbol == "BTCUSDT" &&
                r.Side == OrderSide.Sell &&
                r.Type == OrderType.Market &&
                r.Quantity == 0.0005M
            ))).ReturnsAsync(new ResultCreateOrderResponse() { ClientOrderId = "123" });
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            var result = subject.Sell(new Sample() { Value = 0.0001M }, 0.0005M).Result;

            Assert.AreEqual("123", result.Id);
        }

        #endregion

        #region CheckOrder

        [TestMethod]
        public void CheckOrder_NullOrder_ThrowsException()
        {
            var binanceClient = new Mock<IBinanceClient>();
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentNullException>(async () =>
            {
                await subject.CheckOrder(null);
            });

            Assert.AreEqual("order", e.ParamName);
        }

        [TestMethod]
        public void CheckOrder_NullOrderId_ThrowsException()
        {
            var binanceClient = new Mock<IBinanceClient>();
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentException>(async () =>
            {
                await subject.CheckOrder(new Order());
            });

            Assert.AreEqual("order", e.ParamName);
        }

        [TestMethod]
        public void CheckOrder_EmptyOrderId_ThrowsException()
        {
            var binanceClient = new Mock<IBinanceClient>();
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentException>(async () =>
            {
                await subject.CheckOrder(new Order() { Id = string.Empty });
            });

            Assert.AreEqual("order", e.ParamName);
        }

        [TestMethod]
        public void CheckOrder_NotInitialized_ThrowsException()
        {
            var binanceClient = new Mock<IBinanceClient>();
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());

            var e = Expect.ThrowAsync<InvalidOperationException>(async () =>
            {
                await subject.CheckOrder(new Order() { Id = "123" });
            });

            Assert.AreEqual("Cannot CheckOrder until exchange has been Initialized", e.Message);
        }

        [TestMethod]
        public void CheckOrder_OrderIncomplete_ReturnsOrderWithSameIDMarkedUnfilled()
        {
            var binanceClient = InitBinanceClientMock();
            binanceClient.Setup(m => m.QueryOrder(It.Is<QueryOrderRequest>(q =>
                        q.OriginalClientOrderId == "123" &&
                        q.Symbol == "BTCUSDT"), 5000))
                    .ReturnsAsync(new OrderResponse() { Status = OrderStatus.PartiallyFilled });
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            var result = subject.CheckOrder(new Order() { Id = "123" }).Result;

            Assert.AreEqual("123", result.Id);
            Assert.IsFalse(result.Fulfilled);
        }

        [TestMethod]
        public void CheckOrder_OrderFilled_ReturnsOrderWithSameIDMarkedFilled()
        {
            var binanceClient = InitBinanceClientMock();
            binanceClient.Setup(m => m.QueryOrder(It.Is<QueryOrderRequest>(q =>
                        q.OriginalClientOrderId == "123" &&
                        q.Symbol == "BTCUSDT"), 5000))
                    .ReturnsAsync(new OrderResponse() { Status = OrderStatus.Filled });
            var subject = new Binance(Mock.Of<IWebSocket>(), binanceClient.Object, Mock.Of<ITime>());
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            var result = subject.CheckOrder(new Order() { Id = "123" }).Result;

            Assert.AreEqual("123", result.Id);
            Assert.IsTrue(result.Fulfilled);
        }

        #endregion

        #region Dispose

        [TestMethod]
        public void Dispose_NotInitialized_CantDoStuff()
        {
            var socketMock = new Mock<IWebSocket>();
            var subject = new Binance(socketMock.Object, Mock.Of<IBinanceClient>(), Mock.Of<ITime>());

            subject.Dispose();

            var e = Expect.ThrowAsync<InvalidOperationException>(async () => {
                await subject.GetCurrentPrice();
            });

            Assert.AreEqual("Binance cannot GetCurrentPrice until Initialized!", e.Message);
            socketMock.Verify(m => m.Dispose());
        }

        [TestMethod]
        public void Dispose_Initialized_CantDoStuff()
        {
            var socketMock = new Mock<IWebSocket>();
            var binanceMock = InitBinanceClientMock();

            var subject = new Binance(socketMock.Object, binanceMock.Object, Mock.Of<ITime>());
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            subject.Dispose();

            var e = Expect.ThrowAsync<InvalidOperationException>(async () => {
                await subject.GetCurrentPrice();
            });

            Assert.AreEqual("Binance cannot GetCurrentPrice until Initialized!", e.Message);
            socketMock.Verify(m => m.Dispose());
        }

        #endregion

        private Mock<IBinanceClient> InitBinanceClientMock()
        {
            var binanceMock = new Mock<IBinanceClient>();
            binanceMock.Setup(m => m.GetExchangeInfo()).ReturnsAsync(new ExchangeInfoResponse()
            {
                Symbols = new List<ExchangeInfoSymbol>() { new ExchangeInfoSymbol() {
                    Symbol = "BTCUSDT",
                    Filters = new List<ExchangeInfoSymbolFilter>() { new ExchangeInfoSymbolFilterLotSize() {
                        FilterType = ExchangeInfoSymbolFilterType.LotSize,
                        MinQty = 0.0001M
                    }}
                }}
            });
            binanceMock.Setup(m => m.GetAccountInformation(5000)).ReturnsAsync(new AccountInformationResponse()
            {
                Balances = new List<BalanceResponse>() {
                new BalanceResponse() { Asset = "BTC", Free = 1 },
                new BalanceResponse() { Asset = "USDT", Free = 10000 }
            }
            });
            return binanceMock;
        }
    }
}
