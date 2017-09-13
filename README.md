# LinuxSerialPort
A managed Linux Serial Port implementation targeting netstandard2.0.

## About

This SerialPort class is intended to offer similar functionality to the SerailPort class provided by Microsoft in System.IO.Ports. However, although much of the interface is the same or similar, it is not intended to be a direct drop in replacement.

Most basic functionality is covered, including the configuration of the BaudRate, DataBits, StopBits, Handshake, and Parity.

Notably, the Read() and Write() methods are absent from the SerialPort class itself (as an aside, these are problematic in the Microsoft implementation anyway). Instead, the BaseStream property provides the underlying Stream used to read and write from the serial port.

SerialPort.EnableRawMode must be set to True in order to disable Linux TTY behaviour. This will be the desired behaviour for most people and allow the reading/writing of raw bytes to the serial port without the interference of the kernel TTY layer.

SerialPort.MinimumBytesToRead and SerialPort.ReadTimeout allow the blocking behaviour of BaseStream.Read() to be modified.

SerialPort.MinimumBytesToRead corresponds to the stty min parameter. MinimumBytesToRead specifies the minimum number of bytes to be read before BaseStream.Read() will return. BaseStream.Read() will only return after MinimumBytesToRead bytes have been read, or the read has timed out. Setting MinimumBytesToRead to 0 will cause Read() to never block, and instantly return whatever bytes are available in the buffer, even if the buffer is empty.

SerialPort.ReadTimeout corresponds to the stty time parameter. ReadTimeout specifies the number of millseconds a Read() will block for, before it times out. After it times out, it will return whatever data has been read, which may be zero bytes. Due to stty constrains, the timespan will be rounded to the nearest tenth of a second. A ReadTimeout of 0 specifies an infinite timeout. When an infinite timeout is set, the Read() will only return after MinimumBytesToRead bytes have been read.

## Implementation

The implementation works by opening a FileStream to a serial port tty device, usually represented as a file within the /dev directory on a Linux system. For example, /dev/ttyUSB0 is a common path for USB serial adaptors. The FileStream opened is returned as the SerialPort.BaseStream property.

Unlike the Mono implementation of SerialPort, there is no native component for calling into the various kernel serial port APIs. Instead, the /bin/stty binary is called directly with the appropriate parameters to set up the serial device. This is a portable solution that works across the majority of Unix systems.

Another key difference to the Microsoft and Mono SerialPort implementation is the way this implemenatation applies settings. This implementation will not change any parameters of the serial port device unless the property for that setting has been set. If the serial port is opened and none of the properties (BaudRate, Parity, etc) have been set, the TTY will be opened with its existing settings. The properties can be set both before or after the serial port is opened.

Additionally, because this implementation does not forcefully change serial port settings, and is fundamentally operating on top of a TTY device, the TTY device will behave as a Unix terminal device unless explicity placed into raw mode with all echo behaviours disabled. Therefore, the SerialPort.EnableRawMode property must be set to True to read and write raw bytes without the interference of the kernel TTY layer. This will be the desired behaviour for most scenarios.

## Contributing

There are still quite a few things missing from this implemenation that can be implemented using stty, and would be nice to have. Feel free to chuck me a pull request if your heart desires!
