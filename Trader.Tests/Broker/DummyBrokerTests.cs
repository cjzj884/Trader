using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Trader.Time;
using Trader.Networking;
using Trader.Broker;
using System.Net.WebSockets;

namespace Trader.Tests.Broker
{
    [TestClass]
    public class DummyBrokerTests
    {
        #region Initialize

        [TestMethod]
        public void Initialize_NullTradePair_ThrowsException()
        {
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());
            
            var result = Expect.ThrowAsync<ArgumentNullException>(async () => { await subject.Initialize(null); });

            Assert.AreEqual("tradingPair", result.ParamName);
        }

        [TestMethod]
        public void Initialize_EmptyTradePair_ThrowsException()
        {
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            var result = Expect.ThrowAsync<ArgumentException>(async () => { await subject.Initialize(""); });

            Assert.AreEqual("tradingPair", result.ParamName);
        }

        [TestMethod]
        public void Initialize_Connects_WarmsUpFor10Minutes()
        {
            var timeMock = new Mock<ITime>();
            var now = DateTime.Now;
            timeMock.SetupSequence(m => m.Now)
                .Returns(now).Returns(now).Returns(now)
                .Returns(now + TimeSpan.FromMinutes(9)).Returns(now + TimeSpan.FromMinutes(9))
                .Returns(now + TimeSpan.FromMinutes(11)).Returns(now + TimeSpan.FromMinutes(11));
            var socketMock = new Mock<IWebSocket>();
            socketMock.Setup(m => m.ReceiveMessage()).ReturnsAsync(new { type = "ticker", price = 1.000 }.Json());

            var subject = new DummyBroker(socketMock.Object, timeMock.Object);

            var result = subject.Initialize("DOGE-DOGE").Result;

            socketMock.Verify(m => m.Connect("wss://ws-feed.gdax.com"));
            socketMock.Verify(m => m.SendMessage(new {
                type = "subscribe",
                product_ids = new[] { "DOGE-DOGE" },
                channels = new[] { "ticker" }
            }.Json()));
            socketMock.Verify(m => m.ReceiveMessage(), Times.Exactly(2));
        }

        [TestMethod]
        public void Initialize_StartValueLarger_ReturnsFalse()
        {
            var timeMock = new Mock<ITime>();
            var now = DateTime.Now;
            timeMock.SetupSequence(m => m.Now)
                .Returns(now).Returns(now).Returns(now)
                .Returns(now + TimeSpan.FromMinutes(6)).Returns(now + TimeSpan.FromMinutes(6))
                .Returns(now + TimeSpan.FromMinutes(9)).Returns(now + TimeSpan.FromMinutes(9))
                .Returns(now + TimeSpan.FromMinutes(11)).Returns(now + TimeSpan.FromMinutes(11));
            var socketMock = new Mock<IWebSocket>();
            socketMock.SetupSequence(m => m.ReceiveMessage())
                .ReturnsAsync(new { type = "ticker", price = 1.001 }.Json())
                .ReturnsAsync(new { type = "ticker", price = 1000 }.Json())
                .ReturnsAsync(new { type = "ticker", price = 1.000 }.Json());

            var subject = new DummyBroker(socketMock.Object, timeMock.Object);

            var result = subject.Initialize("DOGE-BTC").Result;

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Initialize_StartValueSmaller_ReturnsTrue()
        {
            var timeMock = new Mock<ITime>();
            var now = DateTime.Now;
            timeMock.SetupSequence(m => m.Now)
                .Returns(now).Returns(now).Returns(now)
                .Returns(now + TimeSpan.FromMinutes(6)).Returns(now + TimeSpan.FromMinutes(6))
                .Returns(now + TimeSpan.FromMinutes(9)).Returns(now + TimeSpan.FromMinutes(9))
                .Returns(now + TimeSpan.FromMinutes(11)).Returns(now + TimeSpan.FromMinutes(11));
            var socketMock = new Mock<IWebSocket>();
            socketMock.SetupSequence(m => m.ReceiveMessage())
                .ReturnsAsync(new { type = "ticker", price = 1.000 }.Json())
                .ReturnsAsync(new { type = "ticker", price = 0.100 }.Json())
                .ReturnsAsync(new { type = "ticker", price = 1000 }.Json());

            var subject = new DummyBroker(socketMock.Object, timeMock.Object);

            var result = subject.Initialize("DOGE-BTC").Result;

            Assert.IsTrue(result);
        }

        #endregion

        #region Buy

        [TestMethod]
        public void Buy_NotInitialized_ThrowsException()
        {
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Buy(new Sample()); });

