using System;
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
        public const int InfiniteTimeout = 0;

        private string basePortPath = null;
        private string port = null;
        private bool isDisposed = false;
        private FileStream internalStream = null;

        private bool? enableRawMode = null;
        private int? minimumBytesToRead = null;
        private int? readTimeout = null;
        private int? baudRate = null;
        private int? dataBits = null;
        private StopBits? stopBits = null;
        private Handshake? handshake = null;
        private Parity? parity = null;

        /// <summary>
        /// Creates an instance of SerialPort for accessing a serial port on the system.
        /// </summary>
        /// <param name="port">
        /// The path of the serial port device, for example /dev/ttyUSB0.
        /// Wildcards are accepted, for example /dev/ttyUSB* will open the first port that matches that path.
        /// </param>
        public LinuxSerialPort(string port)
        {
            basePortPath = port ?? throw new ArgumentNullException(nameof(port));
            this.port = basePortPath;
            if (!IsPlatformCompatible()) throw new PlatformNotSupportedException("This serial implementation only works on platforms with stty");

            isDisposed = false;
        }

        #region Properties
        public bool IsOpen {
            get {
                return internalStream != null;
            }
        }

        public string PortName {
            get {
                return port;
            }
        }

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

        #endregion

        #region Public Methods

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
                Path.GetDirectoryName(basePortPath),
                Path.GetFileName(basePortPath)
                )
                .OrderBy(p => p)
                .FirstOrDefault();

            this.port = portPath ?? throw new FileNotFoundException($"No ports match the path {basePortPath}");

            // Instead of using the linux kernel API to configure the serial port,
            // call stty from the shell.
            //
            // Open the serial port file as a filestream
            //
            internalStream = File.Open(port, FileMode.Open);

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

        #endregion

        #region Private Methods

        private void ThrowIfNotOpen()
        {
            if (!IsOpen) throw new InvalidOperationException("Port is not open");
        }

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
            var arguments = GetPortTtyParam(port).Concat(sttyArguments);

            // Call stty with the parameters given
            //
            return SetTtyWithParam(arguments);
        }

        #region stty Commands
        private void SetSerialSane()
        {
            SetTtyOnSerial(GetSaneModeTtyParam());
        }

        private void SetAllSerialParams()
        {
            SetTtyOnSerial(GetAllTtyParams());
        }

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

        #endregion
        #endregion
    }
}
