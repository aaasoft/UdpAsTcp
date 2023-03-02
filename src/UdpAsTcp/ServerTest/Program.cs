using System.Net;
using System.Text;
using UdpAsTcp;

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