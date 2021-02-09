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
        public IPEndPoint udpEndpoint { get; set; }


        public RemotePlayer(TcpClient client_, ushort id_) // server based creation
        {
            remote = client_;
            id = id_;
            
        }

        public async Task Write(byte[] data, uint time, Unreliable unreliable = null)
        {
            Datagram datagram = new Datagram(id, time, data);
            await WriteDatagram(datagram, unreliable);
        }
    }
}
