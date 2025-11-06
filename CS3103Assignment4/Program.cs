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
            byte[] clientUnreliableData = new byte[] { 1, 2, 3, 4, 5 };
            byte[] serverUnreliableData = new byte[] { 6, 7, 8, 9, 10 };
            byte[] clientReliableData = new byte[] { 11, 12, 13, 14, 15 };
            byte[] serverReliableData = new byte[] { 16, 17, 18, 19, 20 };
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
                    client.Send(ChannelType.Unreliable, now.ToBinary(), clientUnreliableData);
                    server.Send(ChannelType.Unreliable, now.ToBinary(), serverUnreliableData);
                    client.Send(ChannelType.Reliable, now.ToBinary(), clientReliableData);
                    server.Send(ChannelType.Reliable, now.ToBinary(), serverReliableData);
                }
                byte[][] serverReceivedUnreliableData = server.GetUnreliablePackets();
                byte[][] clientReceivedUnreliableData = client.GetUnreliablePackets();
                byte[][] serverReceivedReliableData = server.GetReliablePackets();
                byte[][] clientReceivedReliableData = client.GetReliablePackets();
                if (serverReceivedUnreliableData.Length > 0)
                {
                    Console.WriteLine("[User] Server received unreliable data: " + string.Join("\n", serverReceivedUnreliableData.Select(x => "{" + string.Join(", ", x) + "}")));
                }
                if (clientReceivedUnreliableData.Length > 0)
                {
                    Console.WriteLine("[User] Client received unreliable data: " + string.Join("\n", clientReceivedUnreliableData.Select(x => "{" + string.Join(", ", x) + "}")));
                }
                if (serverReceivedReliableData.Length > 0)
                {
                    Console.WriteLine("[User] Server received reliable data: " + string.Join("\n", serverReceivedReliableData.Select(x => "{" + string.Join(", ", x) + "}")));
                }
                if (clientReceivedReliableData.Length > 0)
                {
                    Console.WriteLine("[User] Client received reliable data: " + string.Join("\n", clientReceivedReliableData.Select(x => "{" + string.Join(", ", x) + "}")));
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
