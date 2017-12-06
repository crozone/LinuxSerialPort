# LinuxSerialPort

[![NuGet](https://img.shields.io/badge/nuget-1.1.0-green.svg)](https://www.nuget.org/packages/crozone.LinuxSerialPort/)
[![license](https://img.shields.io/github/license/mashape/apistatus.svg?maxAge=2592000)]()

A managed Linux Serial Port implementation targeting netstandard2.0.
Works on systems that have a POSIX compatible /bin/stty binary.

## About

This SerialPort class is intended to offer similar functionality to the SerialPort class provided by Microsoft in System.IO.Ports. However, although much of the interface is the same or similar, it is not intended to be a direct drop in replacement.

Most basic functionality is covered, including the configuration of the BaudRate, DataBits, StopBits, Handshake, and Parity.

Notably, the Read() and Write() methods are absent from the SerialPort class itself (as an aside, these are problematic in the Microsoft implementation anyway). Instead, the BaseStream property provides the underlying Stream used to read and write from the serial port.

SerialPort.EnableRawMode must be set to true in order to disable Linux TTY behaviour, and since v1.1.0 is set to true by default. This will be the desired behaviour for most people and allow the reading/writing of raw bytes to the serial port without the interference of the kernel TTY layer.

SerialPort.MinimumBytesToRead and SerialPort.ReadTimeout allow the blocking behaviour of BaseStream.Read() to be modified.

SerialPort.MinimumBytesToRead corresponds to the stty min parameter. MinimumBytesToRead specifies the minimum number of bytes to be read before BaseStream.Read() will return. BaseStream.Read() will only return after MinimumBytesToRead bytes have been read, or the read has timed out. Setting MinimumBytesToRead to 0 will cause Read() to never block, and instantly return whatever bytes are available in the buffer, even if the buffer is empty.

SerialPort.ReadTimeout corresponds to the stty time parameter. ReadTimeout specifies the number of milliseconds a Read() will block for, before it times out. After it times out, it will return whatever data has been read, which may be zero bytes. Due to stty constraints, the time-span will be rounded to the nearest tenth of a second. A ReadTimeout of 0 specifies an infinite timeout. When an infinite timeout is set, the Read() will only return after MinimumBytesToRead bytes have been read.

## Implementation

The implementation works by opening a FileStream to a serial port TTY device, and then setting the serial port TTY parameters with stty. Usually the TTY device is represented as a file within the /dev directory on a Unix system. For example, /dev/ttyUSB0 is a common path for USB serial adaptors.

Once the serial port stream has been opened, it is provided by the SerialPort.BaseStream property.

Unlike the mono implementation of SerialPort, there is no native component for calling into the various kernel serial port APIs. Instead, the /bin/stty binary is called directly with the appropriate parameters to set up the serial device. This is a portable solution that works across the majority of Unix systems.

This implementation will not change parameters of the serial port device unless the property for that setting has been set. However, some TTY behaviour is set when the serial port is opened - specifically, the TTY is set to "sane" and then "raw" (or "-raw"). Unless the respective properties have been set, the values for Baudrate, DataBits, StopBits, Handshake, and Parity should remain unchanged when the serial port is opened.

## Contributing

There are still quite a few things missing from this implementation that can be implemented using stty, and would be nice to have. Feel free to chuck me a pull request as your heart desires!
