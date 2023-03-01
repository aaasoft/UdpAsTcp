# UdpAsTcp [![NuGet Version](http://img.shields.io/nuget/v/UdpAsTcp.svg?style=flat)](https://www.nuget.org/packages/UdpAsTcp/)
* Use UDP like TCP.
* ��ʹ��TCPһ��ʹ��UDP

## How to Use - ʹ�÷���

### Server - �����
```csharp
var listener = new UdpAsTcpListener(3001);
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
### Client - �ͻ���
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

## Main Types - ��Ҫ����

The main types provided by this library are:
�˳�����ṩ����Щ��Ҫ����:

* `UdpAsTcpListener` (like `TcpListener`)
* `UdpAsTcpClient` (like `TcpClient`)
