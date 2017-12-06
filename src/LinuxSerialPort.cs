﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static crozone.LinuxSerialPort.Helpers.SttyParameters;
using static crozone.LinuxSerialPort.Helpers.SttyExecution;

namespace crozone.LinuxSerialPort
{
    /// <summary>
    /// A serial port implementation for POSIX style systems that have /bin/stty available.
    /// </summary>
    public class LinuxSerialPort : IDisposable
    {
        //
        // Constants
        //

        public const int InfiniteTimeout = 0;


        //
        // Private fields
        //

        // The original port path is the path passed into the constructor
        // when the serial port is created. This value may contain wildcards,
        // and never changes.
        //
        private string originalPortPath = null;

        // The port path is the current path of the port.
        // This will usually be equal to originalPortPath, but may change
        // when the port is opened, as any wildcards in the originalPortPath
        // will be resolved to an actual file, and portPath will be set to the
        // actual path of the file opened.
        //
        private string portPath = null;

        // Set to true when the port is disposed.
        // After the port is disposed, it cannot be reopened.
        //
        private bool isDisposed = false;

        // When the port is opened, this gets set to the filestream of the
        // serial port file, and remains until the port is closed or disposed.
        //
        private FileStream internalStream = null;


        // Backing fields for the public serial port properties
        //
        private bool? enableRawMode = null;
        private int? minimumBytesToRead = null;
        private int? readTimeout = null;
        private int? baudRate = null;
        private int? dataBits = null;
        private StopBits? stopBits = null;
        private Handshake? handshake = null;
        private Parity? parity = null;

        //
        // Constructors
        //

        /// <summary>
        /// Creates an instance of SerialPort for accessing a serial port on the system.
        /// </summary>
        /// <param name="port">
        /// The path of the serial port device, for example /dev/ttyUSB0.
        /// Wildcards are accepted, for example /dev/ttyUSB* will open the first port that matches that path.
        /// </param>
        public LinuxSerialPort(string port)
        {
            // Set the original port path to whatever value was passed in.
            //
            originalPortPath = port ?? throw new ArgumentNullException(nameof(port));

            // Also set port path to the original port path.
            // this port path may be changed when the port is actually opened.
            //
            this.portPath = originalPortPath;

            // Check that stty is actually available on this platform before continuing.
            //
            if (!IsPlatformCompatible())
            {
                throw new PlatformNotSupportedException("This serial implementation only works on platforms with stty");
            }

            isDisposed = false;
        }

        //
        // Public Properties
        //

        /// <summary>
        /// True if the serialport has been opened, and the stream is avialable for reading and writing.
        /// </summary>
        public bool IsOpen {
            get {
                return internalStream != null;
            }
        }

        /// <summary>
        /// The path of the opened port.
        /// </summary>
        public string PortName {
            get {
                return portPath;
            }
        }

        /// <summary>
        /// The stream for reading from and writing to the serial port.
        /// </summary>
        public Stream BaseStream {
            get {
                ThrowIfDisposed();
                ThrowIfNotOpen();

                return internalStream;
            }
        }
     
        /// <summary>
        /// Disables as much of the kernel tty layer as possible,
        /// to provide raw serialport like behaviour over the underlying tty.
        /// </summary>
        public bool EnableRawMode {
            get {
                return enableRawMode ?? false;
            }
            set {
                if (IsOpen)
                {
                    // Set the raw mode
                    //
                    SetTtyOnSerial(GetRawModeTtyParam(value));

                    // Only set the backing field after the raw mode was set successfully.
                    //
                    enableRawMode = value;

                    // Since this command is composite and sets multiple parameters,
                    // we must re-commit all other settings back over the top of it once set.
                    //
                    SetAllSerialParams();
                }
                else
                {
                    enableRawMode = value;
                }
            }
        }
        
        /// <summary>
        /// The minimum bytes that must fill the serial read buffer before the Read command
        /// will return. (However, it may still time out and return less than this).
        /// </summary>
        public int MinimumBytesToRead {
            get {
                return minimumBytesToRead ?? 0;
            }
            set {
                if (IsOpen)
                {
                    SetTtyOnSerial(GetMinDataTtyParam(value));
                }
                minimumBytesToRead = value;
            }
        }

