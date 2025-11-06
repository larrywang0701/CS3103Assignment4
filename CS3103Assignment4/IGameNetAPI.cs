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
        void Connect(string remoteHost, ushort port);
        void ReliableSend(byte[] data);
        void UnreliableSend(byte[] data);
        /// <summary>
        /// Trigger receive all UDP data from the reliable channel, process internal packets, and put application data that are ready into the receive buffer.<br/>
        /// You can call this method in the game loop to receive data.
        /// </summary>
        void ReliableReceive();
        /// <summary>
        /// Get data in receive buffer.
        /// </summary>
        /// <param name="buffer"></param>
        void ReliableGetData(byte[] buffer);
        void UnreliableReceive(byte[] buffer);
        void Disconnect();
    }
}
