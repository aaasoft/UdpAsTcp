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
     发送和接收窗口默认为1024个包，发送窗口中第一个包确认后，窗口向后滑动一格。

    发送缓存：byte[][]，大小为发送窗口的2倍
    接收缓存：byte[][]，大小为接收窗口的2倍
    变量：
    发送：窗口起始包序号，窗口结束包序号，窗口起始数组序号，窗口结束数组序号，发送包序号，发送数组序号
    接收：窗口起始包序号，窗口结束包序号，窗口起始数组序号，窗口结束数组序号，接收包序号，接收数组序号
     */
    public class UdpAsTcpListener
    {
        private UdpClient listener;
        private CancellationTokenSource cts;
        private Func<UdpClient> newListenerFunc;
        private ConcurrentQueue<UdpAsTcpClient> newClientQueue = new ConcurrentQueue<UdpAsTcpClient>();
        private ConcurrentDictionary<IPEndPoint, UdpAsTcpClient> clientDict = new ConcurrentDictionary<IPEndPoint, UdpAsTcpClient>();

        public UdpAsTcpListener(int port)
        {
            newListenerFunc = () => new UdpClient(port);
        }

        public UdpAsTcpListener(IPEndPoint localEP)
        {
            newListenerFunc = () => new UdpClient(localEP);
        }

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
                    client = new UdpAsTcpClient(this, remoteEP);
                    client.HandleBuffer(buffer);
                    clientDict.TryAdd(remoteEP, client);
                    newClientQueue.Enqueue(client);
                }
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