using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using Trader.Broker;
using Trader.Exchange;
using Trader.Reporter;

namespace Trader.Tests
{
    [TestClass]
    public class TraderTests
    {
        #region Ctor
        [TestMethod]
        public void Ctor_ConfigIsNull_ThrowsException()
        {
            var e = Expect.Throw<ArgumentNullException>(() => {
                new Trader(null, Mock.Of<IBroker>(), Mock.Of<IReporter>());
            });

            Assert.AreEqual("config", e.ParamName);
        }

        [TestMethod]
        public void Ctor_BrokerIsNull_ThrowsException()
        {
            var e = Expect.Throw<ArgumentNullException>(() => {
                new Trader(new Config(false), null, Mock.Of<IReporter>());
            });

            Assert.AreEqual("broker", e.ParamName);
        }

        [TestMethod]
        public void Ctor_ReporterIsNull_ThrowsException()
        {
            var e = Expect.Throw<ArgumentNullException>(() => {
                new Trader(new Config(false), Mock.Of<IBroker>(), null);
            });

            Assert.AreEqual("reporter", e.ParamName);
        }

        #endregion

        #region Initialize

        [TestMethod]
        public void Initialize_InitializesBroker_AndReportsIt()
        {
            var config = new Config(false) { Asset1 = Assets.DOGE, Asset2 = Assets.DOGE };
            var brokerMock = new Mock<IBroker>();
            brokerMock.Setup(m => m.InitializeAsync(Assets.DOGE, Assets.DOGE)).ReturnsAsync(true);
            var reporterMock = new Mock<IReporter>();
            var subject = new Trader(config, brokerMock.Object, reporterMock.Object);

            subject.Initialize().Wait();

            brokerMock.Verify(m => m.InitializeAsync(Assets.DOGE, Assets.DOGE));
            reporterMock.Verify(m => m.ReportInitial(true));
        }

        #endregion

        #region Trade general

        [TestMethod]
        public void Trade_NotInitialized_ThrowsException()
        {
            var subject = new Trader(new Config(false), Mock.Of<IBroker>(), Mock.Of<IReporter>());

            var e = Expect.ThrowAsync<InvalidOperationException>(async () => {
                await subject.Trade();
            });

            Assert.AreEqual("Trader must be Initialized before it can Trade", e.Message);
        }

