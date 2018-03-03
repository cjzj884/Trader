using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Trader.Time;
using Trader.Networking;
using Trader.Broker;
using System.Net.WebSockets;
using Trader.Exchange;

namespace Trader.Tests.Broker
{
    [TestClass]
    public class DemoBrokerTests
    {
        #region Initialize

        [TestMethod]
        public void Initialize_Connects_WarmsUpFor10Minutes()
        {
            var now = DateTime.Now;
            var exchangeMock = new Mock<IExchange>();
            exchangeMock.SetupSequence(m => m.GetCurrentPrice())
                .ReturnsAsync(new Sample { Value = 100, DateTime = now })
                .ReturnsAsync(new Sample { Value = 100, DateTime = now + TimeSpan.FromMinutes(9) })
                .ReturnsAsync(new Sample { Value = 100, DateTime = now + TimeSpan.FromMinutes(10) });

            var subject = new DemoBroker(exchangeMock.Object);

            var result = subject.Initialize(Assets.DOGE, Assets.BTC).Result;

            exchangeMock.Verify(m => m.Initialize(Assets.DOGE, Assets.BTC));
            exchangeMock.Verify(m => m.GetCurrentPrice(), Times.Exactly(3));
        }

        [TestMethod]
        public void Initialize_StartValueLarger_ReturnsFalse()
        {
            var now = DateTime.Now;
            var exchangeMock = new Mock<IExchange>();
            exchangeMock.SetupSequence(m => m.GetCurrentPrice())
                .ReturnsAsync(new Sample { Value = 101, DateTime = now })
                .ReturnsAsync(new Sample { Value = 102, DateTime = now + TimeSpan.FromMinutes(9) })
                .ReturnsAsync(new Sample { Value = 100, DateTime = now + TimeSpan.FromMinutes(10) });

            var subject = new DemoBroker(exchangeMock.Object);

            var result = subject.Initialize(Assets.DOGE, Assets.BTC).Result;

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Initialize_StartValueSmaller_ReturnsTrue()
        {
            var now = DateTime.Now;
            var exchangeMock = new Mock<IExchange>();
            exchangeMock.SetupSequence(m => m.GetCurrentPrice())
                .ReturnsAsync(new Sample { Value = 100, DateTime = now })
                .ReturnsAsync(new Sample { Value = 99, DateTime = now + TimeSpan.FromMinutes(9) })
                .ReturnsAsync(new Sample { Value = 101, DateTime = now + TimeSpan.FromMinutes(10) });

            var subject = new DemoBroker(exchangeMock.Object);

            var result = subject.Initialize(Assets.DOGE, Assets.BTC).Result;

            Assert.IsTrue(result);
        }

        #endregion

        #region Buy

        [TestMethod]
        public void Buy_NotInitialized_ThrowsException()
        {
            var subject = new DemoBroker(Mock.Of<IExchange>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Buy(new Sample()); });

            Assert.AreEqual("Broker cannot Buy until Initialized!", exception.Message);
        }

        [TestMethod]
        public void Buy_NullSample_ThrowsException()
        {
            var subject = new DemoBroker(Mock.Of<IExchange>());

            var exception = Expect.ThrowAsync<ArgumentNullException>(async () => { await subject.Buy(null); });

            Assert.AreEqual("rate", exception.ParamName);
        }

        [TestMethod]
        public void Buy_GoodSample_CalculatesCorrectly()
        {
            var mockExchange = new Mock<IExchange>();
            var sample = new Sample() { Value = 1.25 };
            var subject = InitBroker(mockExchange);
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003);

            var result = subject.Buy(sample).Result;

            Assert.AreEqual(0, subject.Asset2Holdings);
            Assert.AreEqual(17.976, subject.Asset1Holdings);
            Assert.AreEqual(0.03, subject.Fees);
            Assert.AreEqual(0.03, result);
        }

        [TestMethod]
        public void Buy_AndSell_FeesAccumulate()
        {
            var mockExchange = new Mock<IExchange>();
            var sample = new Sample() { Value = 1.25 };
            var subject = InitBroker(mockExchange);
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003);

            subject.Buy(sample).Wait();
            subject.Sell(sample).Wait();
            
