using System;
using System.Threading.Tasks;

namespace Trader
{
    public interface IWebSocket : IDisposable
    {
        Task Connect(string url);

        Task SendMessage(string payload);

        Task<string> ReceiveMessage();
    }
}
