using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxSerialPort
{
    /// <summary>
    /// A serial port implementation for POSIX style systems that have /bin/stty available.
    /// </summary>
    public class LinuxSerialPort : IDisposable
    {
        public const int InfiniteTimeout = 0;

        private const string SttyPath = "/bin/stty";

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
                    SetTtyOnSerial(GetRawModeTtyParam(value));

                    // Only set the backing field after the raw mode was set successfully.
                    enableRawMode = value;

                    // Since this command is composite and sets multiple parameters,
                    // we must re-commit all other settings back over the top of it once set.
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

            // resolve the actual port, since the path given may be a wildcard

            // get the first file that matches the given path and search
            string portPath = Directory.EnumerateFiles(
                Path.GetDirectoryName(basePortPath),
                Path.GetFileName(basePortPath)
                )
                .OrderBy(p => p) // choose the lowest serial port first
                .FirstOrDefault();

            this.port = portPath ?? throw new FileNotFoundException($"No ports match the path {basePortPath}");

            // instead of using the linux kernel API to configure the serial port,
            // use stty from the shell.

            // open the serial port stream
            internalStream = File.Open(port, FileMode.Open);

            // set all settings that were configured before the port was opened.
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

        public void DiscardInBuffer()
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            byte[] buffer = new byte[128];
            while (internalStream.Read(buffer, 0, buffer.Length) > 0) ;
        }

        public async Task DiscardInBufferAsync(CancellationToken token)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            byte[] buffer = new byte[128];
            while (await internalStream.ReadAsync(buffer, 0, buffer.Length, token) > 0) ;
        }

        public void DiscardOutBuffer()
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            internalStream.Flush();
        }

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

        #region Public Static Methods
        public static bool IsPlatformCompatible()
        {
            return File.Exists(SttyPath);
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
            // append the serial port file argument to the list of provided arguments
            // to make the stty command target the active serial port
            var arguments = GetPortTtyParam(port).Concat(sttyArguments);
            // call stty with params
            return SetTtyWithParam(arguments); ;
        }

        /// <summary>
        /// Calls stty with the list of stty arguments
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private string SetTtyWithParam(IEnumerable<string> arguments)
        {
            string argumentsString = string.Join(" ", arguments);
            string result = CallStty(argumentsString);
            return result;
        }

        /// <summary>
        /// Calls the stty command with the parameters given.
        /// </summary>
        /// <param name="sttyParams"></param>
        /// <returns></returns>
        private static string CallStty(string sttyParams)
        {
            Process process = new Process();
            process.StartInfo.FileName = SttyPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.Arguments = sttyParams;
            process.Start();
            // Always call ReadToEnd() before WaitForExit() to avoid a deadlock
            Task<string> readOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> readError = process.StandardError.ReadToEndAsync();
            Task.WaitAll(readOutput, readError);
            string outputString = readOutput.Result;
            string errorString = readError.Result;
            process.WaitForExit();

            // if there was any error printed, throw it
            if (errorString.Trim().Length > 0)
            {
                throw new InvalidOperationException(errorString);
            }

            return outputString;
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

            // start with sane to reset any previous commands
            allParams = allParams.Concat(GetSaneModeTtyParam());

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

        private IEnumerable<string> GetListAllTtyParam()
        {
            yield return "-a";
        }

        private IEnumerable<string> GetPortTtyParam(string port)
        {
            yield return $"-F {port}";
        }

        private IEnumerable<string> GetSaneModeTtyParam()
        {
            // sets cread -ignbrk brkint -inlcr -igncr icrnl -iutf8 -ixoff -iuclc -ixany imaxbel opost
            // -olcuc -ocrnl onlcr -onocr -onlret -ofill -ofdel nl0 cr0 tab0 bs0 vt0 ff0 isig icanon iexten
            // echo echoe echok -echonl -noflsh -xcase -tostop -echoprt echoctl echoke
            // and all special characters to their default values
            yield return "sane";
        }

        private IEnumerable<string> GetRawModeTtyParam(bool rawEnabled)
        {
            if (rawEnabled)
            {
                // sets -ignbrk -brkint -ignpar -parmrk -inpck -istrip -inlcr -igncr -icrnl -ixon -ixoff
                // -iuclc -ixany -imaxbel -opost -isig -icanon -xcase min 1 time 0
                yield return "raw";

                // remove echo and other things that will get in the way of reading raw data how we expect
                yield return "-hupcl"; // don't send a hangup signal when the last process closes the tty
                yield return "-clocal"; // disable modem control signals
                yield return "-iexten"; // don't enable non-POSIX special characters
                yield return "-echo"; // don't echo erase characters as backspace-space-backspace
                yield return "-echoe"; // don't echo erase characters as backspace-space-backspace
                yield return "-echok"; // don't echo a newline after a kill characters
                yield return "-echonl"; // don't echo newline even if not echoing other characters
                yield return "-echoprt"; // don't echo erased characters backward, between '\' and '/'
                yield return "-echoctl"; // don't echo control characters in hat notation ('^c')
                yield return "-echoke"; // kill all line by obeying the echoctl and echok settings
            }
            else
            {
                yield return "-raw";
            }
        }

        private IEnumerable<string> GetBaudTtyParam(int baudRate)
        {
            yield return $"{baudRate}";
        }

        private IEnumerable<string> GetReadTimeoutTtyParam(int readTimeout)
        {
            yield return $"time {(readTimeout + 50) / 100}"; // timeout on each read. Time is in tenths of a second, 1 = 100ms.
        }

        private IEnumerable<string> GetMinDataTtyParam(int byteCount)
        {
            yield return $"min {byteCount}"; // minimum bytes that can be read out of the stream
        }

        private IEnumerable<string> GetHandshakeTtyParams(Handshake handshake)
        {
            switch (handshake)
            {
                case Handshake.None:
                    yield return "-crtscts";
                    yield return "-ixoff";
                    yield return "-ixon";
                    yield break;
                case Handshake.RequestToSend:
                    yield return "crtscts";
                    yield return "-ixoff";
                    yield return "-ixon";
                    yield break;
                case Handshake.XOnXOff:
                    yield return "-crtscts";
                    yield return "ixoff";
                    yield return "ixon";
                    yield break;
                case Handshake.RequestToSendXOnXOff:
                    yield return "crtscts";
                    yield return "ixoff";
                    yield return "ixon";
                    yield break;
                default:
                    throw new InvalidOperationException($"Invalid Handshake {handshake}");
            }
        }

        private IEnumerable<string> GetParityTtyParams(Parity parity)
        {
            switch (parity)
            {
                case Parity.None:
                    yield return "-parenb";
                    yield return "-cmspar";
                    yield break;
                case Parity.Odd:
                    yield return "parenb";
                    yield return "-cmspar";
                    yield return "parodd";
                    yield break;
                case Parity.Even:
                    yield return "parenb";
                    yield return "-cmspar";
                    yield return "-parodd";
                    yield break;
                case Parity.Mark:
                    yield return "-parenb";
                    yield return "cmspar";
                    yield return "parodd";
                    yield break;
                case Parity.Space:
                    yield return "-parenb";
                    yield return "cmspar";
                    yield return "-parodd";
                    yield break;
                default:
                    throw new InvalidOperationException($"Invalid Parity {parity}");
            }
        }

        private IEnumerable<string> GetDataBitsTtyParam(int dataBits)
        {
            if (dataBits < 5 || dataBits > 8)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dataBits),
                    $"{nameof(dataBits)} must be between 5 and 8"
                 );
            }

            yield return $"cs{dataBits}";
        }

        private IEnumerable<string> GetStopBitsTtyParam(StopBits stopBits)
        {
            switch (stopBits)
            {
                case StopBits.None:
                    throw new InvalidOperationException($"Stop bits cannot be set to {StopBits.None}");
                case StopBits.One:
                    yield return "-cstopb";
                    yield break;
                case StopBits.OnePointFive:
                    throw new InvalidOperationException($"Stop bits cannot be set to {StopBits.OnePointFive}");
                case StopBits.Two:
                    yield return "cstopb";
                    yield break;
                default:
                    throw new InvalidOperationException($"Invalid StopBits {stopBits}");
            }
        }
        #endregion
        #endregion
    }

    #region Enums

    public enum Handshake
    {
        None,
        XOnXOff,
        RequestToSend,
        RequestToSendXOnXOff
    }

    public enum Parity
    {
        None,
        Odd,
        Even,
        Mark,
        Space
    }

    public enum StopBits
    {
        None,
        One,
        Two,
        OnePointFive
    }

    #endregion
}
