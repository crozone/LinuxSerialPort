using crozone.SerialPorts.Abstractions;
using crozone.SerialPorts.LinuxSerialPort;
using crozone.SerialPorts.WindowsSerialPort;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SerialPorts.ExampleConsoleApp
{
    public static class Program
    {
        private static readonly string linuxPort = "/dev/ttyUSB*";
        private static readonly string windowsPort = "COM1";

        public static void Main(string[] args)
        {
            using (ISerialPort serialPort = CreateSerialPort())
            {
                // Open the serial port now.
                //
                Console.WriteLine($"Opening port {serialPort.PortName}...");
                serialPort.Open();

                // Get the base stream for reading and writing
                //
                Stream stream = serialPort.BaseStream;

                // Send/Receive loop
                //
                while (true)
                {
                    // Write some data to the port.
                    //
                    string sendMessage = "Hello!";
                    Console.WriteLine($"SEND: {sendMessage}");

                    byte[] writeBytes = Encoding.UTF8.GetBytes(sendMessage);
                    stream.Write(writeBytes, 0, writeBytes.Length);
                    stream.Flush();

                    // Wait for a response.
                    // In this example, we don't know the expected response data length ahead of time,
                    // so we use the non-blocking behaviour of the serial port in a loop
                    // to keep checking for data until no more data is received.
                    //
                    int totalBytesReceived = 0;
                    byte[] readBuffer = new byte[4096];
                    while (true)
                    {
                        // Sleep 10ms while we wait for data to arrive
                        //
                        Thread.Sleep(10);

                        // Read any data from the serial buffer into the latest index in the read buffer
                        //
                        int bytesReceived = stream.Read(
                            readBuffer,
                            totalBytesReceived,
                            readBuffer.Length - totalBytesReceived
                            );

                        // Update our running buffer length
                        //
                        totalBytesReceived += bytesReceived;

                        // If we received no data during the Thread.Sleep, assume we have reached the end
                        // of the packet being sent to us.
                        //
                        if (bytesReceived <= 0)
                        {
                            break;
                        }

                        // Check if we have run out of read buffer.
                        //
                        if (totalBytesReceived >= readBuffer.Length)
                        {
                            // Use what we have.
                            //
                            break;
                        }
                    }

                    // Print what we received
                    //
                    string receivedMessage = Encoding.UTF8.GetString(readBuffer, 0, totalBytesReceived);
                    Console.WriteLine($"RECEIVE: {receivedMessage}");

                    // Sleep for 500ms
                    //
                    Thread.Sleep(500);
                }
            } // Serial port is automatically closed here when Dispose() is called by the using statement.
        }

        private static ISerialPort CreateSerialPort()
        {
            PlatformID pid = Environment.OSVersion.Platform;

            // Create a different serial port type depending on the OS platform
            //
            if (pid == PlatformID.Unix || pid == PlatformID.MacOSX)
            {
                return new LinuxSerialPort(linuxPort)
                {
                    //
                    // Set serial port parameters
                    //

                    // Set drain to false so that stty doesn't attempt to flush the write buffer when configuring the port,
                    // avoiding a potential hang when flow control is enabled.
                    // Set this to null (or remove this line) if your stty version doesn't support the [-]drain option.
                    //
                    EnableDrain = false,

                    // Set the minimum bytes required to trigger a read to 0.
                    // This means that the read will never block indefinitely, even if no data arrives.
                    // Change this to a value > 0 if you want blocking behaviour.
                    //
                    MinimumBytesToRead = 0,

                    // Set the read timeout in ms.
                    // 0 specifies an infinite timeout, so a read will only complete when MinimumBytesToRead have been read.
                    // However, since MinimumBytesToRead is 0, this combination results in reads that do not block.
                    //
                    ReadTimeout = LinuxSerialPort.InfiniteTimeout,

                    // Set standard serial options
                    //
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None
                };
            }
            else if (pid == PlatformID.Win32NT
                    || pid == PlatformID.Win32Windows
                    || pid == PlatformID.Win32S)
            {
                return new WindowsSerialPort(new System.IO.Ports.SerialPort(windowsPort))
                {
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = WindowsSerialPort.InfiniteTimeout
                };
            }
            else
            {
                // We don't know what platform we're on, fallback to the Windows implementation
                //
                return new WindowsSerialPort(new System.IO.Ports.SerialPort(windowsPort))
                {
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = WindowsSerialPort.InfiniteTimeout
                };
            }
        }
    }
}
