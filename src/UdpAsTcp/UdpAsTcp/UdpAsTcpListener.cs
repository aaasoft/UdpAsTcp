using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace UdpAsTcp
{
    /*
     [1字节]  [2字节]           [n字节]
     包类型   包序号             包负载
     0:数据   从0开始到65535     最大1024字节
     1:确认   ushort

     接收方收到数据包后，需要发送确认包。
     发送方收到确认包后，才从内存中移除缓存。
     包序号达到65535后，又从0开始。
     发送和接收缓存默认为1024个包

    变量：
    接收：接收包序号，接收数组序号
    发送：发送包序号，发送数组序号    
     */
    public class UdpAsTcpListener
    {
        private UdpClient listener;
        private CancellationTokenSource cts;
        private Func<UdpClient> newListenerFunc;
        private ConcurrentQueue<UdpAsTcpClient> newClientQueue = new ConcurrentQueue<UdpAsTcpClient>();
        private ConcurrentDictionary<IPEndPoint, UdpAsTcpClient> clientDict = new ConcurrentDictionary<IPEndPoint, UdpAsTcpClient>();
        public IPEndPoint LocalEndPoint { get; private set; }
        public UdpAsTcpListener(IPEndPoint localEP)
        {
            LocalEndPoint = localEP;
            newListenerFunc = () => new UdpClient(localEP);
        }

        public UdpAsTcpListener(int port): this(new IPEndPoint(IPAddress.Any, port)) { }

        public void Start()
        {
            cts?.Cancel();
            cts = null;
            cts = new CancellationTokenSource();

            listener = newListenerFunc();
            _ = beginRecv(listener, cts.Token);
        }

        private async ValueTask beginRecv(UdpClient listener, CancellationToken token)
        {
            try
            {
                var ret = await listener.ReceiveAsync(token);
                var buffer = ret.Buffer;
                var remoteEP = ret.RemoteEndPoint;
                UdpAsTcpClient client = null;
                //如果是已有连接
                if (clientDict.TryGetValue(remoteEP, out client))
                {
                    client.HandleBuffer(buffer);
                }
                //如果是新连接
                else
                {
                    client = new UdpAsTcpClient(this, listener, remoteEP);
                    client.HandleBuffer(buffer);
                    clientDict.TryAdd(remoteEP, client);
                    newClientQueue.Enqueue(client);
                }
                _ = beginRecv(listener, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                foreach (var client in clientDict.Values)
                    client.OnError(ex);
            }
        }

        public void Stop()
        {
            cts?.Cancel();
            cts = null;

            listener?.Close();
            listener?.Dispose();
            listener = null;

            clientDict.Clear();
            newClientQueue.Clear();
        }

        public UdpAsTcpClient AcceptClient()
        {
            return AcceptClientAsync().Result;
        }

        public async ValueTask<UdpAsTcpClient> AcceptClientAsync()
        {
            while (true)
            {
                if (newClientQueue.Count > 0)
                {
                    if (newClientQueue.TryDequeue(out var client))
                        return client;
                }
                await Task.Delay(10);
            }
        }

        internal void OnClientDisconnected(UdpAsTcpClient udpAsTcpClient)
        {
            var key = udpAsTcpClient.RemoteEndPoint;
            while (clientDict.ContainsKey(key))
                clientDict.TryRemove(key, out _);
        }
    }
}