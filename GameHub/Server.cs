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

        public Dictionary<ushort, RemotePlayer> players;

        public Stopwatch stopwatch { get; private set; }

        Unreliable unreliable;

        Random rand = new Random();

        public Server(int port_)
        {
            port = port_;
            players = new Dictionary<ushort, RemotePlayer>();
            stopwatch = new Stopwatch();
            stopwatch.Start();

            unreliable = new Unreliable(port);
            _ = unreliable.Read(LinkPlayer);
        }

        void LinkPlayer(IPEndPoint remote, Datagram datagram)
        {
            RemotePlayer player;
            if (players.TryGetValue(datagram.playerId, out player))
            {
                player.datagrams.Enqueue(datagram);
            }
            else if (datagram.playerId == 0)
            {
                foreach (var p in players.Values)
                {
                    if (remote.Equals(p.Endpoint))
                    {
                        p.datagrams.Enqueue(datagram);
                        return;
                    }
                }

                bool added = false;
                while (!added)
                {
                    ushort id = (ushort)rand.Next(65000);
                    player = new RemotePlayer(id, remote);
                    added = players.TryAdd(id, player);
                }
            }
        }


        public async Task WritePlayer(ushort id, byte[] data, bool reliable = true)
        {
            RemotePlayer player;
            if (players.TryGetValue(id, out player))
            {
                await player.Write(data, (uint) stopwatch.ElapsedMilliseconds, unreliable);
            }
        }
    }
}