            Assert.AreEqual(0.09741, subject.Fees);
        }

        #endregion

        #region Sell

        [TestMethod]
        public void Sell_NotInitialized_ThrowsException()
        {
            var subject = new DemoBroker(Mock.Of<IExchange>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Sell(new Sample()); });

            Assert.AreEqual("Broker cannot Sell until Initialized!", exception.Message);
        }

        [TestMethod]
        public void Sell_NullSample_ThrowsException()
        {
            var subject = new DemoBroker(Mock.Of<IExchange>());

            var exception = Expect.ThrowAsync<ArgumentNullException>(async () => { await subject.Sell(null); });

            Assert.AreEqual("rate", exception.ParamName);
        }

        [TestMethod]
        public void Sell_GoodSample_CalculatesCorrectly()
        {
            var mockExchange = new Mock<IExchange>();
            var sample = new Sample() { Value = 1.25 };
            var subject = InitBroker(mockExchange);
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003);

            var result = subject.Sell(sample).Result;

            Assert.AreEqual(22.4625, subject.Asset2Holdings);
            Assert.AreEqual(0, subject.Asset1Holdings);
            Assert.AreEqual(0.0375, subject.Fees);
            Assert.AreEqual(0.0375, result);
        }

        #endregion

        #region CheckPrice

        [TestMethod]
        public void CheckPrice_GetsMessage_ReturnsSample()
        {
            var sample = new Sample();
            var exchangeMock = new Mock<IExchange>();
            var subject = InitBroker(exchangeMock);

            exchangeMock.Setup(m => m.GetCurrentPrice()).ReturnsAsync(sample);

            var result = subject.CheckPrice().Result;

            Assert.AreEqual(sample, result);
        }

        [TestMethod]
        public void CheckPrice_NotInitialized_ThrowsException()
        {
            var subject = new DemoBroker(Mock.Of<IExchange>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.CheckPrice(); });

            Assert.AreEqual("Broker cannot CheckPrice until Initialized!", exception.Message);
        }

        #endregion

        #region GetTotalValue

        [TestMethod]
        public void GetTotalValue_NullSample_ThrowsException()
        {
            var subject = new DemoBroker(Mock.Of<IExchange>());

            var exception = Expect.Throw<ArgumentNullException>(() => { subject.GetTotalValue(null); });

            Assert.AreEqual("rate", exception.ParamName);
        }

        [TestMethod]
        public void GetTotalValue_GoodSample_CalculatesCorrectly()
        {
            var sample = new Sample() { Value = 1.25 };
            var subject = new DemoBroker(Mock.Of<IExchange>());

            var result = subject.GetTotalValue(sample);

            Assert.AreEqual(22.50, result);
        }

        #endregion

        #region Dispose

        [TestMethod]
        public void Dispose_NotInitialized_CannotDoStuff()
        {
            var subject = new DemoBroker(Mock.Of<IExchange>());

            subject.Dispose();
            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Sell(new Sample()); });

            Assert.AreEqual("Broker cannot Sell until Initialized!", exception.Message);
        }

        [TestMethod]
        public void Dispose_Initialized_CannotDoStuff()
        {
            var mockExchange = new Mock<IExchange>();
            var subject = InitBroker(mockExchange);

            subject.Dispose();
            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Sell(new Sample()); });

            Assert.AreEqual("Broker cannot Sell until Initialized!", exception.Message);
            mockExchange.Verify(m => m.Dispose());
        }

        #endregion

        private DemoBroker InitBroker(Mock<IExchange> socketMock)
        {
            var now = DateTime.Now;
            socketMock.SetupSequence(m => m.GetCurrentPrice())
                .ReturnsAsync(new Sample { Value = 1.000, DateTime = now })
                .ReturnsAsync(new Sample { Value = 1.000, DateTime = now + TimeSpan.FromMinutes(9) })
                .ReturnsAsync(new Sample { Value = 1.000, DateTime = now + TimeSpan.FromMinutes(10) });

            var subject = new DemoBroker(socketMock.Object);

            subject.Initialize(Assets.DOGE, Assets.DOGE).Wait();

            socketMock.Reset();
            return subject;
        }
    }
}
