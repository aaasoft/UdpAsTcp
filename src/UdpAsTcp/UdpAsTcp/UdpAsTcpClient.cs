using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace UdpAsTcp
{
    public class UdpAsTcpClient
    {
        private UdpAsTcpListener listener;
        public bool Connected { get; private set; }
        public IPEndPoint RemoteEndPoint { get; private set; }
        private Exception lastException;

        public UdpAsTcpClient(UdpAsTcpListener listener, IPEndPoint remoteEP)
        {
            this.listener = listener;
            RemoteEndPoint = remoteEP;
        }

        internal void HandleBuffer(byte[] buffer)
        {

        }

        internal void OnError(Exception ex)
        {
            lastException = ex;
            Connected = false;
            if (listener != null)
                listener.OnClientDisconnected(this);
        }
    }
}
