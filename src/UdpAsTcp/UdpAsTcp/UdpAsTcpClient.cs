using System.Net;
using System.Net.Sockets;
using UdpAsTcp.Utils;

namespace UdpAsTcp
{
    public class UdpAsTcpClient : IDisposable
    {
        private CancellationTokenSource cts;
        private UdpAsTcpListener listener;

        public UdpClient Client { get; private set; }
        public bool Connected { get; private set; } = false;
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
        private bool isWaitingForSynAndAck = true;

        public int SendTimeout { get; set; } = 0;
        public int ReceiveTimeout { get; set; } = 0;
        public int DataAckRetryTimes { get; set; } = 10;
        public int DataAckRetryInterval { get; set; } = 100;

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
            clientSynAndAck();
        }

        internal void serverSynAndAck()
        {
            try
            {
                var stream = GetStream();

                var buffer = new byte[1024];
                //等待客户端SYN
                IPEndPoint remoteEP = RemoteEndPoint;
                var ret = stream.Read(buffer);
                if (ret != 3)
                    throw new IOException("Data length error.");
                var packageType = (UdpAsTcpPackageType)buffer[0];
                if (packageType != UdpAsTcpPackageType.SYN)
                    throw new IOException("Package type error.");
                var synNumber = ByteUtils.B2US_BE(buffer, 1);
                //向客户端发送SYN_ACK
                buffer[0] = (byte)UdpAsTcpPackageType.SYN_ACK;
                synNumber++;                
                ByteUtils.US2B_BE(Convert.ToUInt16(synNumber)).CopyTo(buffer, 1);
                stream.Write(buffer, 0, 3);

                //等待客户端SYN_ACK 
                ret = stream.Read(buffer);
                if (ret != 3)
                    throw new IOException("Data length error.");
                packageType = (UdpAsTcpPackageType)buffer[0];
                if (packageType != UdpAsTcpPackageType.SYN_ACK)
                    throw new IOException("Package type error.");
                var ackNumber = ByteUtils.B2US_BE(buffer, 1);
                if (ackNumber != synNumber + 1)
                    throw new IOException("ACK number error.");
                
                //连接建立
                Connected = true;
            }
            catch (Exception ex)
            {
                OnError(ex);
                throw;
            }
            finally
            {
                isWaitingForSynAndAck = false;
            }
        }
        internal void clientSynAndAck()
        {
            try
            {
                var stream = GetStream();

                //向服务端发送SYN
                var buffer = new byte[1024];
                buffer[0] = (byte)UdpAsTcpPackageType.SYN;
                var synNumber = Random.Shared.Next(ushort.MinValue, ushort.MaxValue / 2);
                ByteUtils.US2B_BE(Convert.ToUInt16(synNumber)).CopyTo(buffer, 1);
                stream.Write(buffer, 0, 3);

                //等待服务端SYN_ACK
                IPEndPoint remoteEP = RemoteEndPoint;
                var ret = stream.Read(buffer);
                if (ret != 3)
                    throw new IOException("Data length error.");
                var packageType = (UdpAsTcpPackageType)buffer[0];
                if (packageType != UdpAsTcpPackageType.SYN_ACK)
                    throw new IOException("Package type error.");
                var ackNumber = ByteUtils.B2US_BE(buffer, 1);
                if (ackNumber != synNumber + 1)
                    throw new IOException("ACK number error.");
                //向服务端再发送ACK
                ackNumber++;
                ByteUtils.US2B_BE(ackNumber).CopyTo(buffer, 1);
                stream.Write(buffer, 0, 3);
                //连接建立
                Connected = true;
            }
            catch (Exception ex)
            {
                OnError(ex);
                throw;
            }
            finally
            {
                isWaitingForSynAndAck = false;
            }
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
            networkStream?.Close();
            networkStream?.Dispose();
            networkStream = null;
            if (listener != null)
                listener.OnClientDisconnected(this);
            else
                Client.Close();
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
            if (!isWaitingForSynAndAck && !Connected)
                throw new IOException("Not connected", LastException);

            if (Client.Client.RemoteEndPoint == null)
                Client.Send(buffer, count, RemoteEndPoint);
            else
                Client.Send(buffer, count);
        }
    }
}
