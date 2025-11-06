using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CS3103Assignment4
{
    class UnreliableChannel : Channel
    {
        private List<byte[]> _receiveBuffer = new List<byte[]>();

        public virtual void ListenForConnection(ushort port)
        {
            
        }

        public virtual void Connect()
        {
            
        }

        public virtual void Send(byte[] data, int length)
        {
            this.Sender(data, length);
        }

        public override void OnReceivedData(byte[] data)
        {
            this._receiveBuffer.Add(data);
        }

        public override byte[][] GetPackets()
        {
            byte[][] packets = this._receiveBuffer.ToArray();
            this._receiveBuffer.Clear();
            return packets;
        }
    }
}
