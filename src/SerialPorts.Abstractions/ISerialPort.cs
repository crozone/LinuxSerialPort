using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.SerialPorts.Abstractions
{
    public interface ISerialPort
    {
        Stream BaseStream { get; }
        int BaudRate { get; set; }
        int DataBits { get; set; }
        Handshake Handshake { get; set; }
        bool IsOpen { get; }
        Parity Parity { get; set; }
        string PortName { get; }
        int ReadTimeout { get; set; }
        StopBits StopBits { get; set; }

        void Close();
        void DiscardInBuffer();
        Task DiscardInBufferAsync(CancellationToken token);
        void DiscardOutBuffer();
        Task DiscardOutBufferAsync(CancellationToken token);
        void Dispose();
        void Open();
        string ToString();
    }
}