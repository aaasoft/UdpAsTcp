using System.Net;
using System.Text;
using UdpAsTcp;

var client = new UdpAsTcpClient();
Console.WriteLine("Connecting...");
client.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3001));
Console.WriteLine("Connected.");
var stream = client.GetStream();
Task.Run(() =>
{
    while (true)
    {
        var line = DateTime.Now.ToString() + Environment.NewLine;
        stream.Write(Encoding.Default.GetBytes(line));
        Console.WriteLine("Send:" + line);
        Thread.Sleep(1000);
    }
});
var reader = new StreamReader(stream);
Task.Run(() =>
{
    while (true)
    {
        var line = reader.ReadLine();
        Console.WriteLine("Recv:" + line);
    }
});
Console.ReadLine();
