using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Trader
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
            var buffer = new ArraySegment<byte>(new byte[1024]);
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            return Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
        }

        public async Task SendMessage(string payload)
        {
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
