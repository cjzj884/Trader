using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Trader
{
    class Program
    {
        static void Main(string[] args)
        {
            Run().Wait();
        }

        private static async Task Run()
        {
            using (var ws = new ClientWebSocket())
            {
                await ws.ConnectAsync(new Uri("wss://ws-feed.gdax.com"), CancellationToken.None);
                dynamic subscribeMessage = new ExpandoObject();
                subscribeMessage.type = "subscribe";
                subscribeMessage.product_ids = new List<string>() { "ETH-USD" };
                subscribeMessage.channels = new List<string>() { "ticker" };
                string subscribeMessageString = JsonConvert.SerializeObject(subscribeMessage);

                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(subscribeMessageString)), WebSocketMessageType.Text, true, CancellationToken.None);

                while (true)
                {
                    var buffer = new ArraySegment<byte>(new byte[1024]);
                    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);

                    dynamic message = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(buffer.Array, 0, result.Count));
                    if (message.type == "ticker")
                    {
                        Console.WriteLine($"ETH/USD Price: {message.price}");
                    }
                    else
                    {
                        Console.WriteLine("Got unknown message:");
                        Console.WriteLine(message);
                    }
                }
            }
        }
    }
}
