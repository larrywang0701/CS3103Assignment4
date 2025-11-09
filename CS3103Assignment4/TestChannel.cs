using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace CS3103Assignment4
{
    class TestChannel
    {
        private class BufferedPacket
        {
            public byte[] Data;
            public IPEndPoint Destination;
            public float LatencyTimer;
        }

        private IPEndPoint _serverSocketEndPoint;
        private IPEndPoint _clientSocketEndPoint;
        private IPEndPoint _packetFromServerForwardToEndPoint;
        private IPEndPoint _packetFromClientForwardToEndPoint;
        private UdpClient _serverSocket;
        private UdpClient _clientSocket;
        private Random _random = new Random(0);
        private float _packetLossRate;
        private float _maximumLatency;
        private float _reorderRate;

        // Buffers for in-flight packets (simulate latency)
        private List<BufferedPacket> _buffer_ToServer = new List<BufferedPacket>();
        private List<BufferedPacket> _buffer_ToClient = new List<BufferedPacket>();

        public TestChannel(float packetLossRate, float maximumLatency, float reorderRate, ushort serverSocketPort, ushort clientSocketPort, ushort packetFromClientForwardToPort, ushort packetFromServerForwardToPort)
        {
            this._serverSocketEndPoint = new IPEndPoint(IPAddress.Any, serverSocketPort);
            this._clientSocketEndPoint = new IPEndPoint(IPAddress.Any, clientSocketPort);
            this._packetFromClientForwardToEndPoint = new IPEndPoint(IPAddress.Loopback, packetFromClientForwardToPort);
            this._packetFromServerForwardToEndPoint = new IPEndPoint(IPAddress.Loopback, packetFromServerForwardToPort);

            this._serverSocket = new UdpClient();
            this._clientSocket = new UdpClient();
            this._serverSocket.Client.Bind(this._serverSocketEndPoint);
            this._clientSocket.Client.Bind(this._clientSocketEndPoint);
            this._packetLossRate = packetLossRate;
            this._maximumLatency = maximumLatency;
            this._reorderRate = reorderRate;
        }

        public void Tick(float deltaTime)
        {
            while (this._serverSocket.Available > 0)
            {
                byte[] data = this._serverSocket.Receive(ref this._packetFromClientForwardToEndPoint);
                if (!ShouldDropPacket())
                {
                    AddPacketToBuffer(_buffer_ToClient, data, _packetFromServerForwardToEndPoint);
                }
            }

            while (this._clientSocket.Available > 0)
            {
                byte[] data = this._clientSocket.Receive(ref this._packetFromServerForwardToEndPoint);
                if (!ShouldDropPacket())
                {
                    AddPacketToBuffer(_buffer_ToServer, data, _packetFromClientForwardToEndPoint);
                }
            }

            ProcessBuffer(_buffer_ToClient, _clientSocket, deltaTime);
            ProcessBuffer(_buffer_ToServer, _serverSocket, deltaTime);
        }

        private void AddPacketToBuffer(List<BufferedPacket> buffer, byte[] data, IPEndPoint destination)
        {
            float latency = (float)(_random.NextDouble() * _maximumLatency); 
            buffer.Add(new BufferedPacket
            {
                Data = data,
                Destination = destination,
                LatencyTimer = latency
            });
        }

        private void ProcessBuffer(List<BufferedPacket> buffer, UdpClient socket, float deltaTime)
        {
            foreach (var packet in buffer)
                packet.LatencyTimer -= deltaTime;

            var ready = buffer.Where(p => p.LatencyTimer <= 0).ToList();

            if (ready.Count > 1 && _random.NextDouble() < this._reorderRate)
            {
                ready = ready.OrderBy(_ => _random.Next()).ToList(); 
            }

            foreach (var packet in ready)
            {
                socket.Send(packet.Data, packet.Data.Length, packet.Destination);
                buffer.Remove(packet);
            }
        }

        private bool ShouldDropPacket()
        {
            return _random.NextDouble() < _packetLossRate;
        }
    }
}



