using System.Collections.Concurrent;
using UdpAsTcp.Utils;

namespace UdpAsTcp
{
    public class UdpAsTcpNetworkStream : Stream
    {
        private struct ReadBufferInfo
        {
            /// <summary>
            /// 包序号
            /// </summary>
            public int PackageIndex;
            /// <summary>
            /// 数组数组序号
            /// </summary>
            public int BufferBufferIndex;
        }

        private struct WriteBufferInfo
        {
            /// <summary>
            /// 包开始序号
            /// </summary>
            public int PackageBeginIndex;
            /// <summary>
            /// 包结束序号
            /// </summary>
            public int PackageEndIndex;
        }

        private const int PACKAGE_HEAD_SIZE = 3;
        /// <summary>
        /// 每个数据包最大负载
        /// </summary>
        private const int MAX_PAYLOAD_PER_PACKAGE = 1024;
        /// <summary>
        /// 默认窗口大小
        /// </summary>
        private const int DEFAULT_BUFFER_SIZE = 1024;
        private const int MAX_PACKAGE_INDEX = ushort.MaxValue;

        private UdpAsTcpClient udpAsTcpClient;
        private CancellationTokenSource cts;
        private ConcurrentDictionary<int, byte[]> readDict = new ConcurrentDictionary<int, byte[]>();
        private ConcurrentDictionary<int, byte[]> writeDict = new ConcurrentDictionary<int, byte[]>();

        //读取缓存信息
        private ReadBufferInfo readBufferInfo = new ReadBufferInfo();
        //写入缓存信息
        private WriteBufferInfo writeBufferInfo = new WriteBufferInfo();

        public UdpAsTcpNetworkStream(UdpAsTcpClient udpAsTcpClient)
        {
            this.udpAsTcpClient = udpAsTcpClient;
            cts = new CancellationTokenSource();
        }

        public override void Close()
        {
            cts?.Cancel();
            cts = null;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var beginTime = DateTime.Now;
            var ret = 0;
            while (true)
            {
                if (udpAsTcpClient.ReceiveTimeout > 0)
                {
                    if (udpAsTcpClient.ReceiveTimeout > 0
                        && (DateTime.Now - beginTime).TotalMilliseconds > udpAsTcpClient.ReceiveTimeout)
                        throw new TimeoutException($"Read timeout.");
                }
                byte[] readBufferBuffer;
                if (!readDict.TryGetValue(readBufferInfo.PackageIndex, out readBufferBuffer))
                {
                    //如果已经读了一部分数据，则先返回，不再等待
                    if (ret > 0)
                        return ret;

                    try
                    {
                        await Task.Delay(10, cancellationToken);
                        continue;
                    }
                    catch (TaskCanceledException)
                    {
                        return ret;
                    }
                }
                var avalibleCount = readBufferBuffer.Length - readBufferInfo.BufferBufferIndex;
                var readCount = Math.Min(avalibleCount, count);
                Buffer.BlockCopy(readBufferBuffer, readBufferInfo.BufferBufferIndex, buffer, offset, readCount);
                readBufferInfo.BufferBufferIndex += readCount;
                offset += readCount;
                count -= readCount;
                ret += readCount;
                avalibleCount = readBufferBuffer.Length - readBufferInfo.BufferBufferIndex;
                //如果当前包序号的数据已读取完成，则从字典中移除
                if (avalibleCount <= 0)
                {
                    readDict.TryRemove(readBufferInfo.PackageIndex, out _);

                    var newReadBufferInfo = new ReadBufferInfo();
                    newReadBufferInfo.PackageIndex = readBufferInfo.PackageIndex + 1;
                    newReadBufferInfo.PackageIndex %= MAX_PACKAGE_INDEX;
                    newReadBufferInfo.BufferBufferIndex = 0;
                    readBufferInfo = newReadBufferInfo;
                }
                //如果已经读取够了
                if (count <= 0)
                    return ret;
            }
        }

