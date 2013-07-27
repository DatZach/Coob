﻿using Coob.Exceptions;
using Coob.Packets;
using Coob.Structures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Coob.CoobEventArgs;

namespace Coob
{
    public class Client
    {
        public bool Joined;
        public NetReader Reader;
        public BinaryWriter Writer;
        public NetworkStream NetStream;
        public ulong ID {get; private set;}
        public Entity Entity;
        public string IP;
        public Coob Coob { get; private set; }
        public bool PVP;
        private bool disconnecting;

        TcpClient tcp;
        byte[] recvBuffer;

        public Client(TcpClient tcpClient, Coob coob)
        {
            Joined = false;
            Entity = null;
            disconnecting = false;
            tcp = tcpClient;
            IP = (tcp.Client.RemoteEndPoint as IPEndPoint).Address.ToString();
            NetStream = tcp.GetStream();
            Reader = new NetReader(NetStream);
            Writer = new BinaryWriter(NetStream);
            Coob = coob;

            ID = Coob.CreateID();

            if (ID == 0)
            {
                throw new UserLimitReachedException();
            }

            recvBuffer = new byte[4];
            NetStream.BeginRead(recvBuffer, 0, 4, idCallback, null);
        }

        void idCallback(IAsyncResult result)
        {
            if (!tcp.Connected)
            {
                Disconnect("Connection reset by peer.");
                return;
            }
            if (disconnecting)
                return;
            int bytesRead = 0;
            try
            {
                bytesRead = NetStream.EndRead(result);

                if (bytesRead == 4)
                {
                    Coob.HandleRecvPacket(BitConverter.ToInt32(recvBuffer, 0), this);
                }
                NetStream.BeginRead(recvBuffer, 0, 4, idCallback, null);
            }
            catch { Disconnect("Read error"); }
        }

        public void Disconnect(string reason = "")
        {
            Joined = false;
            disconnecting = true;
            tcp.Close();

            if (Coob.Clients.ContainsKey(ID))
            {
                var client = Coob.Clients[ID];
                Coob.Clients.Remove(ID);

                Entity removedEntity;
                if (!Coob.World.Entities.TryRemove(this.ID, out removedEntity))
                {
                    throw new NotImplementedException("Failed to remove entity from Entities");
                }

                Root.ScriptManager.CallEvent("OnClientDisconnect", new ClientDisconnectEventArgs(client, reason));
            }
        }

        public void SendMessage(ulong id, string message)
        {
            byte[] msgBuffer = Encoding.Unicode.GetBytes(message);
            int msgLength = msgBuffer.Length / 2;

            Writer.Write(SCPacketIDs.ServerChatMessage);
            Writer.Write(id);
            Writer.Write(msgLength);
            Writer.Write(msgBuffer);
        }

        public void SendServerMessage(string message)
        {
            SendMessage(0, message);
        }

        /// <summary>
        /// Sets the current day and time for the client.
        /// </summary>
        /// <param name="day">The current day (not sure what use this has).</param>
        /// <param name="time">The elapsed hours in 0-24 range.</param>
        public void SetTime(uint day, float time)
        {
            Writer.Write(SCPacketIDs.CurrentTime);
            Writer.Write(day);
            Writer.Write((uint)(60f * 60f * time * 1000f));
        }
    }
}
