# crozone.SerialPorts

[![license](https://img.shields.io/github/license/mashape/apistatus.svg?maxAge=2592000)]()

A cross platform serial port solution for netstandard2.0.



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