        internal void HandleBuffer(byte[] buffer)
        {
            if (buffer.Length < 3)
                return;
            var packageType = (UdpAsTcpPackageType)buffer[0];
            var packageIndex = ByteUtils.B2US_BE(buffer, 1);
            switch (packageType)
            {
                //数据包
                case UdpAsTcpPackageType.DATA:
                    //发送确认包
                    buffer[0] = (byte)UdpAsTcpPackageType.DATA_ACK;
                    udpAsTcpClient.Send(buffer, PACKAGE_HEAD_SIZE);
                    var currentReadBufferInfo = readBufferInfo;
                    //如果包序号不在读取窗口范围，则抛弃
                    if (currentReadBufferInfo.PackageIndex + DEFAULT_BUFFER_SIZE < MAX_PACKAGE_INDEX)
                    {
                        if (packageIndex < currentReadBufferInfo.PackageIndex
                            || packageIndex > currentReadBufferInfo.PackageIndex + DEFAULT_BUFFER_SIZE)
                            return;
                    }
                    else
                    {
                        if (packageIndex < currentReadBufferInfo.PackageIndex
                            && packageIndex > currentReadBufferInfo.PackageIndex + DEFAULT_BUFFER_SIZE)
                            return;
                    }
                    var payload = buffer.Skip(PACKAGE_HEAD_SIZE).ToArray();
                    readDict.AddOrUpdate(packageIndex, payload, (k, v) => payload);
                    break;
                //确认包
                case UdpAsTcpPackageType.DATA_ACK:
                    var currentWriteBufferInfo = writeBufferInfo;
                    //如果包序号不在发送窗口范围，则抛弃
                    if (currentWriteBufferInfo.PackageBeginIndex < currentWriteBufferInfo.PackageEndIndex)
                    {
                        if (packageIndex < currentWriteBufferInfo.PackageBeginIndex
                            || packageIndex > currentWriteBufferInfo.PackageEndIndex)
                            return;
                    }
                    else
                    {
                        if (packageIndex < currentWriteBufferInfo.PackageBeginIndex
                            && packageIndex > currentWriteBufferInfo.PackageEndIndex)
                            return;
                    }
                    writeDict.TryRemove(packageIndex, out _);
                    //如果可以移动
                    while (true)
                    {
                        if (writeDict.ContainsKey(currentWriteBufferInfo.PackageBeginIndex))
                            break;

                        currentWriteBufferInfo.PackageBeginIndex++;
                        currentWriteBufferInfo.PackageBeginIndex %= MAX_PACKAGE_INDEX;
                        writeBufferInfo = currentWriteBufferInfo;
                        if (currentWriteBufferInfo.PackageBeginIndex == currentWriteBufferInfo.PackageEndIndex)
                            break;
                    }
                    break;
                //未知包
                default:
                    return;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count, CancellationToken.None).Wait();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var beginTime = DateTime.Now;
            var currentWriteBufferInfo = writeBufferInfo;
            var waitToWriteCount = count;
            for (var i = 0; i < count; i += MAX_PAYLOAD_PER_PACKAGE)
            {
                var takeCount = Math.Min(waitToWriteCount, MAX_PAYLOAD_PER_PACKAGE);
                var data = new byte[PACKAGE_HEAD_SIZE + takeCount];
                var currentPackageIndex = currentWriteBufferInfo.PackageEndIndex;
                //包类型
                data[0] = (byte)UdpAsTcpPackageType.DATA;
                //包序号
                ByteUtils.US2B_BE(Convert.ToUInt16(currentPackageIndex)).CopyTo(data, 1);
                //包负载
                Buffer.BlockCopy(buffer, offset + i, data, PACKAGE_HEAD_SIZE, takeCount);

                waitToWriteCount -= takeCount;
                while (true)
                {
                    if (udpAsTcpClient.SendTimeout > 0
                        && (DateTime.Now - beginTime).TotalMilliseconds > udpAsTcpClient.SendTimeout)
                        throw new TimeoutException("Write timeout.");
                    if (writeDict.Count >= DEFAULT_BUFFER_SIZE)
                    {
                        try
                        {
                            await Task.Delay(10, cancellationToken);
                            continue;
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }
                    }
                    writeDict.AddOrUpdate(currentPackageIndex, data, (k, v) => data);
                    currentWriteBufferInfo.PackageEndIndex++;
                    currentWriteBufferInfo.PackageEndIndex %= MAX_PACKAGE_INDEX;
                    writeBufferInfo = currentWriteBufferInfo;
                    udpAsTcpClient.Send(data);
                    _ = Task.Delay(udpAsTcpClient.DataAckRetryInterval, cancellationToken).ContinueWith(async t =>
                    {
                        if (t.IsCanceled)
                            return;
                        try
                        {
                            //重试3次
                            for (var i = 0; i < udpAsTcpClient.DataAckRetryTimes; i++)
                            {
                                byte[] data;
                                if (!writeDict.TryGetValue(currentPackageIndex, out data))
                                    return;
                                //再次发送
                                udpAsTcpClient.Send(data);
                                await Task.Delay(udpAsTcpClient.DataAckRetryInterval, cancellationToken);
                            }
                            //等待接收确认包超时，触发错误
                            udpAsTcpClient.OnError(new IOException($"Wait for package[{currentPackageIndex}] ACK timeout."));

                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }
                        catch { }
                    });
                    break;
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override void Flush() { }
    }
}
