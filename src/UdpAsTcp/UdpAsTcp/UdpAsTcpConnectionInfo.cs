using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace UdpAsTcp
{
    public class UdpAsTcpConnectionInfo
    {
        public IPEndPoint RemoteIPEndPoint { get; set; }
        public UdpAsTcpClient Client { get; set; }
        public Exception Exception { get; set; }
    }
}
