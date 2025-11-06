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
            ReliableChannel server = new ReliableChannel();
            server.Name = "Server";
            server.ListenForConnection(50000);
            ReliableChannel client = new ReliableChannel();
            client.Name = "Client";
            client.Connect("127.0.0.1", 50002);
            byte[] clientData = new byte[] { 1, 2, 3, 4, 5 };
            byte[] serverData = new byte[] { 6, 7, 8, 9, 10 };
            Stopwatch stopwatch = new Stopwatch();
            // run with game loop, no need multi threading
            while (true)
            {
                float deltaSeconds = stopwatch.ElapsedMilliseconds / 1000;
                stopwatch.Restart();
                testChannel.Tick();
                server.Tick(deltaSeconds);
                client.Tick(deltaSeconds);

                if(client.IsConnected && server.IsConnected)
                {
                    client.SendData(clientData);
                    server.SendData(serverData);
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
