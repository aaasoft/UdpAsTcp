# UdpAsTcp [![NuGet Version](http://img.shields.io/nuget/v/UdpAsTcp.svg?style=flat)](https://www.nuget.org/packages/UdpAsTcp/)
* Use UDP like TCP.
* 像使用TCP一样使用UDP

## How to Use - 使用方法

### Server - 服务端
```csharp
using UdpAsTcp;

var listener = new UdpAsTcpListener(3001);
//listener.Debug = true;
//if you want to listen IPv6 udp port.Use IPAddress.IPv6Any or other IPv6 address.
//var listener = new UdpAsTcpListener(IPAddress.IPv6Any, 3001);
listener.Start();
listener.ClientConnected += (s, e) => Console.WriteLine($"[{e.RemoteIPEndPoint}] Connected.");
listener.ClientDisconnected += (s, e) => Console.WriteLine($"[{e.RemoteIPEndPoint}] Disconnected.Reason:{e.Exception}");

Console.WriteLine($"Listening on {listener.LocalEndPoint}...");

while (true)
{
    var client = listener.AcceptClient();
    var stream = client.GetStream();
    var writer = new StreamWriter(stream);
    Task.Run(() =>
    {
        try
        {
            while (true)
            {
                var line = DateTime.Now.ToString();
                writer.WriteLine(line);
                writer.Flush();
                Thread.Sleep(1000);
            }
        }
        catch
        {
            Console.WriteLine($"[{client.RemoteEndPoint}]: Write error.");
        }
    });
    var reader = new StreamReader(stream);
    Task.Run(() =>
    {
        try
        {
            while (true)
            {
                var line = reader.ReadLine();
                Console.WriteLine($"[{client.RemoteEndPoint}]: {line}");
            }
        }
        catch
        {
            Console.WriteLine($"[{client.RemoteEndPoint}]: Read error.");
        }
    });
}
```
### Client - 客户端
```csharp
using UdpAsTcp;

Thread.Sleep(2000);
var host = "127.0.0.1";
var port = 3001;

var client = new UdpAsTcpClient();
//client.Debug = true;
Console.WriteLine($"Connecting to {host}:{port}...");
client.Connect(host, port);
Console.WriteLine("Connected.");
var stream = client.GetStream();
var writer = new StreamWriter(stream);
Task.Run(() =>
{
    try
    {
        while (true)
        {
            var line = DateTime.Now.ToString();
            writer.WriteLine(line);
            writer.Flush();
            Thread.Sleep(1000);
        }
    }
    catch
    {
        Console.WriteLine($"[{client.RemoteEndPoint}]: Write error.");
    }
});
var reader = new StreamReader(stream);
Task.Run(() =>
{
    try
    {
        while (true)
        {
            var line = reader.ReadLine();
            Console.WriteLine($"[{client.RemoteEndPoint}]: {line}");
        }
    }
    catch
    {
        Console.WriteLine($"[{client.RemoteEndPoint}]: Read error.");
    }
});
Console.ReadLine();
```

## Main Types - 主要类型

The main types provided by this library are:
此程序库提供了这些主要类型:

* `UdpAsTcpListener` (like `TcpListener`)
* `UdpAsTcpClient` (like `TcpClient`)
