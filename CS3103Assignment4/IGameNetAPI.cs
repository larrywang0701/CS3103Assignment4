/*
 * Author: Wang Zihan (A0266073A)
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS3103Assignment4
{
    interface IGameNetAPI
    {
        bool IsConnected { get; }
        void ListenForConnection(ushort port);
        void Connect(string remoteHost, ushort port);
        void Send(ChannelType channelType, long timeStamp, byte[] data);
        void Tick(float deltaSeconds);
        byte[][] GetUnreliablePackets();
        byte[][] GetReliablePackets();
        void Disconnect();
    }
}
