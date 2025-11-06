using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CS3103Assignment4
{
    class TestChannel
    {
        private class BufferedPacket
        {
            public byte[] data;
            public float latencyTimer;
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
        private List<byte[]> _buffer_ToServer = new List<byte[]>();
        private List<byte[]> _buffer_ToClient = new List<byte[]>();

        public TestChannel(float packetLossRate, float maximumLatency, ushort serverSocketPort, ushort clientSocketPort, ushort packetFromClientForwardToPort)
        {
            this._serverSocketEndPoint = new IPEndPoint(IPAddress.Any, serverSocketPort);
            this._clientSocketEndPoint = new IPEndPoint(IPAddress.Any, clientSocketPort);
            this._packetFromClientForwardToEndPoint = new IPEndPoint(IPAddress.Loopback, packetFromClientForwardToPort);
            //this._firstSocketForwardToEndPoint = new IPEndPoint(IPAddress.Loopback, firstSocketForwardToPort);
            //this._secondSocketForwardToEndPoint = new IPEndPoint(IPAddress.Loopback, secondSocketForwardToPort);
            this._serverSocket = new UdpClient();
            this._clientSocket = new UdpClient();
            this._serverSocket.Client.Bind(this._serverSocketEndPoint);
            this._clientSocket.Client.Bind(this._clientSocketEndPoint);
            this._packetLossRate = packetLossRate;
            this._maximumLatency = maximumLatency;
        }

        public void Tick()
        {
            if (this._serverSocket.Available > 0)
            {
                byte[] data = this._serverSocket.Receive(ref this._packetFromClientForwardToEndPoint);
                if (!this.ShouldDropPacket())
                {
                    this._clientSocket.Send(data, data.Length, this._packetFromServerForwardToEndPoint);
                }
            }
            if(this._clientSocket.Available > 0)
            {
                byte[] data = this._clientSocket.Receive(ref this._packetFromServerForwardToEndPoint);
                if (!this.ShouldDropPacket())
                {
                    this._serverSocket.Send(data, data.Length, this._packetFromClientForwardToEndPoint);
                }
            }
        }

        private bool ShouldDropPacket()
        {
            return (1 - this._random.NextDouble()) >= (1 - this._packetLossRate);
        }
    }
}