        /// <summary>
        /// The maximum amount of time a Read command will block for before returning.
        /// The time is in milliseconds, but is rounded to tenths of a second when passed to stty.
        /// </summary>
        public int ReadTimeout {
            get {
                return readTimeout ?? 0;
            }
            set {
                if (IsOpen)
                {
                    SetTtyOnSerial(GetReadTimeoutTtyParam(value));
                }
                readTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the baud rate of the serial port.
        /// </summary>
        public int BaudRate {
            get {
                return baudRate ?? -1;
            }

            set {
                if (IsOpen)
                {
                    SetTtyOnSerial(GetBaudTtyParam(value));
                }
                baudRate = value;
            }
        }

        /// <summary>
        /// Gets or sets the databits to use for the serial port.
        /// </summary>
        public int DataBits {
            get {
                return dataBits ?? 8;
            }

            set {
                if (IsOpen)
                {
                    SetTtyOnSerial(GetDataBitsTtyParam(value));
                }
                dataBits = value;
            }
        }

        /// <summary>
        /// Gets or sets the stopbits to use for the serial port.
        /// </summary>
        public StopBits StopBits {
            get {
                return stopBits ?? StopBits.One;
            }

            set {
                if (IsOpen)
                {
                    SetTtyOnSerial(GetStopBitsTtyParam(value));
                }
                stopBits = value;
            }
        }

        /// <summary>
        /// Gets or sets the handshake method to use for the serial port.
        /// </summary>
        public Handshake Handshake {
            get {
                return handshake ?? Handshake.None;
            }
            set {
                if (IsOpen)
                {
                    SetTtyOnSerial(GetHandshakeTtyParams(value).ToArray());
                }
                handshake = value;
            }
        }

        /// <summary>
        /// Gets or sets the parity to use for the serial port.
        /// </summary>
        public Parity Parity {
            get {
                return parity ?? Parity.None;
            }
            set {
                if (IsOpen)
                {
                    SetTtyOnSerial(GetParityTtyParams(value).ToArray());
                }
                parity = value;
            }
        }

        //
        // Public Methods
        //

        /// <summary>
        /// Opens the serial port.
        /// If any of the serial port properties have been set, they will be applied
        /// as stty commands to the serial port as it is opened.
        /// </summary>
        public void Open()
        {
            ThrowIfDisposed();

            // Resolve the actual port, since the path given may be a wildcard.
            //
            // Do this by getting all the files that match the given path and search string,
            // order by descending, and get the first path. This will get the first port.
            //
            string portPath = Directory.EnumerateFiles(
                Path.GetDirectoryName(originalPortPath),
                Path.GetFileName(originalPortPath)
                )
                .OrderBy(p => p)
                .FirstOrDefault();

            this.portPath = portPath ?? throw new FileNotFoundException($"No ports match the path {originalPortPath}");

            // Instead of using the linux kernel API to configure the serial port,
            // call stty from the shell.
            //
            // Open the serial port file as a filestream
            //
            internalStream = File.Open(this.portPath, FileMode.Open);

            // Set all settings that were configured before the port was opened
            //
            SetAllSerialParams();
        }

        /// <summary>
        /// Closes the serial port.
        /// The serial port may be re-opened, as long as it is not disposed.
        /// </summary>
        public void Close()
        {
            ThrowIfDisposed();
            internalStream?.Dispose();
            internalStream = null;
        }

        /// <summary>
        /// Disposes the serial port.
        /// Once it has been disposed, it cannot be re-opened.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed) return;
            Close();
            isDisposed = true;
        }

        /// <summary>
        /// Discards the contents of the serial port read buffer.
        /// Note, the current implementation only reads all bytes from the buffer,
        /// which is less than ideal.
        /// </summary>
        public void DiscardInBuffer()
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            byte[] buffer = new byte[128];
            while (internalStream.Read(buffer, 0, buffer.Length) > 0) ;
        }

