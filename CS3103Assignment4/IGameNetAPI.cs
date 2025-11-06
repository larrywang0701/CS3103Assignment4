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
        /// <summary>
        /// Is GameNet connected?
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Start listening for connections. (server)
        /// </summary>
        /// <param name="port">Port to listen for connections.</param>
        void ListenForConnection(ushort port);

        /// <summary>
        /// Connect to the server. (client)
        /// </summary>
        /// <param name="remoteHost">remote server IP address</param>
        /// <param name="port">remote server port</param>
        void Connect(string remoteHost, ushort port);

        /// <summary>
        /// Send data to the peer.
        /// </summary>
        /// <param name="channelType">the type of channel, either reliable or unreliable</param>
        /// <param name="timeStamp">the timestamp of the packet</param>
        /// <param name="data">user data of the packet</param>
        void Send(ChannelType channelType, long timeStamp, byte[] data);

        /// <summary>
        /// Tick the GameNet instance. You should call this method in every iteration of your game loop.
        /// </summary>
        /// <param name="deltaSeconds">How many seconds have elapsed since last tick?</param>
        void Tick(float deltaSeconds);

        /// <summary>
        /// Get the unreliable packets received after the previous get.
        /// </summary>
        /// <returns>An array of unreliable packets.</returns>
        byte[][] GetUnreliablePackets();

        /// <summary>
        /// Get the reliable packets that are ready for the user to use after the previous get.
        /// </summary>
        /// <returns>An array of ready relable packets.</returns>
        byte[][] GetReliablePackets();

        /// <summary>
        /// Disconnect from the peer.
        /// </summary>
        void Disconnect();
    }
}
