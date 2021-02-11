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

        async Task LinkPlayer(IPEndPoint remote, Datagram datagram)
        {
            NetPlayer player;
            if (players.TryGetValue(datagram.playerId, out player))
            {
                await player.Enqueue(datagram);
            }
            else if (datagram.playerId == 0)
            { // before a player finalized the player id with the server the first few packets will need assignment manually
                ushort id = 0;

                foreach (var kv in players)
                {
                    if (remote.Equals(kv.Value.endpoint))
                    {
                        id = kv.Key;
                        break;
                    }
                }

                if (id == 0)
                {
                    bool added = false;
                    player = new NetPlayer(id, remote, unreliable);


                    while (!added)
                    {
                        id = (ushort)rand.Next(65000);
                        player.id = id;
                        added = players.TryAdd(id, player);
                    }
                }

                await players[id].Enqueue(datagram);
            }
        }
    }
}
