using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS3103Assignment4
{
    class ReliableChannel : Channel
    {
        private ReliableChannelState _reliableChannelState;
        public bool IsConnected { get { return this._reliableChannelState == ReliableChannelState.Connected || this._reliableChannelState == ReliableChannelState.Disconnecting; } }
        public bool IsDisconnecting { get { return this._reliableChannelState == ReliableChannelState.Disconnecting; } }
        private const int _sendBufferLength = 1024;

        private byte[] _sendBuffer;
        private int _sendBufferNextWriteIndex = 0;
        private int SendBufferDataLength { get { return this._sendBufferNextWriteIndex; } }

        // the reliable channel's internal sequence number
        private uint _sequenceNumber;

        private byte[] _receiveBuffer;
        private int _receiveBufferNextReadIndex;

        private List<byte[]> _readyData = new List<byte[]>();

        private uint _selfReceivedInOrderPacketLastSequenceID;
        private uint _remoteReceivedInOrderPacketLastSequenceID;

        private float _timerBeforeResetConnectionDuringConnecting;
        private float _connectionWaitDuration = 0.5f;

        private Dictionary<uint, SentDataPacket> _sentPackets = new Dictionary<uint, SentDataPacket>();
        private Dictionary<uint, ReceivedDataPacket> _receivedPackets = new Dictionary<uint, ReceivedDataPacket>();

        private Dictionary<uint, float> _missingPacketTimers = new Dictionary<uint, float>();
        private const float MissingPacketTimeout = 0.2f;

        private bool _toldRemoteThatSelfHaveNoDataDuringDisconnecting;

        public void ListenForConnection()
        {
            this._sendBuffer = new byte[_sendBufferLength];
            this._reliableChannelState = ReliableChannelState.ListeningForConnection;
        }

        public void Connect()
        {
            this._reliableChannelState = ReliableChannelState.Connecting;
            this._sendBuffer = new byte[_sendBufferLength];
            this.ResetSendBufferWriteIndex();
            this.WriteControlMessagePacketAndSendImmediately(ControlMessageType.ConnectionRequest, this._sequenceNumber, out _);
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
                this._sendBufferNextWriteIndex += dataLength;
            }
            int packetLength = this._sendBufferNextWriteIndex - initialSendBufferNextWriteIndex;
            Array.Copy(BitConverter.GetBytes(packetLength), 0, this._sendBuffer, packetLengthWriteIndex, sizeof(int));
            packet = new byte[packetLength];
            Array.Copy(this._sendBuffer, initialSendBufferNextWriteIndex, packet, 0, packetLength);
        }

        private void SendImmediately()
        {
            this.Sender(this._sendBuffer.Take(this.SendBufferDataLength).ToArray(), this.SendBufferDataLength);
            this.ResetSendBufferWriteIndex();
        }

        private void WriteControlMessagePacketAndSendImmediately(ControlMessageType controlMessageType, uint sequenceNumber, out byte[] packet)
        {
            this.WritePacket(controlMessageType, sequenceNumber, null, 0, out packet);
            this.SendImmediately();
        }

        public void Tick(float deltaSeconds)
        {
            if (this._reliableChannelState == ReliableChannelState.Connecting)
            {
                this._timerBeforeResetConnectionDuringConnecting -= deltaSeconds;
                if (this._timerBeforeResetConnectionDuringConnecting <= 0)
                {
                    this._reliableChannelState = ReliableChannelState.NotConnected;
                    this.Log("establishing connection has timed out");
                }
            }

            var keys = _missingPacketTimers.Keys.ToList();
            foreach (var seq in keys)
            {
                _missingPacketTimers[seq] += deltaSeconds;
                if(seq < this._selfReceivedInOrderPacketLastSequenceID)
                {
                    this._missingPacketTimers.Remove(seq);
                }
            }

            TryProcessInOrderPackets();
        }

        public override void OnReceivedData(byte[] data)
        {
            this._receiveBuffer = data;
            if (this._receiveBuffer is null)
            {
                return;
            }
            int receivedLength = this._receiveBuffer.Length;
            this._receiveBufferNextReadIndex = 0;
            while (this._receiveBufferNextReadIndex < receivedLength)
            {
                this.ProcessReceivedPacket();
                if (this._reliableChannelState == ReliableChannelState.NotConnected)
                {
                    break;
                }
            }
        }

        private void ProcessReceivedPacket()
        {
            if (this._reliableChannelState == ReliableChannelState.NotConnected)
            {
                this.WriteControlMessagePacketAndSendImmediately(ControlMessageType.ConnectionReset, 0, out _);
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
                    this._receiveBufferNextReadIndex += userDataLength;
                    this.ProcessUserDataPacket(sequenceNumber, userData);
                    break;
                case ControlMessageType.Acknowledgement:
                    this.ProcessAcknowledgementPacket(sequenceNumber);
                    break;
                case ControlMessageType.ConnectionReset:
                    this.ProcessConnectionResetPacket();
                    break;
                case ControlMessageType.Disconnection_Wait:
                    this.ProcessDisconnection(true);
                    break;
                case ControlMessageType.Disconnection_AllDataReceived:
                    this.ProcessDisconnection(false);
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
            this.WriteControlMessagePacketAndSendImmediately(ControlMessageType.ConnectionResponse, this._sequenceNumber, out _);
            this.Log("received connection request");
            this._timerBeforeResetConnectionDuringConnecting = _connectionWaitDuration;
        }

        private void ProcessConnectionResponsePacket()
        {
            this._sequenceNumber = 0;
            this._remoteReceivedInOrderPacketLastSequenceID = 0;
            this.WriteControlMessagePacketAndSendImmediately(ControlMessageType.ConnectionRequestThirdHandshake, this._sequenceNumber, out _);
            this._reliableChannelState = ReliableChannelState.Connected;
            this.Log("connection established (received response)");
        }

        private void ProcessConnectionRequestThirdHandshakePacket()
        {
            this._reliableChannelState = ReliableChannelState.Connected;
            this.Log("connection established (third handshake)");
        }

        public void StartDisconnecting()
        {
            if (this._reliableChannelState == ReliableChannelState.NotConnected)
            {
                return;
            }
            bool selfHasUnreceivedData = this.HasUnreceivedData();
            ControlMessageType controlMessageType = selfHasUnreceivedData ? ControlMessageType.Disconnection_Wait : ControlMessageType.Disconnection_AllDataReceived;
            this._toldRemoteThatSelfHaveNoDataDuringDisconnecting = !selfHasUnreceivedData;
            this.WriteControlMessagePacketAndSendImmediately(controlMessageType, this._sequenceNumber, out _);
            this._reliableChannelState = ReliableChannelState.Disconnecting;
            this.Log("start disconnecting, self have unreceived data = " + selfHasUnreceivedData);
        }

        private void ProcessDisconnection(bool wait)
        {
            bool selfHasUnreceivedData = this.HasUnreceivedData();
            if (this._reliableChannelState == ReliableChannelState.Connected)
            {
                ControlMessageType controlMessageType = selfHasUnreceivedData ? ControlMessageType.Disconnection_Wait : ControlMessageType.Disconnection_AllDataReceived;
                this.WriteControlMessagePacketAndSendImmediately(controlMessageType, this._sequenceNumber, out _);
            }
            this._reliableChannelState = ReliableChannelState.Disconnecting;
            if (wait || selfHasUnreceivedData)
            {
                this.Log("received Disconnection_Wait, will actually disconnect after all remaining data are ready on both sides. sequence number = " + this._sequenceNumber);
            }
            else
            {
                this._reliableChannelState = ReliableChannelState.NotConnected;
                this.Log("[Disconnection Completed] received Disconnection_AllDataReceived, and self also have no unreceived data. sequence number = " + this._sequenceNumber);
                this.WriteControlMessagePacketAndSendImmediately(ControlMessageType.Disconnection_AllDataReceived, this._sequenceNumber, out _);
            }
        }

        private void ProcessUserDataPacket(uint sequenceNumber, byte[] userData)
        {
            this.WriteControlMessagePacketAndSendImmediately(ControlMessageType.Acknowledgement, sequenceNumber, out _);

            if (!_receivedPackets.ContainsKey(sequenceNumber) && sequenceNumber >= this._selfReceivedInOrderPacketLastSequenceID)
            {
                _receivedPackets.Add(sequenceNumber, new ReceivedDataPacket(userData, userData.Length, sequenceNumber));
                this.Log("received packet " + sequenceNumber + ", acknowledgment sent");
            }
            else
            {
                this.Log("received duplicate packet " + sequenceNumber + ", acknowledgement sent");
            }

            uint expectedSeq = _selfReceivedInOrderPacketLastSequenceID + 1;
            for (uint i = expectedSeq; i < sequenceNumber; i++)
            {
                if (!_receivedPackets.ContainsKey(i) & !_missingPacketTimers.ContainsKey(i))
                {
                    _missingPacketTimers[i] = 0f;
                }
            }

            TryProcessInOrderPackets();
        }

        private void TryProcessInOrderPackets()
        {
            uint nextSeq = _selfReceivedInOrderPacketLastSequenceID + 1;
            while (true)
            {
                if (_receivedPackets.ContainsKey(nextSeq))
                {
                    var packet = _receivedPackets[nextSeq];
                    _receivedPackets.Remove(nextSeq);
                    _readyData.Add(packet.data);
                    _selfReceivedInOrderPacketLastSequenceID = nextSeq;
                    nextSeq++;
                }
                else if (_missingPacketTimers.ContainsKey(nextSeq) && _missingPacketTimers[nextSeq] >= MissingPacketTimeout)
                {
                    this.Log("skipping missing packet " + nextSeq + " after timeout");
                    _missingPacketTimers.Remove(nextSeq);
                    _selfReceivedInOrderPacketLastSequenceID = nextSeq;
                    nextSeq++;
                }
                else
                {
                    break;
                }
            }
            if(this._reliableChannelState == ReliableChannelState.Disconnecting && !this.HasUnreceivedData() && !this._toldRemoteThatSelfHaveNoDataDuringDisconnecting)
            {
                this.WriteControlMessagePacketAndSendImmediately(ControlMessageType.Disconnection_AllDataReceived, this._sequenceNumber, out _);
                this._toldRemoteThatSelfHaveNoDataDuringDisconnecting = true;
                this.Log("all data on self side already received, will tell remote about this. sequence number = " + this._sequenceNumber);
            }
        }

        private bool HasUnreceivedData()
        {
            return this._receivedPackets.Count > 0 || this._missingPacketTimers.Count > 0;
        }

        private void ProcessAcknowledgementPacket(uint sequenceID)
        {
            this.Log("received acknowledgement of " + sequenceID);
            if (this._sentPackets.ContainsKey(sequenceID))
            {
                this._sentPackets.Remove(sequenceID);
            }
            uint expectedInOrderSequenceID = this._remoteReceivedInOrderPacketLastSequenceID + 1;
            if (expectedInOrderSequenceID == sequenceID)
            {
                uint i = expectedInOrderSequenceID;
                while (!this._sentPackets.ContainsKey(i + 1)) // packet already acknowledged, so the dictionary no longer contains it
                {
                    i++;
                    if (i >= this._sequenceNumber)
                    {
                        break;
                    }
                }
                this._remoteReceivedInOrderPacketLastSequenceID = i;
                return;
            }
            if (sequenceID > expectedInOrderSequenceID)
            {
                if (_sentPackets.TryGetValue(expectedInOrderSequenceID, out var resentPacket))
                {
                    if (resentPacket.retransmissionCount < 2)
                    {
                        resentPacket.retransmissionCount++;
                        this._sendBufferNextWriteIndex = resentPacket.packet.Length;
                        Array.Copy(resentPacket.packet, 0, this._sendBuffer, 0, this._sendBufferNextWriteIndex);
                        SendImmediately();
                        this.Log("packet " + expectedInOrderSequenceID + " is missing acknowledgement resending it (attempt " + resentPacket.retransmissionCount + ")");
                    }
                    else
                    {
                        _sentPackets.Remove(expectedInOrderSequenceID);
                        this.Log("packet " + expectedInOrderSequenceID + " failed to be acknowledged after 2 retransmissions, giving up");
                        _remoteReceivedInOrderPacketLastSequenceID = expectedInOrderSequenceID;
                    }
                }
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

        public byte[] GetEncapsulatedDataToSend(byte[] data)
        {
            if(this._reliableChannelState != ReliableChannelState.Connected)
            {
                throw new InvalidOperationException("You cannot send data in the connection state " + this._reliableChannelState);
            }
            this._sequenceNumber++;
            byte[] packet;
            this.WritePacket(ControlMessageType.UserData, this._sequenceNumber, data, data.Length, out packet);
            this._sentPackets.Add(this._sequenceNumber, new SentDataPacket(packet));
            this.ResetSendBufferWriteIndex();
            return packet;
        }

        public override byte[][] GetPackets()
        {
            byte[][] packets = this._readyData.ToArray();
            this._readyData.Clear();
            return packets;
        }

        private enum ControlMessageType : byte
        {
            UserData = 0,
            ConnectionRequest = 1,
            ConnectionResponse = 2,
            ConnectionRequestThirdHandshake = 3,
            Acknowledgement = 4,
            ConnectionReset = 5,
            Disconnection_Wait = 6,
            Disconnection_AllDataReceived = 7,
        }

        private class SentDataPacket
        {
            public readonly byte[] packet;
            public int retransmissionCount;
            public SentDataPacket(byte[] packet)
            {
                this.packet = packet;
                this.retransmissionCount = 0;
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


