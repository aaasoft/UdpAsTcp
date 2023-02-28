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
    //Task.Run(() =>
    //{
    //    while (true)
    //    {
    //        var line = DateTime.Now.ToString() + Environment.NewLine;
    //        stream.Write(Encoding.Default.GetBytes(line));
    //        Console.WriteLine("Send:" + line);
    //        Thread.Sleep(1000);
    //    }
    //});
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

}