using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using Trader.Broker;
using Trader.Exchange;
using Trader.Time;

namespace Trader.Tests.Broker
{
    [TestClass]
    public class LiveBrokerTests
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

            var subject = new LiveBroker(exchangeMock.Object, Mock.Of<ITime>());

            var result = subject.InitializeAsync(Assets.DOGE, Assets.BTC).Result;

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

            var subject = new LiveBroker(exchangeMock.Object, Mock.Of<ITime>());

            var result = subject.InitializeAsync(Assets.DOGE, Assets.BTC).Result;

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

            var subject = new LiveBroker(exchangeMock.Object, Mock.Of<ITime>());

            var result = subject.InitializeAsync(Assets.DOGE, Assets.BTC).Result;

            Assert.IsTrue(result);
        }

        #endregion

        #region Buy

        [TestMethod]
        public void Buy_NotInitialized_ThrowsException()
        {
            var subject = new LiveBroker(Mock.Of<IExchange>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Buy(new Sample()); });

            Assert.AreEqual("Broker cannot Buy until Initialized!", exception.Message);
        }

        [TestMethod]
        public void Buy_NullSample_ThrowsException()
        {
            var subject = new LiveBroker(Mock.Of<IExchange>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<ArgumentNullException>(async () => { await subject.Buy(null); });

            Assert.AreEqual("rate", exception.ParamName);
        }

        [TestMethod]
        public void Buy_GoodSample_CalculatesCorrectly()
        {
            var mockExchange = new Mock<IExchange>();
            var sample = new Sample() { Value = 1.25M };
            var order = new Order() { Id = "123" };
            var subject = InitBroker(mockExchange, new Mock<ITime>());
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003M);
            mockExchange.Setup(m => m.Buy(sample, 1M)).ReturnsAsync(order);
            mockExchange.Setup(m => m.CheckOrder(order)).ReturnsAsync(new Order() { Fulfilled = true });
            mockExchange.Setup(m => m.GetAssetBalance(Assets.DOGE)).ReturnsAsync(18M);
            mockExchange.Setup(m => m.GetAssetBalance(Assets.BTC)).ReturnsAsync(0M);

            subject.Buy(sample).Wait();

            Assert.AreEqual(0, subject.Asset2Holdings);
            Assert.AreEqual(18, subject.Asset1Holdings);
        }

        [TestMethod]
        public void Buy_NotImmediatelyFilled_LoopsUntilFilled()
        {
            var mockTime = new Mock<ITime>();
            var mockExchange = new Mock<IExchange>();
            var sample = new Sample() { Value = 1.25M };
            var order = new Order() { Id = "123" };
            var subject = InitBroker(mockExchange, mockTime);
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003M);
            mockExchange.Setup(m => m.Buy(sample, 1M)).ReturnsAsync(order);
            mockExchange.SetupSequence(m => m.CheckOrder(It.Is<Order>(o => o.Id == "123")))
                .ReturnsAsync(new Order() { Fulfilled = false, Id = "123" })
                .ReturnsAsync(new Order() { Fulfilled = false, Id = "123" })
                .ReturnsAsync(new Order() { Fulfilled = true, Id = "123" });
            mockExchange.Setup(m => m.GetAssetBalance(Assets.DOGE)).ReturnsAsync(0M);
            mockExchange.Setup(m => m.GetAssetBalance(Assets.BTC)).ReturnsAsync(22.50M);

            subject.Buy(sample).Wait();

            Assert.AreEqual(22.50M, subject.Asset2Holdings);
            Assert.AreEqual(0M, subject.Asset1Holdings);
            mockExchange.Verify(m => m.CheckOrder(It.Is<Order>(o => o.Id == "123")), Times.Exactly(3));
            mockTime.Verify(m => m.Wait(1000), Times.Exactly(3));
        }

        [TestMethod]
        public void Buy_ExchangeReturnedNull_AssumeUnnecessaryOrderAndDoNothing()
        {
            var mockExchange = new Mock<IExchange>();
            var sample = new Sample() { Value = 1.25M };
            var subject = InitBroker(mockExchange, new Mock<ITime>());
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003M);
            mockExchange.Setup(m => m.Buy(sample, 1M)).ReturnsAsync((Order)null);

            subject.Buy(sample).Wait();

            Assert.AreEqual(1M, subject.Asset2Holdings);
            Assert.AreEqual(10M, subject.Asset1Holdings);
        }

        #endregion

        #region Sell

        [TestMethod]
        public void Sell_NotInitialized_ThrowsException()
        {
            var subject = new LiveBroker(Mock.Of<IExchange>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Sell(new Sample()); });

            Assert.AreEqual("Broker cannot Sell until Initialized!", exception.Message);
        }

        [TestMethod]
        public void Sell_NullSample_ThrowsException()
        {
            var subject = new LiveBroker(Mock.Of<IExchange>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<ArgumentNullException>(async () => { await subject.Sell(null); });

            Assert.AreEqual("rate", exception.ParamName);
        }

        [TestMethod]
        public void Sell_GoodSample_CalculatesCorrectly()
        {
            var mockExchange = new Mock<IExchange>();
            var sample = new Sample() { Value = 1.25M };
            var order = new Order() { Id = "123" };
            var subject = InitBroker(mockExchange, new Mock<ITime>());
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003M);
            mockExchange.Setup(m => m.Sell(sample, 10M)).ReturnsAsync(order);
            mockExchange.Setup(m => m.CheckOrder(order)).ReturnsAsync(new Order() { Fulfilled = true });
            mockExchange.Setup(m => m.GetAssetBalance(Assets.DOGE)).ReturnsAsync(0M);
            mockExchange.Setup(m => m.GetAssetBalance(Assets.BTC)).ReturnsAsync(22.50M);

            subject.Sell(sample).Wait();

            Assert.AreEqual(22.50M, subject.Asset2Holdings);
            Assert.AreEqual(0, subject.Asset1Holdings);
        }

        [TestMethod]
        public void Sell_NotImmediatelyFilled_LoopsUntilFilled()
        {
            var mockTime = new Mock<ITime>();
            var mockExchange = new Mock<IExchange>();
            var sample = new Sample() { Value = 1.25M };
            var order = new Order() { Id = "123" };
            var subject = InitBroker(mockExchange, mockTime);
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003M);
            mockExchange.Setup(m => m.Sell(sample, 10M)).ReturnsAsync(order);
            mockExchange.SetupSequence(m => m.CheckOrder(It.Is<Order>(o => o.Id == "123")))
                .ReturnsAsync(new Order() { Fulfilled = false, Id = "123" })
                .ReturnsAsync(new Order() { Fulfilled = false, Id = "123" })
                .ReturnsAsync(new Order() { Fulfilled = true, Id = "123" });
            mockExchange.Setup(m => m.GetAssetBalance(Assets.DOGE)).ReturnsAsync(0M);
            mockExchange.Setup(m => m.GetAssetBalance(Assets.BTC)).ReturnsAsync(22.50M);

            subject.Sell(sample).Wait();

            Assert.AreEqual(22.50M, subject.Asset2Holdings);
            Assert.AreEqual(0, subject.Asset1Holdings);
            mockExchange.Verify(m => m.CheckOrder(It.Is<Order>(o => o.Id == "123")), Times.Exactly(3));
            mockTime.Verify(m => m.Wait(1000), Times.Exactly(3));
        }

        [TestMethod]
        public void Sell_ExchangeReturnedNull_AssumeUnnecessaryOrderAndDoNothing()
        {
            var mockExchange = new Mock<IExchange>();
            var sample = new Sample() { Value = 1.25M };
            var subject = InitBroker(mockExchange, new Mock<ITime>());
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003M);
            mockExchange.Setup(m => m.Sell(sample, 1M)).ReturnsAsync((Order)null);

            subject.Sell(sample).Wait();

            Assert.AreEqual(1M, subject.Asset2Holdings);
            Assert.AreEqual(10M, subject.Asset1Holdings);
        }

        #endregion

        #region CheckPrice

        [TestMethod]
        public void CheckPrice_GetsMessage_ReturnsSample()
        {
            var sample = new Sample();
            var exchangeMock = new Mock<IExchange>();
            var subject = InitBroker(exchangeMock, new Mock<ITime>());

            exchangeMock.Setup(m => m.GetCurrentPrice()).ReturnsAsync(sample);

            var result = subject.CheckPriceAsync().Result;

            Assert.AreEqual(sample, result);
        }

        [TestMethod]
        public void CheckPrice_NotInitialized_ThrowsException()
        {
            var subject = new LiveBroker(Mock.Of<IExchange>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.CheckPriceAsync(); });

            Assert.AreEqual("Broker cannot CheckPrice until Initialized!", exception.Message);
        }

        #endregion

        #region GetTotalValue

        [TestMethod]
        public void GetTotalValue_NullSample_ThrowsException()
        {
            var subject = new LiveBroker(Mock.Of<IExchange>(), Mock.Of<ITime>());

            var exception = Expect.Throw<ArgumentNullException>(() => { subject.GetTotalValue(null); });

            Assert.AreEqual("sample", exception.ParamName);
        }

        [TestMethod]
        public void GetTotalValue_GoodSample_CalculatesCorrectly()
        {
            var sample = new Sample() { Value = 1.25M };
            var subject = InitBroker(new Mock<IExchange>(), new Mock<ITime>());

            var result = subject.GetTotalValue(sample);

            Assert.AreEqual(13.5M, result);
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
            var subject = InitBroker(mockExchange, new Mock<ITime>());

            subject.Dispose();
            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.Sell(new Sample()); });

            Assert.AreEqual("Broker cannot Sell until Initialized!", exception.Message);
            mockExchange.Verify(m => m.Dispose());
        }

        #endregion

        private LiveBroker InitBroker(Mock<IExchange> exchangeMock, Mock<ITime> timeMock)
        {
            var now = DateTime.Now;
            exchangeMock.SetupSequence(m => m.GetCurrentPrice())
                .ReturnsAsync(new Sample { Value = 1.000M, DateTime = now })
                .ReturnsAsync(new Sample { Value = 1.000M, DateTime = now + TimeSpan.FromMinutes(9) })
                .ReturnsAsync(new Sample { Value = 1.000M, DateTime = now + TimeSpan.FromMinutes(10) });
            exchangeMock.Setup(m => m.Initialize(Assets.DOGE, Assets.BTC)).ReturnsAsync((10M, 1M));

            var subject = new LiveBroker(exchangeMock.Object, timeMock.Object);

            subject.InitializeAsync(Assets.DOGE, Assets.BTC).Wait();

            exchangeMock.Reset();
            return subject;
        }
    }
}