            Assert.AreEqual("Broker cannot Buy until Initialized!", exception.Message);
        }

        [TestMethod]
        public void Buy_NullSample_ThrowsException()
        {
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<ArgumentNullException>(async () => { await subject.Buy(null); });

            Assert.AreEqual("rate", exception.ParamName);
        }

        [TestMethod]
        public void Buy_GoodSample_CalculatesCorrectly()
        {
            var sample = new Sample() { Value = 1.25 };
            var subject = InitBroker(new Mock<IWebSocket>(), new Mock<ITime>());

            var result = subject.Buy(sample).Result;

            Assert.AreEqual(0, subject.FiatValue);
            Assert.AreEqual(17.976, subject.CryptoValue);
            Assert.AreEqual(0.03, subject.Fees);
            Assert.AreEqual(0.03, result);
        }

        [TestMethod]
        public void Buy_AndSell_FeesAccumulate()
        {
            var sample = new Sample() { Value = 1.25 };
            var subject = InitBroker(new Mock<IWebSocket>(), new Mock<ITime>());

            subject.Buy(sample).Wait();
            subject.Sell(sample).Wait();
            
            Assert.AreEqual(0.09741, subject.Fees);
        }

        #endregion

        #region Sell

        [TestMethod]
        public void Sell_NotInitialized_ThrowsException()
        {
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Sell(new Sample()); });

            Assert.AreEqual("Broker cannot Sell until Initialized!", exception.Message);
        }

        [TestMethod]
        public void Sell_NullSample_ThrowsException()
        {
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<ArgumentNullException>(async () => { await subject.Sell(null); });

            Assert.AreEqual("rate", exception.ParamName);
        }

        [TestMethod]
        public void Sell_GoodSample_CalculatesCorrectly()
        {
            var sample = new Sample() { Value = 1.25 };
            var subject = InitBroker(new Mock<IWebSocket>(), new Mock<ITime>());

            var result = subject.Sell(sample).Result;

            Assert.AreEqual(22.4625, subject.FiatValue);
            Assert.AreEqual(0, subject.CryptoValue);
            Assert.AreEqual(0.0375, subject.Fees);
            Assert.AreEqual(0.0375, result);
        }

        #endregion

        #region CheckPrice

        [TestMethod]
        public void CheckPrice_GetsMessage_ReturnsSample()
        {
            var time = DateTime.Now;
            var message = new { type = "ticker", price = 1.25 }.Json();
            var socketMock = new Mock<IWebSocket>();
            var timeMock = new Mock<ITime>();
            var subject = InitBroker(socketMock, timeMock);

            socketMock.Setup(m => m.ReceiveMessage()).ReturnsAsync(message);
            timeMock.Setup(m => m.Now).Returns(time);

            var result = subject.CheckPrice().Result;

            Assert.AreEqual(time, result.DateTime);
            Assert.AreEqual(1.25, result.Value);
        }

        [TestMethod]
        public void CheckPrice_SocketDisconnct_ReconnectsAndRetries()
        {
            var time = DateTime.Now;
            var message = new { type = "ticker", price = 1.25 }.Json();
            var socketMock = new Mock<IWebSocket>();
            var timeMock = new Mock<ITime>();
            var subject = InitBroker(socketMock, timeMock);

            socketMock.SetupSequence(m => m.ReceiveMessage())
                .ThrowsAsync(new WebSocketException("Shit's broken yo"))
                .ReturnsAsync(message);
            timeMock.Setup(m => m.Now).Returns(time);

            var result = subject.CheckPrice().Result;

            Assert.AreEqual(time, result.DateTime);
            Assert.AreEqual(1.25, result.Value);
            socketMock.Verify(m => m.Connect("wss://ws-feed.gdax.com"));
            socketMock.Verify(m => m.SendMessage(new {
                type = "subscribe",
                product_ids = new[] { "DOGE-DOGE" },
                channels = new[] { "ticker" }
            }.Json()));
        }

        [TestMethod]
        public void CheckPrice_NonTickerMessage_Retries()
        {
            var time = DateTime.Now;
            var message = new { type = "ticker", price = 1.25 }.Json();
            var dummyMessage = new { type = "cat facts" }.Json();
            var socketMock = new Mock<IWebSocket>();
            var timeMock = new Mock<ITime>();
            var subject = InitBroker(socketMock, timeMock);

            socketMock.SetupSequence(m => m.ReceiveMessage())
                .ReturnsAsync(dummyMessage)
                .ReturnsAsync(message);
            timeMock.Setup(m => m.Now).Returns(time);

            var result = subject.CheckPrice().Result;

            Assert.AreEqual(time, result.DateTime);
            Assert.AreEqual(1.25, result.Value);
        }

        [TestMethod]
        public void CheckPrice_NotInitialized_ThrowsException()
        {
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.CheckPrice(); });

            Assert.AreEqual("Broker cannot CheckPrice until Initialized!", exception.Message);
        }

        #endregion

        #region GetTotalValue

        [TestMethod]
        public void GetTotalValue_NullSample_ThrowsException()
        {
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            var exception = Expect.Throw<ArgumentNullException>(() => { subject.GetTotalValue(null); });

            Assert.AreEqual("rate", exception.ParamName);
        }

        [TestMethod]
        public void GetTotalValue_GoodSample_CalculatesCorrectly()
        {
            var sample = new Sample() { Value = 1.25 };
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            var result = subject.GetTotalValue(sample);

            Assert.AreEqual(22.50, result);
        }

        #endregion

        #region Dispose

        [TestMethod]
        public void Dispose_NotInitialized_CannotDoStuff()
        {
            var subject = new DummyBroker(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            subject.Dispose();
            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Sell(new Sample()); });

            Assert.AreEqual("Broker cannot Sell until Initialized!", exception.Message);
        }

        [TestMethod]
        public void Dispose_Initialized_CannotDoStuff()
        {
            var mockSocket = new Mock<IWebSocket>();
            var subject = InitBroker(mockSocket, new Mock<ITime>());

            subject.Dispose();
            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Sell(new Sample()); });

            Assert.AreEqual("Broker cannot Sell until Initialized!", exception.Message);
            mockSocket.Verify(m => m.Dispose());
        }

        #endregion

        private DummyBroker InitBroker(Mock<IWebSocket> socketMock, Mock<ITime> timeMock)
        {
            var now = DateTime.Now;
            timeMock.SetupSequence(m => m.Now)
                .Returns(now)
                .Returns(now).Returns(now)
                .Returns(now + TimeSpan.FromMinutes(11));
            
            socketMock.Setup(m => m.ReceiveMessage()).ReturnsAsync(new { type = "ticker", price = 1.000 }.Json());

            var subject = new DummyBroker(socketMock.Object, timeMock.Object);

            subject.Initialize("DOGE-DOGE").Wait();

            socketMock.Reset();
            timeMock.Reset();
            return subject;
        }
    }
}
