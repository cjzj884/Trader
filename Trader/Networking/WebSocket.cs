using FluentScheduler;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Trader.Networking
{
    public class WebSocket : IWebSocket
    {
        private ClientWebSocket socket;

        public async Task Connect(string url)
        {
            this.Dispose(); // Is this weird? This feels a little weird

            socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(url), CancellationToken.None);
        }

        public void Dispose()
        {
            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }
        }

        public async Task<string> ReceiveMessage()
        {
            var tokenSource = new CancellationTokenSource();
            var buffer = new ArraySegment<byte>(new byte[1024]);
            var delay = 2;
            tokenSource.CancelAfter(TimeSpan.FromMinutes(delay));
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer, tokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                throw new WebSocketException($"Websocket stopped sending us messages ({delay} minutes of silence)");
            }
            return Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
        }

        public async Task SendMessage(string payload)
        {
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
