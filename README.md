# UdpAsTcp [![NuGet Version](http://img.shields.io/nuget/v/UdpAsTcp.svg?style=flat)](https://www.nuget.org/packages/UdpAsTcp/)
* Use UDP like TCP.
* 像使用TCP一样使用UDP

## How to Use - 使用方法

### Server - 服务端
```csharp
var listener = new UdpAsTcpListener(3001);
//if you want to listen IPv6 udp port.Use IPAddress.IPv6Any or other IPv6 address.
//var listener = new UdpAsTcpListener(IPAddress.IPv6Any, 3001);
listener.Start();
Console.WriteLine($"Listening...");

while (true)
{
    var client = listener.AcceptClient();
    Console.WriteLine($"{client.RemoteEndPoint} connected.");
    var stream = client.GetStream();
    var writer = new StreamWriter(stream);
    Task.Run(() =>
    {
        while (true)
        {
            var line = DateTime.Now.ToString();
            writer.WriteLine(line);
            writer.Flush();
            Thread.Sleep(1000);
        }
    });
    var reader = new StreamReader(stream);
    Task.Run(() =>
    {
        while (true)
        {
            var line = reader.ReadLine();
            Console.WriteLine($"[{client.RemoteEndPoint}]: {line}");
        }
    });    
}

```
### Client - 客户端
```csharp
var client = new UdpAsTcpClient();
Console.WriteLine("Connecting...");
client.Connect("127.0.0.1", 3001);
Console.WriteLine("Connected.");
var stream = client.GetStream();
var writer = new StreamWriter(stream);
Task.Run(() =>
{
    while (true)
    {
        var line = DateTime.Now.ToString();
        writer.WriteLine(line);
        writer.Flush();
        Thread.Sleep(1000);
    }
});
var reader = new StreamReader(stream);
Task.Run(() =>
{
    while (true)
    {
        var line = reader.ReadLine();
        Console.WriteLine($"[{client.RemoteEndPoint}]: {line}");
    }
});
Console.ReadLine();

```

## Main Types - 主要类型

The main types provided by this library are:
此程序库提供了这些主要类型:

* `UdpAsTcpListener` (like `TcpListener`)
* `UdpAsTcpClient` (like `TcpClient`)
