using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GameNetwork
{
    class Server
    {
        private int port;

        public Dictionary<ushort, NetPlayer> players;

        public Stopwatch stopwatch { get; private set; }

        Unreliable unreliable;

        Random rand = new Random();

        public Server(int port_)
        {
            port = port_;
            players = new Dictionary<ushort, NetPlayer>();
            stopwatch = new Stopwatch();
            stopwatch.Start();

            unreliable = new Unreliable(port);
            _ = unreliable.Read(LinkPlayer);
        }

        void LinkPlayer(IPEndPoint remote, Datagram datagram)
        {
            NetPlayer player;
            if (players.TryGetValue(datagram.playerId, out player))
            {
                player.Enqueue(datagram);
            }
            else if (datagram.playerId == 0)
            {
                foreach (var p in players.Values)
                {
                    if (remote.Equals(p.endpoint))
                    {
                        p.Enqueue(datagram);
                        return;
                    }
                }

                bool added = false;
                while (!added)
                {
                    ushort id = (ushort)rand.Next(65000);
                    player = new NetPlayer(id, remote, unreliable);
                    added = players.TryAdd(id, player);
                }
            }
        }
    }
}
