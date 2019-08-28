using crozone.SerialPorts.Abstractions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Ports;

using Handshake = crozone.SerialPorts.Abstractions.Handshake;
using Parity = crozone.SerialPorts.Abstractions.Parity;
using StopBits = crozone.SerialPorts.Abstractions.StopBits;

namespace crozone.SerialPorts.WindowsSerialPort
{
    /// <summary>
    /// Wrapper for System.IO.Ports.SerialPort to interface it to the ISerialPort interface
    /// </summary>
    public class WindowsSerialPort : ISerialPort
    {
        /// <summary>
        /// The value representing an infinite timout on the serial port.
        /// </summary>
        public const int InfiniteTimeout = SerialPort.InfiniteTimeout;

        private SerialPort serialPort;

        public WindowsSerialPort(SerialPort serialPort)
        {
            this.serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
        }

        public Stream BaseStream => serialPort.BaseStream;

        public int BaudRate { get => serialPort.BaudRate; set => serialPort.BaudRate = value; }
        public int DataBits { get => serialPort.DataBits; set => serialPort.DataBits = value; }
        public Handshake Handshake { get => (Handshake)serialPort.Handshake; set => serialPort.Handshake = (System.IO.Ports.Handshake)value; }

        public bool IsOpen => serialPort.IsOpen;

        public Parity Parity { get => (Parity)serialPort.Parity; set => serialPort.Parity = (System.IO.Ports.Parity)serialPort.Parity; }

        public string PortName => serialPort.PortName;

        public int ReadTimeout { get => serialPort.ReadTimeout; set => serialPort.ReadTimeout = value; }
        public StopBits StopBits { get => (StopBits)serialPort.StopBits; set => serialPort.StopBits = (System.IO.Ports.StopBits)value; }

        public void Close()
        {
            serialPort.Close();
        }

        public void DiscardInBuffer()
        {
            serialPort.DiscardInBuffer();
        }

        public Task DiscardInBufferAsync(CancellationToken token)
        {
            serialPort.DiscardInBuffer();
            return Task.CompletedTask;
        }

        public void DiscardOutBuffer()
        {
            serialPort.DiscardOutBuffer();
        }

        public Task DiscardOutBufferAsync(CancellationToken token)
        {
            serialPort.DiscardOutBuffer();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            serialPort.Dispose();
        }

        public void Open()
        {
            serialPort.Open();
        }
    }
}
