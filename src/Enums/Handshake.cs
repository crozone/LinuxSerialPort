using System;
using System.Collections.Generic;
using System.Text;

namespace crozone.LinuxSerialPort
{
    public enum Handshake
    {
        None,
        XOnXOff,
        RequestToSend,
        RequestToSendXOnXOff
    }
}
