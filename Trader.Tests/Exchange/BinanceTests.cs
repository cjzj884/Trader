﻿using System;
using System.Net.WebSockets;
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
                var subject = new Binance(null, Mock.Of<ITime>());
            });

            Assert.AreEqual("websocket", e.ParamName);
        }

        [TestMethod]
        public void Ctor_TimeNull_ThrowsException()
        {
            var e = Expect.Throw<ArgumentNullException>(() => {
                var subject = new Binance(Mock.Of<IWebSocket>(), null);
            });

            Assert.AreEqual("time", e.ParamName);
        }

        [TestMethod]
        public void Ctor_ArgsOK_MakerFeeIs0003()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            Assert.AreEqual(0.001, subject.TakerFeeRate);
        }

        #endregion

        #region Initialize
        
        [TestMethod]
        public void Initialize_ValidPair_OpensSocketAndSubscribes()
        {
            var socketMock = new Mock<IWebSocket>();
            var subject = new Binance(socketMock.Object, Mock.Of<ITime>());

            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            socketMock.Verify(m => m.Connect("wss://stream.binance.com:9443/ws/btcusdt@ticker"));
        }

        [TestMethod]
        public void Initialize_InvalidPair_ThrowsException()
        {
            var socketMock = new Mock<IWebSocket>();
            var subject = new Binance(socketMock.Object, Mock.Of<ITime>());

            var e = Expect.ThrowAsync<ArgumentException>(async () => {
                await subject.Initialize(Assets.BTC, Assets.DOGE);
            });
            
            Assert.AreEqual("Trading pair BTC/DOGE is not available on Binance", e.Message);
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

            var subject = new Binance(socketMock.Object, timeMock.Object);
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            socketMock.SetupSequence(m => m.ReceiveMessage())
                .ThrowsAsync(new WebSocketException("Shit's broken yo"))
                .ReturnsAsync(message);
            timeMock.Setup(m => m.Now).Returns(time);
            
            var result = subject.GetCurrentPrice().Result;

            Assert.AreEqual(time, result.DateTime);
            Assert.AreEqual(1.25, result.Value);
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

            var subject = new Binance(socketMock.Object, timeMock.Object);
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            socketMock.SetupSequence(m => m.ReceiveMessage())
                .ReturnsAsync(dummyMessage)
                .ReturnsAsync(message);
            timeMock.Setup(m => m.Now).Returns(time);

            var result = subject.GetCurrentPrice().Result;

            Assert.AreEqual(time, result.DateTime);
            Assert.AreEqual(1.25, result.Value);
        }

        [TestMethod]
        public void GetCurrentPrice_NotInitialized_ThrowsException()
        {
            var subject = new Binance(Mock.Of<IWebSocket>(), Mock.Of<ITime>());

            var exception = Expect.ThrowAsync<InvalidOperationException>(async () => {
                await subject.GetCurrentPrice();
            });

            Assert.AreEqual("Binance cannot GetCurrentPrice until Initialized!", exception.Message);
        }

        #endregion

        #region Dispose

        [TestMethod]
        public void Dispose_NotInitialized_CantDoStuff()
        {
            var socketMock = new Mock<IWebSocket>();
            var subject = new Binance(socketMock.Object, Mock.Of<ITime>());

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
            var subject = new Binance(socketMock.Object, Mock.Of<ITime>());
            subject.Initialize(Assets.BTC, Assets.USDT).Wait();

            subject.Dispose();

            var e = Expect.ThrowAsync<InvalidOperationException>(async () => {
                await subject.GetCurrentPrice();
            });

            Assert.AreEqual("Binance cannot GetCurrentPrice until Initialized!", e.Message);
            socketMock.Verify(m => m.Dispose());
        }

        #endregion
    }
}