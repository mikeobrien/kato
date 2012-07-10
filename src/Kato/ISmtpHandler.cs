using System.Net.Sockets;

namespace Kato
{
    public interface ISmtpHandler
    {
        void HandleConnection(Socket socket);
    }
}