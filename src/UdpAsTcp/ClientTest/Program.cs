using System.Net;
using System.Text;
using UdpAsTcp;

Thread.Sleep(2000);
var client = new UdpAsTcpClient();
Console.WriteLine("Connecting...");
client.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3001));
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
