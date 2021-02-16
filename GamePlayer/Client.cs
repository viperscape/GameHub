using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameNetwork
{
    class Client
    {
        public Dictionary<ushort, NetPlayer> players;
        public Stopwatch stopwatch { get; private set; }

        public Unreliable unreliable { get; private set; }
        public string uuid = Guid.NewGuid().ToString();
        public ushort id; // network player session id

        public string localIP;

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

            // get our local address that is used to access the internet, we'll use this for fallback
            // NOTE this is a temporary connection just to discover the ip
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect(host, port);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }
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
                if (np.endpoint != remote)
                    np.endpoint = remote; // update endpoint if needed NOTE this is easily exploitable
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

        public void AddPeer(ushort id, string hostIP, int port, string altIP = null)
        {
            IPAddress ip = IPAddress.Parse(hostIP);
            IPEndPoint ep = new IPEndPoint(ip, port);
            NetPlayer np = new NetPlayer(id, ep, unreliable);
            np.altIP = altIP;
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

        public async Task WritePlayers(Message msg, bool isReliable = true)
        {
            foreach (var player in players.Values)
            {
                if (player.id != 0)
                    await player.Write(msg, id, isReliable);
            }
        }
    }
}
