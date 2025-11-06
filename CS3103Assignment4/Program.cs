using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CS3103Assignment4
{
    class Program
    {
        static void Main(string[] args)
        {
            TestChannel testChannel = new TestChannel(0.02f, 0.01f, 50001, 50002, 50000);
            GameNet server = new GameNet("Server");
            server.ListenForConnection(50000);
            GameNet client = new GameNet("Client");
            client.Connect("127.0.0.1", 50000);
            byte[] clientData = new byte[] { 1, 2, 3, 4, 5 };
            byte[] serverData = new byte[] { 6, 7, 8, 9, 10 };
            Stopwatch stopwatch = new Stopwatch();
            // run with game loop, no need multi threading
            while (true)
            {
                float deltaSeconds = stopwatch.ElapsedMilliseconds / 1000;
                stopwatch.Restart();
                //testChannel.Tick();
                server.Tick(deltaSeconds);
                client.Tick(deltaSeconds);
                DateTime now = DateTime.Now;
                if(client.IsConnected && server.IsConnected)
                {
                    client.Send(ChannelType.Reliable, now.ToBinary(), clientData);
                    server.Send(ChannelType.Reliable, now.ToBinary(), serverData);
                }
                byte[][] serverReceived = server.GetReliablePackets();
                byte[][] clientReceived = client.GetReliablePackets();
                if (serverReceived.Length > 0)
                {
                    Console.WriteLine("Server received ready data: " + string.Join("\n", serverReceived.Select(x => "{" + string.Join(", ", x) + "}")));
                }
                if (clientReceived.Length > 0)
                {
                    Console.WriteLine("Client received ready data: " + string.Join("\n", clientReceived.Select(x => "{" + string.Join(", ", x) + "}")));
                }
            }
            /*Thread thread = new Thread(() =>
            {
                Stopwatch stopwatch = new Stopwatch();
                while (true)
                {
                    float deltaSeconds = stopwatch.ElapsedMilliseconds / 1000;
                    stopwatch.Restart();
                    testChannel.Tick();
                    server.Tick(deltaSeconds);
                    client.Tick(deltaSeconds);
                    
                }
            });
            thread.IsBackground = true;
            thread.Start();
            while (true)
            {
                lock (client)
                {
                    if (client.IsConnected)
                    {
                        client.SendData(clientData);
                       // server.SendData(serverData);
                    }
                }
                Thread.Sleep(5);
            }*/
        }
    }
}
