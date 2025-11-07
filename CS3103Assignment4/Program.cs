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
            client.Connect("127.0.0.1", 50002);
            byte[] clientUnreliableData = new byte[] { 1, 2, 3, 4, 5 };
            byte[] serverUnreliableData = new byte[] { 6, 7, 8, 9, 10 };
            byte[] clientReliableData = new byte[] { 11, 12, 13, 14, 15 };
            byte[] serverReliableData = new byte[] { 16, 17, 18, 19, 20 };
            Stopwatch stopwatch = new Stopwatch();
            int gameLoopIterationCount = 0;
            int gameLoopIterationCountToStartDisconnecting = 999;
            // run with game loop, no need multi threading
            while(gameLoopIterationCount < 999 || (client.IsConnected || server.IsConnected))
            {
                gameLoopIterationCount++;
                float deltaSeconds = stopwatch.ElapsedMilliseconds / 1000;
                if(deltaSeconds == 0)
                {
                    // when waiting for data during disconnecting, the value of deltaSeconds is too small that it may become zero due to floating-point precision problem.
                    // set deltaSeconds = 0.01f here to make sure the program will not stuck when deltaTime is too small for floating-point numbers to represent.
                    deltaSeconds = 0.01f;
                }
                stopwatch.Restart();
                testChannel.Tick();
                server.Tick(deltaSeconds);
                client.Tick(deltaSeconds);
                DateTime now = DateTime.Now;
                if (gameLoopIterationCount > gameLoopIterationCountToStartDisconnecting && client.IsConnected && !client.IsDisconnecting)
                {
                    Console.WriteLine("\n\n\n=== Disconnection Starts ===\n");
                    client.Disconnect();
                }
                else
                {
                    if (client.IsConnected && server.IsConnected && !client.IsDisconnecting && !server.IsDisconnecting)
                    {
                        client.Send(ChannelType.Unreliable, now.ToBinary(), clientUnreliableData);
                        server.Send(ChannelType.Unreliable, now.ToBinary(), serverUnreliableData);
                        client.Send(ChannelType.Reliable, now.ToBinary(), clientReliableData);
                        server.Send(ChannelType.Reliable, now.ToBinary(), serverReliableData);
                    }
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
            Console.WriteLine("\nAll GameNet channels have disconnected. Program finished executing, press enter to exit.");
            Console.ReadLine();
        }
    }
}
