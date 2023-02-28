using System.Net;
using System.Text;
using UdpAsTcp;

var listener = new UdpAsTcpListener(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3001));
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