        /// <summary>
        /// Discards the contents of the serial port read buffer.
        /// Note, the current implementation only reads all bytes from the buffer,
        /// which is less than ideal. This will cause problems if MinimumBytesToRead
        /// is not set to 0.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task DiscardInBufferAsync(CancellationToken token)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            byte[] buffer = new byte[128];
            while (await internalStream.ReadAsync(buffer, 0, buffer.Length, token) > 0) ;
        }

        /// <summary>
        /// Discards the contents of the serial port write buffer.
        /// Note, the current implementation only flushes the stream,
        /// which is less than ideal. This will cause problems if hardware flow control
        /// is enabled.
        /// </summary>
        public void DiscardOutBuffer()
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            internalStream.Flush();
        }

        /// <summary>
        /// Discards the contents of the serial port write buffer.
        /// Note, the current implementation only flushes the stream,
        /// which is less than ideal. This will cause problems if hardware flow control
        /// is enabled.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task DiscardOutBufferAsync(CancellationToken token)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            await internalStream.FlushAsync(token);
        }

        /// <summary>
        /// Returns the serial port name
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return PortName;
        }


        //
        // Private methods
        //

        /// <summary>
        /// Throw an InvalidOperationException if the port is not open.
        /// </summary>
        private void ThrowIfNotOpen()
        {
            if (!IsOpen) throw new InvalidOperationException("Port is not open");
        }

        /// <summary>
        /// Throw an ObjectDisposedException if the port is disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(LinuxSerialPort));
        }

        /// <summary>
        /// Applies stty arguments to the active serial port.
        /// </summary>
        /// <param name="sttyArguments"></param>
        /// <returns></returns>
        private string SetTtyOnSerial(IEnumerable<string> sttyArguments)
        {
            // Append the serial port file argument to the list of provided arguments
            // to make the stty command target the active serial port
            //
            var arguments = GetPortTtyParam(portPath).Concat(sttyArguments);

            // Call stty with the parameters given
            //
            return SetTtyWithParam(arguments);
        }

        /// <summary>
        /// Sets serial "sane".
        /// </summary>
        private void SetSerialSane()
        {
            SetTtyOnSerial(GetSaneModeTtyParam());
        }

        /// <summary>
        /// Sets the stty parameters for all currently set properties on the serial port.
        /// </summary>
        /// <returns></returns>
        private void SetAllSerialParams()
        {
            SetTtyOnSerial(GetAllTtyParams());
        }

        /// <summary>
        /// Gets the stty parameters for all currently set properties on the serial port.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<string> GetAllTtyParams()
        {
            IEnumerable<string> allParams = Enumerable.Empty<string>();

            // Start with sane to reset any previous commands
            //
            allParams = allParams.Concat(GetSaneModeTtyParam());

            //
            // When properties are set for the first time, their backing value transitions
            // from null to the requested value.
            //
            // Return parameters for all property backing values that aren't null, since
            // these have been set.
            //

            if (enableRawMode.HasValue)
            {
                allParams = allParams.Concat(GetRawModeTtyParam(enableRawMode.Value));
            }

            if (baudRate.HasValue)
            {
                allParams = allParams.Concat(GetBaudTtyParam(baudRate.Value));
            }

            if (minimumBytesToRead.HasValue)
            {
                allParams = allParams.Concat(GetMinDataTtyParam(minimumBytesToRead.Value));
            }

            if (readTimeout.HasValue)
            {
                allParams = allParams.Concat(GetReadTimeoutTtyParam(readTimeout.Value));
            }

            if (dataBits.HasValue)
            {
                allParams = allParams.Concat(GetDataBitsTtyParam(dataBits.Value));
            }

            if (stopBits.HasValue)
            {
                allParams = allParams.Concat(GetStopBitsTtyParam(stopBits.Value));
            }

            if (handshake.HasValue)
            {
                allParams = allParams.Concat(GetHandshakeTtyParams(handshake.Value));
            }

            if (parity.HasValue)
            {
                allParams = allParams.Concat(GetParityTtyParams(parity.Value));
            }

            return allParams;
        }
    }
}
