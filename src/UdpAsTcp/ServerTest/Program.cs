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