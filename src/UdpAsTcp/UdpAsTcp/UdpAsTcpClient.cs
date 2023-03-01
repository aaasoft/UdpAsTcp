using System.IO;
using System;
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

        private ushort readSynOrAck(Stream stream, byte[] buffer, UdpAsTcpPackageType type)
        {
            while (true)
            {
                var ret = stream.Read(buffer);
                if (ret != 3)
                    throw new IOException("Data length error.");
                var packageType = (UdpAsTcpPackageType)buffer[0];
                if (packageType != type)
                    throw new IOException("Package type error.");
                return ByteUtils.B2US_BE(buffer, 1);
            }
        }

        private void writeSyncOrAck(Stream stream, byte[] buffer, UdpAsTcpPackageType type,ushort number)
        {
            buffer[0] = (byte)type;
            ByteUtils.US2B_BE(number).CopyTo(buffer, 1);
            stream.Write(buffer, 0, 3);
        }

        internal void serverSynAndAck()
        {
            try
            {
                var stream = GetStream();

                var buffer = new byte[1024];
                //等待客户端SYN
                var synNumber = readSynOrAck(stream, buffer, UdpAsTcpPackageType.SYN);
                //向客户端发送SYN_ACK
                synNumber++;
                writeSyncOrAck(stream, buffer, UdpAsTcpPackageType.SYN_ACK, synNumber);
                //等待客户端SYN_ACK 
                var ackNumber = readSynOrAck(stream, buffer, UdpAsTcpPackageType.SYN_ACK);
                if (ackNumber != synNumber + 1)
                    throw new IOException("ACK number error.");
                //连接建立
                Connected = true;
            }
            catch (Exception ex)
            {
                var connectionException = new IOException("Syn and ACK failed.", ex);
                OnError(connectionException);
                throw connectionException;
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
                var synNumber = Convert.ToUInt16(Random.Shared.Next(ushort.MinValue, ushort.MaxValue / 2));
                writeSyncOrAck(stream, buffer, UdpAsTcpPackageType.SYN, synNumber);
                //等待服务端SYN_ACK
                var ackNumber = readSynOrAck(stream, buffer, UdpAsTcpPackageType.SYN_ACK);
                if (ackNumber != synNumber + 1)
                    throw new IOException("ACK number error.");
                //向服务端再发送ACK
                ackNumber++;
                writeSyncOrAck(stream, buffer, UdpAsTcpPackageType.SYN_ACK, ackNumber);
                //连接建立
                Connected = true;
            }
            catch (Exception ex)
            {
                var connectionException = new IOException("Connect failed.", ex);
                OnError(connectionException);
                throw connectionException;
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
