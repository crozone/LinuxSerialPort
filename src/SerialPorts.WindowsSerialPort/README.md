# crozone.SerialPorts.WindowsSerialPort

[![NuGet](https://img.shields.io/badge/nuget-2.0.2-green.svg)](https://www.nuget.org/packages/crozone.SerialPorts.WindowsSerialPort/)
[![license](https://img.shields.io/github/license/mashape/apistatus.svg?maxAge=2592000)]()

A wrapper for the Microsoft `System.IO.Ports.SerialPort` implementation that makes it compatible with the `ISerialPort`  interface. This makes cross-platform development between Linux and  Windows easier, because code can be mostly written against the `ISerialPort` interface and not a specific implementation.