        [TestMethod]
        public void Trade_CurrentGreaterThanLastButNoise_UpdatesButNoBuyOrSell()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 1005 };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(true);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false) { NoiseThreshold = 0.01, Asset1 = Assets.BTC, Asset2 = Assets.DOGE };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
        }

        [TestMethod]
        public void Trade_CurrentLessThanLastButNoise_UpdatesButNoBuyOrSell()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 995 };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(true);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false) { NoiseThreshold = 0.01, Asset1 = Assets.BTC, Asset2 = Assets.DOGE };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
        }

        #endregion

        #region Trade bullish

        [TestMethod]
        public void TradeBullish_CurrentGreaterThanHighest_UpdatesAndSetsNewHighButNoBuyOrSell()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 1100 };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(true);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false) {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.MaxValue,
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000 };
            subject.High = new Sample() { Value = 1050 };
            subject.Low = new Sample() { Value = 850 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
            Assert.AreEqual(newPrice, subject.High);
        }

        [TestMethod]
        public void TradeBullish_CurrentGreaterThanNoiseAndLessThanHighestButHigherThanSellThreshold_UpdatesButNoBuyOrSell()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 1131 };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(true);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false)
            {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.MaxValue,
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000 };
            var high = subject.High = new Sample() { Value = 1150 };
            var low = subject.Low = new Sample() { Value = 950 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
            Assert.AreEqual(low, subject.Low);
            Assert.AreEqual(high, subject.High);
        }

        [TestMethod]
        public void TradeBullish_CurrentGreaterThanNoiseAndLessThanSellThreshold_UpdatesAndSells()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 1129 };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(true);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false)
            {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.MaxValue,
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000 };
            var high = subject.High = new Sample() { Value = 1150 };
            var low = subject.Low = new Sample() { Value = 950 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            reporter.Verify(r => r.ReportSell(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(newPrice));
            Assert.IsNull(subject.Low);
            Assert.AreEqual(high, subject.High);
            Assert.AreEqual(newPrice, subject.LastSale);
            Assert.IsFalse(subject.Bullish);
        }

        [TestMethod]
        public void TradeBullish_NoiseButDecayed_UpdatesAndSells()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 1134, DateTime = DateTime.Now };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(true);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false)
            {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.FromDays(1),
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000, DateTime = newPrice.DateTime - TimeSpan.FromHours(12) };
            var high = subject.High = new Sample() { Value = 1150 };
            var low = subject.Low = new Sample() { Value = 950 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            reporter.Verify(r => r.ReportSell(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(newPrice));
            Assert.IsNull(subject.Low);
            Assert.AreEqual(high, subject.High);
            Assert.AreEqual(newPrice, subject.LastSale);
            Assert.IsFalse(subject.Bullish);
        }

        [TestMethod]
        public void TradeBullish_NoiseButDecayedButBelowMinNoise_UpdatesButNoBuyOrSell()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 1141, DateTime = DateTime.Now };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(true);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false)
            {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.FromDays(1),
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000, DateTime = newPrice.DateTime - TimeSpan.FromDays(2) };
            var high = subject.High = new Sample() { Value = 1150 };
            var low = subject.Low = new Sample() { Value = 950 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
            Assert.AreEqual(low, subject.Low);
            Assert.AreEqual(high, subject.High);
        }

        #endregion

        #region Trade bearish

        [TestMethod]
        public void TradeBearish_CurrentLowerThanLowest_UpdatesAndSetsNewLowButNoBuyOrSell()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 840 };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(false);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false)
            {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.MaxValue,
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000 };
            subject.High = new Sample() { Value = 1050 };
            subject.Low = new Sample() { Value = 850 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
            Assert.AreEqual(newPrice, subject.Low);
        }

        [TestMethod]
        public void TradeBearish_CurrentLessThanNoiseAndMoreThanLowestButLowerThanBuyThreshold_UpdatesButNoBuyOrSell()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 969 };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(false);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false)
            {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.MaxValue,
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000 };
            var high = subject.High = new Sample() { Value = 1150 };
            var low = subject.Low = new Sample() { Value = 950 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
            Assert.AreEqual(low, subject.Low);
            Assert.AreEqual(high, subject.High);
        }

        [TestMethod]
        public void TradeBearish_CurrentLessThanNoiseAndGreaterThanBuyThreshold_UpdatesAndBuys()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 971 };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(false);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false)
            {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.MaxValue,
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000 };
            var high = subject.High = new Sample() { Value = 1150 };
            var low = subject.Low = new Sample() { Value = 950 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            reporter.Verify(r => r.ReportBuy(broker.Object, newPrice));
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Buy(newPrice));
            Assert.IsNull(subject.High);
            Assert.AreEqual(low, subject.Low);
            Assert.AreEqual(newPrice, subject.LastSale);
            Assert.IsTrue(subject.Bullish);
        }

        [TestMethod]
        public void TradeBearish_NoiseButDecayed_UpdatesAndBuys()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 966, DateTime = DateTime.Now };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(false);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false)
            {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.FromDays(1),
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000, DateTime = newPrice.DateTime - TimeSpan.FromHours(12) };
            var high = subject.High = new Sample() { Value = 1150 };
            var low = subject.Low = new Sample() { Value = 950 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            reporter.Verify(r => r.ReportBuy(broker.Object, newPrice));
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Buy(newPrice));
            Assert.IsNull(subject.High);
            Assert.AreEqual(low, subject.Low);
            Assert.AreEqual(newPrice, subject.LastSale);
            Assert.IsTrue(subject.Bullish);
        }

        [TestMethod]
        public void TradeBearish_NoiseButDecayedButBelowMinNoise_UpdatesButNoBuyOrSell()
        {
            var reporter = new Mock<IReporter>();
            var newPrice = new Sample() { Value = 959, DateTime = DateTime.Now };
            var broker = new Mock<IBroker>();
            broker.Setup(b => b.InitializeAsync(Assets.BTC, Assets.DOGE)).ReturnsAsync(false);
            broker.Setup(b => b.CheckPriceAsync()).ReturnsAsync(newPrice);

            var config = new Config(false)
            {
                NoiseThreshold = 0.01,
                SwingThreshold = 0.10,
                MinSwingThreshold = 0.05,
                SwingThresholdDecayInterval = TimeSpan.FromDays(1),
                Asset1 = Assets.BTC,
                Asset2 = Assets.DOGE
            };

            var subject = new Trader(config, broker.Object, reporter.Object);
            subject.LastSale = new Sample() { Value = 1000, DateTime = newPrice.DateTime - TimeSpan.FromDays(2) };
            var high = subject.High = new Sample() { Value = 1150 };
            var low = subject.Low = new Sample() { Value = 950 };

            subject.Initialize().Wait();
            subject.Trade().Wait();

            reporter.Verify(r => r.ReportNewPrice(broker.Object, newPrice));
            broker.Verify(b => b.Buy(It.IsAny<Sample>()), Times.Never);
            broker.Verify(b => b.Sell(It.IsAny<Sample>()), Times.Never);
            Assert.AreEqual(low, subject.Low);
            Assert.AreEqual(high, subject.High);
        }

        #endregion
    }
}
