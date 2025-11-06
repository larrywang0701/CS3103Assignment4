using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CS3103Assignment4
{
    class UnreliableChannel
    {
        private IPEndPoint _remoteHost;
        public IPEndPoint RemoteHost { get { return this._remoteHost; } }
        private UdpClient _socket;

        public string Name { get; set; }

        public virtual void ListenForConnection(ushort port)
        {
            this._socket = new UdpClient();
            this._socket.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        public virtual void Connect(string remoteHost, ushort remotePort)
        {
            this._socket = new UdpClient();
            //this._socket.Client.Bind(new IPEndPoint(IPAddress.Any, selfPort));
            this._remoteHost = new IPEndPoint(IPAddress.Parse(remoteHost), remotePort);
        }

        public virtual void Send(byte[] data, int length)
        {
            this._socket.Send(data, length, this._remoteHost);
        }

        public virtual byte[] Receive()
        {
            if (this._socket.Available > 0)
            {
                return this._socket.Receive(ref this._remoteHost);
            }
            return null;
        }

        protected void Log(string message)
        {
            Console.WriteLine(string.Format("[{0}] {1}", this.Name, message));
        }
    }
}
