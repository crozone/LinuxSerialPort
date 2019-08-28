# crozone.SerialPorts.WindowsSerialPort

A wrapper for the Microsoft `System.IO.Ports.SerialPort` implementation that makes it compatible with the `ISerialPort`  interface. This makes cross-platform development between Linux and  Windows easier, because code can be mostly written against the `ISerialPort` interface and not a specific implementation.