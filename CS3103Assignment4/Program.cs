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
            // === Network Emulator Setup ===
            // (2% packet loss, up to 10ms one-way latency)
            TestChannel testChannel = new TestChannel(
                0.02f,    // packetLossRate
                0.01f,    // maxLatency (seconds)
                0.03f,    // reorder rate
                50001,    // serverSocketPort
                50002,    // clientSocketPort
                50000,    // forward: client → server
                50003     // forward: server → client
            );

            // === Initialize GameNet instances ===
            GameNet server = new GameNet("Server");
            server.ListenForConnection(50000);

            GameNet client = new GameNet("Client");
            client.Connect("127.0.0.1", 50002);

            byte[] clientUnreliableData = new byte[] { 1, 2, 3, 4, 5 };
            byte[] serverUnreliableData = new byte[] { 6, 7, 8, 9, 10 };
            byte[] clientReliableData = new byte[] { 11, 12, 13, 14, 15 };
            byte[] serverReliableData = new byte[] { 16, 17, 18, 19, 20 };

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            int loopCount = 0;
            int disconnectStartTick = 500;

            Console.WriteLine("=== Simulation Started ===\n");

            // === Main Game Loop ===
            while (loopCount < 500 || client.IsConnected || server.IsConnected)
            {
                loopCount++;
                float deltaSeconds = (float)stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();

                if (deltaSeconds == 0)
                    deltaSeconds = 0.01f;

                // Simulate network conditions
                testChannel.Tick(deltaSeconds);

                // Update both client and server
                server.Tick(deltaSeconds);
                client.Tick(deltaSeconds);

                DateTime now = DateTime.Now;

                // === Trigger disconnection using GameNetAPI ===
                if (loopCount > disconnectStartTick && client.IsConnected && !client.IsDisconnecting)
                {
                    Console.WriteLine("\n=== Initiating Graceful Disconnection ===\n");
                    client.Disconnect();
                }

                // === Normal data exchange phase ===
                else if (client.IsConnected && server.IsConnected &&
                         !client.IsDisconnecting && !server.IsDisconnecting)
                {
                    client.Send(ChannelType.Unreliable, now.ToBinary(), clientUnreliableData);
                    server.Send(ChannelType.Unreliable, now.ToBinary(), serverUnreliableData);
                    client.Send(ChannelType.Reliable, now.ToBinary(), clientReliableData);
                    server.Send(ChannelType.Reliable, now.ToBinary(), serverReliableData);
                }

                // === Process any received packets ===
                DisplayPackets(server, "Server");
                DisplayPackets(client, "Client");

                Thread.Sleep(10);
            }

            Console.WriteLine("\nAll GameNet channels disconnected successfully.");

            // === Print Metrics Summary from both ends ===
            server.PrintMetrics();
            client.PrintMetrics();

            Console.WriteLine("\nProgram finished executing. Press Enter to exit.");
            Console.ReadLine();
        }

        private static void DisplayPackets(GameNet node, string name)
        {
            byte[][] unreliable = node.GetUnreliablePackets();
            byte[][] reliable = node.GetReliablePackets();

            if (unreliable.Length > 0)
                Console.WriteLine($"[User] {name} received unreliable data: " +
                    string.Join("\n", unreliable.Select(x => "{" + string.Join(", ", x) + "}")));

            if (reliable.Length > 0)
                Console.WriteLine($"[User] {name} received reliable data: " +
                    string.Join("\n", reliable.Select(x => "{" + string.Join(", ", x) + "}")));
        }
    }
}



