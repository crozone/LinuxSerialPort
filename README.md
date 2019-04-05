# LinuxSerialPort

[![NuGet](https://img.shields.io/badge/nuget-1.2.0-green.svg)](https://www.nuget.org/packages/crozone.LinuxSerialPort/)
[![license](https://img.shields.io/github/license/mashape/apistatus.svg?maxAge=2592000)]()

A managed Linux Serial Port implementation targeting netstandard2.0.
Works on systems that have a POSIX compatible /bin/stty binary.

## About

This SerialPort class is intended to offer similar functionality to the SerialPort class provided by Microsoft in System.IO.Ports. However, although much of the interface is the same or similar, it is not intended to be a direct drop in replacement.

Most basic functionality is covered, including the configuration of the BaudRate, DataBits, StopBits, Handshake, and Parity.

## Properties

The `SerialPort.BaseStream` property provides the underlying Stream used to read and write from the serial port. It is a direct filestream opened on the serial port file. The `SerialPort.Read()` and `SerialPort.Write()` methods are absent from the SerialPort class itself (as an aside, these are problematic in the Microsoft implementation anyway). The `SerialPort.DataReceived` event is also absent (and it is also unreliable in the Microsoft implementation, and difficult to implement correctly).

`SerialPort.EnableRawMode` must be set to true in order to disable Linux TTY behaviour, and since v1.1.0 is set to true by default. This will be the desired behaviour for most people and allow the reading/writing of raw bytes to the serial port without the interference of the kernel TTY layer.

`SerialPort.MinimumBytesToRead` and `SerialPort.ReadTimeout` allow the blocking behaviour of `BaseStream.Read()` to be modified.

`SerialPort.MinimumBytesToRead` corresponds to the stty min parameter. MinimumBytesToRead specifies the minimum number of bytes to be read before `BaseStream.Read()` will return. `BaseStream.Read()` will only return after `MinimumBytesToRead` bytes have been read, or the read has timed out. Setting `MinimumBytesToRead` to 0 will cause `BaseStream.Read()` to never block, and instantly return whatever bytes are available in the buffer, even if the buffer is empty.

`SerialPort.ReadTimeout` corresponds to the stty time parameter. `ReadTimeout` specifies the number of milliseconds a `BaseStream.Read()` will block for, before it times out. After it times out, it will return whatever data has been read, which may be zero bytes. Due to stty constraints, the time-span will be rounded to the nearest tenth of a second. A ReadTimeout of 0 specifies an infinite timeout. When an infinite timeout is set, the `BaseStream.Read()` will only return after `MinimumBytesToRead` bytes have been read.

`SerialPort.EnableDrain` controls the use of the stty [-]drain setting. When drain is enabled, stty will attempt to flush the serial port write buffer before applying any configuration to the serial port.
This is problematic if the serial port happens to have data in the write buffer and flow control enabled, since it may never flush, causing stty to hang indefinitely.
Therefore, if [-]drain is available in your version of stty, it is always recommended to set `SerialPort.EnableDrain = false`. [-]drain is not a POSIX compatible command, and older/different versions of stty may not have it available, so be sure to check before setting this. All versions of stty use drain enabled behaviour by default, including the stty versions that to not have the option available.

## Example Code

The following example opens a serial port, and then enters an infinite send/receive loop.

```
// Open the first serial port that matches /dev/ttyUSB* (eg, /dev/ttyUSB0).
//
using (LinuxSerialPort serialPort = new LinuxSerialPort("/dev/ttyUSB*")
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
    ReadTimeout = 0,

    // Set standard serial options
    //
    BaudRate = 9600,
    DataBits = 8,
    Parity = Parity.None,
    StopBits = StopBits.One,
    Handshake = Handshake.None
})

{
    // Open the serial port now.
    //
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
            bytesReceived = stream.Read(
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
            if (bytesRead <= 0)
            {
                break;
            }
            
            // Check if we have run out of read buffer.
            //
            if(totalBytesRead >= readBuffer.Length) {
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
    
    //
    // Serial port is automatically closed here when Dispose() is called by the using statement.
    //
}
```

## Implementation

The implementation works by opening a FileStream to a serial port TTY device, and then setting the serial port TTY parameters with stty. Usually the TTY device is represented as a file within the /dev/ directory on a Unix system. For example, /dev/ttyUSB0 is a common path for USB serial adaptors, and /dev/serial0 is common for physical serial ports.

Once the serial port stream has been opened, read and write capabilities are provided by the `SerialPort.BaseStream` property. This property is a direct filestream to the serial port device.

Unlike the mono implementation of SerialPort, there is no native component for calling into the various kernel serial port APIs. Instead, the /bin/stty binary is called directly with the appropriate parameters to set up the serial device. This is a portable solution that works across the majority of Unix systems.

This implementation will not change parameters of the serial port device unless the property for that setting has been set. However, some TTY behaviour is set when the serial port is opened. Specifically, with `EnableRawMode = true` (default), the TTY is set to "sane" and then "raw", and several other options are set in an attempt to disable as much of the kernel TTY layer as possible.

Other stty parameters will not be changed unless the respective properties have been set. For example, the values for Baudrate, DataBits, StopBits, Handshake, and Parity will remain unchanged when the serial port is first opened, unless  the corresponding properties were set before the port was opened, or set after the port was opened.

## Contributing

There are still quite a few things missing from this implementation that can be implemented using stty, and would be nice to have. Feel free to chuck me a pull request if you have a useful feature idea.
