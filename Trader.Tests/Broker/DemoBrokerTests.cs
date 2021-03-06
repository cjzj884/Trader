﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using Trader.Broker;
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

            var subject = new DemoBroker(exchangeMock.Object);

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

            var subject = new DemoBroker(exchangeMock.Object);

            var result = subject.InitializeAsync(Assets.DOGE, Assets.BTC).Result;

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
            var sample = new Sample() { Value = 1.25M };
            var subject = InitBroker(mockExchange);
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003M);

            subject.Buy(sample);

            Assert.AreEqual(0, subject.Asset2Holdings);
            Assert.AreEqual(18, subject.Asset1Holdings);
            mockExchange.Verify(m => m.Buy(It.IsAny<Sample>(), It.IsAny<decimal>()), Times.Never);
            mockExchange.Verify(m => m.Sell(It.IsAny<Sample>(), It.IsAny<decimal>()), Times.Never);
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
            var sample = new Sample() { Value = 1.25M };
            var subject = InitBroker(mockExchange);
            mockExchange.Setup(m => m.TakerFeeRate).Returns(0.003M);

            subject.Sell(sample);

            Assert.AreEqual(22.50M, subject.Asset2Holdings);
            Assert.AreEqual(0, subject.Asset1Holdings);
            mockExchange.Verify(m => m.Buy(It.IsAny<Sample>(), It.IsAny<decimal>()), Times.Never);
            mockExchange.Verify(m => m.Sell(It.IsAny<Sample>(), It.IsAny<decimal>()), Times.Never);
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

            var result = subject.CheckPriceAsync().Result;

            Assert.AreEqual(sample, result);
        }

        [TestMethod]
        public void CheckPrice_NotInitialized_ThrowsException()
        {
            var subject = new DemoBroker(Mock.Of<IExchange>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => { await subject.CheckPriceAsync(); });

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
            var sample = new Sample() { Value = 1.25M };
            var subject = new DemoBroker(Mock.Of<IExchange>());

            var result = subject.GetTotalValue(sample);

            Assert.AreEqual(22.50M, result);
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
                .ReturnsAsync(new Sample { Value = 1.000M, DateTime = now })
                .ReturnsAsync(new Sample { Value = 1.000M, DateTime = now + TimeSpan.FromMinutes(9) })
                .ReturnsAsync(new Sample { Value = 1.000M, DateTime = now + TimeSpan.FromMinutes(10) });

            var subject = new DemoBroker(socketMock.Object);

            subject.InitializeAsync(Assets.DOGE, Assets.DOGE).Wait();

            socketMock.Reset();
            return subject;
        }
    }
}
