using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdpAsTcp
{
    internal enum UdpAsTcpPackageType : byte
    {
        SYN = 0,
        SYN_ACK = 1,
        DATA = 10,
        DATA_ACK = 11
    }
}
