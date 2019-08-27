using crozone.SerialPorts.Abstractions;
using System;
using System.Collections.Generic;

namespace crozone.SerialPorts.LinuxSerialPort.Helpers
{
    internal static class SttyParameters
    {
        public static IEnumerable<string> GetListAllTtyParam()
        {
            yield return "-a";
        }

        public static IEnumerable<string> GetPortTtyParam(string port)
        {
            yield return $"-F {port}";
        }

        public static IEnumerable<string> GetSaneModeTtyParam()
        {
            // sane is a composite command that sets:
            //
            // cread -ignbrk brkint -inlcr -igncr icrnl -iutf8 -ixoff -iuclc -ixany imaxbel opost
            // -olcuc -ocrnl onlcr -onocr -onlret -ofill -ofdel nl0 cr0 tab0 bs0 vt0 ff0 isig icanon iexten
            // echo echoe echok -echonl -noflsh -xcase -tostop -echoprt echoctl echoke
            //
            // as well as "special characters" to their default values
            //
            yield return "sane";
        }

        public static IEnumerable<string> GetRawModeTtyParam(bool rawEnabled)
        {
            if (rawEnabled)
            {
                // raw is a composite command that sets:
                //
                // -ignbrk -brkint -ignpar -parmrk -inpck -istrip -inlcr -igncr -icrnl -ixon -ixoff
                // -iuclc -ixany -imaxbel -opost -isig -icanon -xcase min 1 time 0
                //
                yield return "raw";

                // Unfortunately, the raw parameter on its own doesn't set enough parameters to
                // actually get the tty to anywhere near a true byte in, byte out raw serial socket.
                //
                // Remove echo and other things that will get in the way of reading raw data how we expect.
                //

                // Don't send a hangup signal when the last process closes the tty
                //
                yield return "-hupcl";

                // Disable modem control signals
                //
                yield return "-clocal";

                // Don't enable non-POSIX special characters
                //
                yield return "-iexten";

                // Don't echo erase characters as backspace-space-backspace
                //
                yield return "-echo";

                // Don't echo erase characters as backspace-space-backspace
                //
                yield return "-echoe";

                // Don't echo a newline after a kill characters
                //
                yield return "-echok";

                // Don't echo newline even if not echoing other characters
                //
                yield return "-echonl";

                // Don't echo erased characters backward, between '\' and '/'
                //
                yield return "-echoprt";

                // Don't echo control characters in hat notation ('^c')
                //
                yield return "-echoctl";

                // Kill all line by obeying the echoctl and echok settings
                //
                yield return "-echoke";
            }
            else
            {
                yield return "-raw";
            }
        }

        public static IEnumerable<string> GetDrainTtyParam(bool drainEnabled)
        {
            if (drainEnabled)
            {
                yield return "drain";
            }
            else
            {
                yield return "-drain";
            }
        }

        public static IEnumerable<string> GetBaudTtyParam(int baudRate)
        {
            yield return $"{baudRate}";
        }

        public static IEnumerable<string> GetReadTimeoutTtyParam(int readTimeout)
        {
            yield return $"time {(readTimeout + 50) / 100}"; // timeout on each read. Time is in tenths of a second, 1 = 100ms.
        }

        public static IEnumerable<string> GetMinDataTtyParam(int byteCount)
        {
            yield return $"min {byteCount}"; // minimum bytes that can be read out of the stream
        }

        public static IEnumerable<string> GetHandshakeTtyParams(Handshake handshake)
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

        public static IEnumerable<string> GetParityTtyParams(Parity parity)
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

        public static IEnumerable<string> GetDataBitsTtyParam(int dataBits)
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

        public static IEnumerable<string> GetStopBitsTtyParam(StopBits stopBits)
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
    }
}
