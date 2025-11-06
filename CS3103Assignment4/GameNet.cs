using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CS3103Assignment4
{
    class GameNet : IGameNetAPI
    {
        private IPEndPoint _remoteHost;
        public IPEndPoint RemoteHost { get { return this._remoteHost; } }
        private UdpClient _socket;

        private ReliableChannel _reliableChannel;
        private UnreliableChannel _unreliableChannel;

        private uint _sequenceNumber;
        private bool _isListener;

        public bool IsConnected => this._reliableChannel.IsConnected;

        public GameNet(string name)
        {
            this._socket = new UdpClient();
            this._reliableChannel = new ReliableChannel();
            this._reliableChannel.Name = name + " Reliable";
            this._reliableChannel.Sender = this.ReliableSender;
            this._unreliableChannel = new UnreliableChannel();
            this._unreliableChannel.Name = name + " Unreliable";
            this._unreliableChannel.Sender = this.UnreliableSender;
        }

        public void ListenForConnection(ushort port)
        {
            this._socket.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            this._reliableChannel.ListenForConnection();
            this._isListener = true;
        }

        public void Connect(string remoteHost, ushort port)
        {
            this._socket.Connect(remoteHost, port);
            this._unreliableChannel.Connect();
            this._reliableChannel.Connect();
            this._isListener = false;
        }

        private void UnreliableSender(byte[] data, int dataLength)
        {
            if (this._isListener)
            {
                this._socket.Send(data, dataLength, this._remoteHost);
            }
            else
            {
                this._socket.Send(data, dataLength);
            }
        }

        // The reliable sender is only used to send control messages of reliable channel
        private void ReliableSender(byte[] data, int dataLength)
        {
            byte[] encapsulatedData = this.EncapsulateData(ChannelType.Reliable, 0, data);
            this.UnreliableSender(encapsulatedData, encapsulatedData.Length);
        }

        public void Disconnect()
        {
            throw new NotImplementedException();
        }

        public void ReliableGetData(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        private byte[] EncapsulateData(ChannelType channelType, long timeStamp, byte[] data)
        {
            int dataLength = data.Length;
            byte[] packet = new byte[sizeof(ChannelType) + sizeof(uint) + sizeof(long) + dataLength];
            packet[0] = (byte)channelType;
            Array.Copy(BitConverter.GetBytes(this._sequenceNumber), 0, packet, sizeof(ChannelType), sizeof(uint));
            Array.Copy(BitConverter.GetBytes(timeStamp), 0, packet, sizeof(ChannelType) + sizeof(uint), sizeof(long));
            Array.Copy(data, 0, packet, sizeof(ChannelType) + sizeof(uint) + sizeof(long), dataLength);
            return packet;
        }

        public void Send(ChannelType channelType, long timeStamp, byte[] data)
        {
            this._sequenceNumber++;
            if(channelType == ChannelType.Reliable)
            {
                data = this._reliableChannel.GetEncapsulatedDataToSend(data);
            }
            byte[] packet = this.EncapsulateData(channelType, timeStamp, data);
            this.UnreliableSender(packet, packet.Length);
        }

        public byte[][] GetUnreliablePackets()
        {
            return this._unreliableChannel.GetPackets();
        }

        public void Tick(float deltaSeconds)
        {
            if(this._socket.Available > 0)
            {
                byte[] packet = this._socket.Receive(ref this._remoteHost);
                ChannelType channelType = (ChannelType)packet[0];
                byte[] innerPacket = packet.Skip(sizeof(ChannelType) + sizeof(uint) + sizeof(long)).ToArray();
                if(channelType == ChannelType.Reliable)
                {
                    this._reliableChannel.OnReceivedData(innerPacket);
                }
                else
                {
                    this._unreliableChannel.OnReceivedData(innerPacket);
                }
            }
            this._reliableChannel.Tick(deltaSeconds);
        }

        public byte[][] GetReliablePackets()
        {
            return this._reliableChannel.GetPackets();
        }
    }

    public enum ChannelType : byte
    {
        Reliable,
        Unreliable,
    }
}
