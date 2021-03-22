# crozone.SerialPorts

[![license](https://img.shields.io/github/license/mashape/apistatus.svg?maxAge=2592000)]()

A cross platform serial port solution for netstandard2.0.

### Obsolescence and support

This library was written for .NET Standard 2.0, before .NET 5's built in `SerialPort` implementation was supported cross-platform on Linux and MacOS.

If you are simply looking for a cross-platform serial port implementation for .NET, using the implementation included with .NET 5 will almost certainly suit your needs better than this library.

However, if you are stuck on .NET 2.0 to .NET 3.1, or are already using this library, I am committed to fixing any major bugs going forward. There will be no new major releases, however.

The source code may also be helpful if you need to write your own code based on the `stty` binary.

## crozone.SerialPorts.Abstractions

Provides abstractions for supporting all serial port implementations, primary the `ISerialPort` interface.

## crozone.SerialPorts.LinuxSerialPort

A managed Linux Serial Port implementation targeting netstandard2.0.

This implementation performs all serial port setup by calling the `/bin/stty` binary, rather than relying on any native interop directly. This means it should work on any system that has a POSIX compatible /bin/stty binary.

See the [README](src/SerialPorts.LinuxSerialPort/README.md) for more details.

## crozone.SerialPorts.WindowsSerialPort

A wrapper for the standard `System.IO.Ports.SerialPort` implementation that makes it compatible with the `ISerialPort` interface. This makes cross-platform development between Linux and Windows easier, because code can be mostly written against the `ISerialPort` interface and not a specific implementation.

See the [README](src/SerialPorts.WindowsSerialPort/README.md) for more details.

## crozone.SerialPorts.ExampleConsoleApp

Contains cross-platform example code in a basic dotnet core console app.

