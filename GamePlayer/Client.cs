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
        public string uuid = Guid.NewGuid().ToString();
        public ushort id; // network player session id

        public Client (string host, int port)
        {
            players = new Dictionary<ushort, NetPlayer>();
            unreliable = new Unreliable();
            stopwatch = new Stopwatch();
            stopwatch.Start();

            IPHostEntry host_info = Dns.GetHostEntry(host);
            IPAddress ip;

            if (host_info.AddressList[0].AddressFamily != AddressFamily.InterNetworkV6)
                ip = host_info.AddressList[0];
            else
                ip = host_info.AddressList[1];

            Console.WriteLine("Client socket destination {0}", ip);

            AddPeer(0, ip.ToString(), port);  // player id 0 is our server connection
        }

        public async Task Start()
        {
            await WriteServer(new Message(0), false);
            _ = unreliable.Read(AppendDatagrams);
        }

        async Task AppendDatagrams(IPEndPoint remote, Datagram datagram)
        {
            NetPlayer np;
            if (players.TryGetValue(datagram.playerId, out np))
            {
                await np.Enqueue(datagram);
            }
            else // find by endpoint instead FIXME this is because we are peering connections and the id isn't matching properly
            {
                ushort id = 0;
                foreach (var kv in players)
                {
                    if (remote.Equals(kv.Value.endpoint))
                    {
                        id = kv.Key;
                        break;
                    }
                }

                if (players.TryGetValue(id, out np))
                {
                    await np.Enqueue(datagram);
                }
            }
        }

        public void AddPeer(ushort id, string hostIP, int port)
        {
            IPAddress ip = IPAddress.Parse(hostIP);
            IPEndPoint ep = new IPEndPoint(ip, port);
            NetPlayer np = new NetPlayer(id, ep, unreliable);
            if (players.ContainsKey(id)) // already known player, maybe reconnecting from new endpoint
            {
                players[id].ResetStats();
                players[id].endpoint = ep;
            }
            else 
                players.Add(id, np);
        }

        public async Task WriteServer(Message msg, bool isReliable = true)
        {
            await players[0].Write(msg, id, isReliable);
        }

        public async Task WritePlayer(ushort toId, Message msg, bool isReliable = true)
        {
            await players[toId].Write(msg, id, isReliable);
        }
    }
}
