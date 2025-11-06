using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS3103Assignment4
{
    class ReliableChannel : UnreliableChannel
    {
        private ReliableChannelState _reliableChannelState;
        public bool IsConnected { get { return this._reliableChannelState == ReliableChannelState.Connected || this._reliableChannelState == ReliableChannelState.Disconnecting; } }
        private const int _sendBufferLength = 1024;

        private byte[] _sendBuffer;
        private int _sendBufferNextWriteIndex = 0;
        private int SendBufferDataLength { get { return this._sendBufferNextWriteIndex; } }

        private uint _sequenceNumber;

        private byte[] _receiveBuffer;
        private int _receiveBufferNextReadIndex;

        private uint _remoteReceivedInOrderPacketLastSequenceID;

        private float _timerBeforeResetConnectionDuringConnecting;
        private float _connectionWaitDuration = 0.5f;

        private Dictionary<uint, SentDataPacket> _sentPackets = new Dictionary<uint, SentDataPacket>();
        private Dictionary<uint, ReceivedDataPacket> _receivedPackets = new Dictionary<uint, ReceivedDataPacket>();

        public override void ListenForConnection(ushort port)
        {
            this._sendBuffer = new byte[_sendBufferLength];
            this._reliableChannelState = ReliableChannelState.ListeningForConnection;
            base.ListenForConnection(port);
        }

        public override void Connect(string remoteHost, ushort remotePort)
        {
            base.Connect(remoteHost, remotePort);
            this._reliableChannelState = ReliableChannelState.Connecting;
            this._sendBuffer = new byte[_sendBufferLength];
            this.ResetSendBufferWriteIndex();
            this.WritePacketAndSendImmediately(ControlMessageType.ConnectionRequest, this._sequenceNumber, null, 0, out _);
            this._timerBeforeResetConnectionDuringConnecting = _connectionWaitDuration;
        }

        private void ResetSendBufferWriteIndex()
        {
            this._sendBufferNextWriteIndex = 0;
        }

        private void WritePacket(ControlMessageType controlMessageType, uint sequenceNumber, byte[] data, int dataLength, out byte[] packet)
        {
            int initialSendBufferNextWriteIndex = this._sendBufferNextWriteIndex;
            this._sendBuffer[this._sendBufferNextWriteIndex] = (byte)controlMessageType;
            this._sendBufferNextWriteIndex += sizeof(ControlMessageType);
            int packetLengthWriteIndex = this._sendBufferNextWriteIndex;
            this._sendBufferNextWriteIndex += sizeof(int);
            Array.Copy(BitConverter.GetBytes(sequenceNumber), 0, this._sendBuffer, this._sendBufferNextWriteIndex, sizeof(uint));
            this._sendBufferNextWriteIndex += sizeof(uint);
            if (!(data is null))
            {
                Array.Copy(data, 0, this._sendBuffer, this._sendBufferNextWriteIndex, dataLength);
            }
            int packetLength = this._sendBufferNextWriteIndex - initialSendBufferNextWriteIndex;
            Array.Copy(BitConverter.GetBytes(packetLength), 0, this._sendBuffer, packetLengthWriteIndex, sizeof(int));
            packet = new byte[packetLength];
            Array.Copy(this._sendBuffer, initialSendBufferNextWriteIndex, packet, 0, packetLength);
        }

        private void SendImmediately()
        {
            base.Send(this._sendBuffer, this.SendBufferDataLength);
            this.ResetSendBufferWriteIndex();
        }

        private void WritePacketAndSendImmediately(ControlMessageType controlMessageType, uint sequenceNumber, byte[] data, int dataLength, out byte[] packet)
        {
            this.WritePacket(controlMessageType, sequenceNumber, data, dataLength, out packet);
            this.SendImmediately();
        }

        public void Tick(float deltaSeconds)
        {
            if(this._reliableChannelState == ReliableChannelState.Connecting)
            {
                this._timerBeforeResetConnectionDuringConnecting -= deltaSeconds;
                if(this._timerBeforeResetConnectionDuringConnecting <= 0)
                {
                    this._reliableChannelState = ReliableChannelState.NotConnected;
                    this.Log("establishing connection has timed out");
                }
            }
            this._receiveBuffer = base.Receive();
            if(this._receiveBuffer is null)
            {
                return;
            }
            this._receiveBufferNextReadIndex = 0;
            int receivedLength = this._receiveBuffer.Length;
            while(this._receiveBufferNextReadIndex < receivedLength)
            {
                this.ProcessReceivedPacket();
                if(this._reliableChannelState == ReliableChannelState.NotConnected)
                {
                    break;
                }
            }
        }

        private void ProcessReceivedPacket()
        {
            if (this._reliableChannelState == ReliableChannelState.NotConnected)
            {
                this.WritePacketAndSendImmediately(ControlMessageType.ConnectionReset, 0, null, 0, out _);
                this.Log("connection is not established, force sender to reset connection");
                return;
            }
            ControlMessageType controlMessage = (ControlMessageType)this.ReadByteFromReceiveBuffer();
            int packetLength = this.ReadIntFromReceivedBuffer();
            uint sequenceNumber = this.ReadUIntFromReceivedBuffer();
            switch (controlMessage)
            {
                case ControlMessageType.ConnectionRequest:
                    this.ProcessConnectionRequestPacket();
                    break;
                case ControlMessageType.ConnectionResponse:
                    this.ProcessConnectionResponsePacket();
                    break;
                case ControlMessageType.ConnectionRequestThirdHandshake:
                    this.ProcessConnectionRequestThirdHandshakePacket();
                    break;
                case ControlMessageType.UserData:
                    int userDataLength = packetLength - this._receiveBufferNextReadIndex;
                    byte[] userData = new byte[userDataLength];
                    Array.Copy(this._receiveBuffer, this._receiveBufferNextReadIndex, userData, 0, userDataLength);
                    this.ProcessUserDataPacket(sequenceNumber, userData);
                    break;
                case ControlMessageType.Acknowledgement:
                    this.ProcessAcknowledgementPacket(sequenceNumber);
                    break;
                case ControlMessageType.ConnectionReset:
                    this.ProcessConnectionResetPacket();
                    break;
                default:
                    this.Log("unknown control message " + controlMessage);
                    break;
            }
        }

        private void ProcessConnectionRequestPacket()
        {
            this._sequenceNumber = 0;
            this._remoteReceivedInOrderPacketLastSequenceID = 0;
            this.WritePacketAndSendImmediately(ControlMessageType.ConnectionResponse, this._sequenceNumber, null, 0, out _);
            this.Log("received connection request");
            this._timerBeforeResetConnectionDuringConnecting = _connectionWaitDuration;
        }

        private void ProcessConnectionResponsePacket()
        {
            this._sequenceNumber = 0;
            this._remoteReceivedInOrderPacketLastSequenceID = 0;
            this.WritePacketAndSendImmediately(ControlMessageType.ConnectionRequestThirdHandshake, this._sequenceNumber, null, 0, out _);
            this._reliableChannelState = ReliableChannelState.Connected;
            this.Log("connection established (received response)");
        }

        private void ProcessConnectionRequestThirdHandshakePacket()
        {
            this._reliableChannelState = ReliableChannelState.Connected;
            this.Log("connection established (third handshake)");
        }

        private void ProcessUserDataPacket(uint sequenceNumber, byte[] userData)
        {
            this.WritePacketAndSendImmediately(ControlMessageType.Acknowledgement, sequenceNumber, null, 0, out _);
            if (!this._receivedPackets.ContainsKey(sequenceNumber))
            {

                this._receivedPackets.Add(sequenceNumber, new ReceivedDataPacket(userData, userData.Length, sequenceNumber));
                this.Log("received packet " + sequenceNumber + ", acknowledgement sent");
            }
            else
            {
                this.Log("received duplicated packet " + sequenceNumber + ", acknowledgement sent");
            }
        }

        private void ProcessAcknowledgementPacket(uint sequenceID)
        {
            this.Log("received acknowledgement of " + sequenceID);
            if (this._sentPackets.ContainsKey(sequenceID))
            {
                this._sentPackets.Remove(sequenceID);
            }
            uint expectedInOrderSequenceID = this._remoteReceivedInOrderPacketLastSequenceID + 1;
            if(expectedInOrderSequenceID == sequenceID)
            {
                uint i = expectedInOrderSequenceID;
                while (!this._sentPackets.ContainsKey(i + 1)) // packet already acknowledged, so the dictionary no longer contains it
                {
                    i++;
                    if(i == this._sequenceNumber)
                    {
                        break;
                    }
                }
                this._remoteReceivedInOrderPacketLastSequenceID = i;
                return;
            }
            if(sequenceID > expectedInOrderSequenceID)
            {
                SentDataPacket resentPacket = this._sentPackets[expectedInOrderSequenceID];
                this._sendBufferNextWriteIndex = resentPacket.packet.Length;
                Array.Copy(resentPacket.packet, 0, this._sendBuffer, 0, this._sendBufferNextWriteIndex);
                this.SendImmediately();
                this.Log("packet " + expectedInOrderSequenceID + " is missing acknowledgement, resending it");
            }
        }

        private void ProcessConnectionResetPacket()
        {
            this._reliableChannelState = ReliableChannelState.NotConnected;
            this.Log("remote has reset the connection");
        }

        private byte ReadByteFromReceiveBuffer()
        {
            byte result = this._receiveBuffer[this._receiveBufferNextReadIndex];
            this._receiveBufferNextReadIndex += sizeof(byte);
            return result;
        }

        private int ReadIntFromReceivedBuffer()
        {
            int result = BitConverter.ToInt32(this._receiveBuffer, this._receiveBufferNextReadIndex);
            this._receiveBufferNextReadIndex += sizeof(int);
            return result;
        }

        private uint ReadUIntFromReceivedBuffer()
        {
            uint result = BitConverter.ToUInt32(this._receiveBuffer, this._receiveBufferNextReadIndex);
            this._receiveBufferNextReadIndex += sizeof(uint);
            return result;
        }

        public void SendData(byte[] data)
        {
            this._sequenceNumber++;
            byte[] packet;
            this.WritePacketAndSendImmediately(ControlMessageType.UserData, this._sequenceNumber, data, data.Length, out packet);
            this._sentPackets.Add(this._sequenceNumber, new SentDataPacket(packet));
        }

        private enum ControlMessageType : byte
        {
            UserData = 0,
            ConnectionRequest = 1,
            ConnectionResponse = 2,
            ConnectionRequestThirdHandshake = 3,
            Acknowledgement = 4,
            ConnectionReset = 5,
            DisconnectionRequest = 6,
            DisconnectionResponse_Wait = 7,
            DisconnectionResponse_LastData = 8,
        }

        private class SentDataPacket
        {
            public readonly byte[] packet;
            public SentDataPacket(byte[] packet)
            {
                this.packet = packet;
            }
        }

        private class ReceivedDataPacket
        {
            public byte[] data;
            public readonly int dataLength;
            public readonly uint sequenceNumber;

            public ReceivedDataPacket(byte[] data, int dataLength, uint sequenceNumber)
            {
                this.data = data;
                this.dataLength = dataLength;
                this.sequenceNumber = sequenceNumber;
            }
        }
    }

    enum ReliableChannelState
    {
        NotConnected,
        ListeningForConnection,
        Connecting,
        Connected,
        Disconnecting,
    }
}
