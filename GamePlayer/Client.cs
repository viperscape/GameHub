using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GameNetwork
{
    class Client
    {
        public Dictionary<ushort, NetPlayer> players;
        public Stopwatch stopwatch { get; private set; }

        Unreliable unreliable;

        public Client (string host, int port)
        {
            players = new Dictionary<ushort, NetPlayer>();

            stopwatch = new Stopwatch();
            stopwatch.Start();
            IPHostEntry host_info = Dns.GetHostEntry(host);
            IPAddress ip;

            if (host_info.AddressList[0].AddressFamily != AddressFamily.InterNetworkV6)
                ip = host_info.AddressList[0];
            else
                ip = host_info.AddressList[1];

            Console.WriteLine("Client socket destination {0}", ip);

            unreliable = new Unreliable();
            IPEndPoint ep = new IPEndPoint(ip, port);
            NetPlayer np = new NetPlayer(0, ep, unreliable); // player id 0 is our server connection
            players.Add(0, np);
        }

        public void BeginUnreliable()
        {
            _ = unreliable.Read(AppendDatagrams);
        }

        void AppendDatagrams(IPEndPoint remote, Datagram datagram)
        {
            NetPlayer np;
            if (players.TryGetValue(datagram.playerId, out np))
            {
                np.Enqueue(datagram);
            }
        }

        public void AddPeer(ushort id, string host, int port)
        {
            IPAddress ip = IPAddress.Parse(host);
            IPEndPoint ep = new IPEndPoint(ip, port);
            NetPlayer np = new NetPlayer(id, ep, unreliable);
            players.Add(id, np);
        }

        public async Task WriteServer(Message msg, bool isReliable = true)
        {
            await players[0].Write(msg, players[0].id, isReliable);
        }
    }
}
