using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GameNetwork
{
    class RemotePlayer : NetPlayer
    {
        public IPEndPoint Endpoint { get; set; }


        public RemotePlayer(ushort id_, IPEndPoint ep) // server based creation
        {
            id = id_;
            Endpoint = ep;
        }

        public async Task Write(byte[] data, uint time, Unreliable unreliable)
        {
            Datagram datagram = new Datagram(id, time, data);
            await WriteDatagram(datagram, unreliable, Endpoint);
        }
    }
}
