using System;
using System.Threading.Tasks;

namespace Trader.Networking
{
    public interface IWebSocket : IDisposable
    {
        Task Connect(string url);

        Task SendMessage(string payload);

        Task<string> ReceiveMessage();
    }
}
