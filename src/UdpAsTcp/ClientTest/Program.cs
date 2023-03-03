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
