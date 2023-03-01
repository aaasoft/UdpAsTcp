using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UdpAsTcp
{
    public class UdpAsTcpClient : IDisposable
    {
        private CancellationTokenSource cts;
        private UdpAsTcpListener listener;

        public UdpClient Client { get; private set; }
        public bool Connected { get; private set; }
        public IPEndPoint LocalEndPoint
        {
            get
            {
                if (listener == null)
                    return (IPEndPoint)Client.Client.LocalEndPoint;
                return listener.LocalEndPoint;
            }
        }
        public IPEndPoint RemoteEndPoint { get; private set; }
        internal Exception LastException { get; private set; }

        public int SendTimeout { get; set; } = 0;
        public int ReceiveTimeout { get; set; } = 0;
        private UdpAsTcpNetworkStream networkStream;

        internal UdpAsTcpClient(UdpAsTcpListener listener, UdpClient client, IPEndPoint remoteEP)
        {
            this.listener = listener;
            Client = client;
            RemoteEndPoint = remoteEP;
        }

        public UdpAsTcpClient()
        {
            Client = new UdpClient();
        }

        public UdpAsTcpClient(int port)
        {
            Client = new UdpClient(port);
        }

        public UdpAsTcpClient(IPEndPoint localEP)
        {
            Client = new UdpClient(localEP);
        }

        public void Connect(IPEndPoint remoteEP)
        {
            RemoteEndPoint = remoteEP;
            Client.Connect(remoteEP);
            cts?.Cancel();
            cts = new CancellationTokenSource();
            _ = beginRecv(cts.Token);
        }

        public void Connect(IPAddress address, int port)
        {
            Connect(new IPEndPoint(address, port));
        }

        public void Connect(string hostname, int port)
        {
            Connect(Dns.GetHostAddresses(hostname).First(), port);
        }

        public void Close()
        {
            cts?.Cancel();
            cts = null;
            networkStream = null;

            if (listener != null)
                return;
            Client.Close();
        }

        public void Dispose()
        {
            if (listener != null)
                return;
            Client.Dispose();
        }

        internal void HandleBuffer(byte[] buffer)
        {
            GetStream().HandleBuffer(buffer);
        }

        private async Task beginRecv(CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested)
                    return;
                var ret = await Client.ReceiveAsync(token);
                if (ret.RemoteEndPoint.Equals(RemoteEndPoint))
                    HandleBuffer(ret.Buffer);
                _ = beginRecv(token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        internal void OnError(Exception ex)
        {
            LastException = ex;
            Connected = false;
            if (listener != null)
                listener.OnClientDisconnected(this);
        }

        public UdpAsTcpNetworkStream GetStream()
        {
            if (networkStream == null)
                networkStream = new UdpAsTcpNetworkStream(this);
            return networkStream;
        }

        internal void Send(byte[] buffer)
        {
            Send(buffer, buffer.Length);
        }

        internal void Send(byte[] buffer, int count)
        {
            if (Client.Client.RemoteEndPoint == null)
                Client.Send(buffer, count, RemoteEndPoint);
            else
                Client.Send(buffer, count);
        }
    }
}
