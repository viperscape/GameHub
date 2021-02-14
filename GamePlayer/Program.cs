using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace GameNetwork
{
    class Program
    {
        static int port = 7070;
        static string host = "localhost"; //"44.234.72.181";
        static async Task Main(string[] args)
        {
            Client client = new Client(host, port);
            await Start(client, HandleServer, HandlePlayers);
        }

        static async Task HandleServer(Datagram datagram)
        {
            Message msg = new Message(datagram.data);
            if (msg.kind == Comm.GameAreasList)
            {
                int count = msg.GetInt();
                for (; count > 0; count--)
                {
                    string area = msg.GetString();
                    Console.WriteLine("area {0}", area);
                }
            }
            else if (msg.kind == Comm.JoinGameArea)
            {
                bool joined = msg.GetBool();
                if (joined) Console.WriteLine("joined area {0}", joined);
                else Console.WriteLine("joined area {0}", joined, msg.GetString());
            }
        }

            static async Task HandlePlayers(Datagram datagram)
        {
            Message msg = new Message(datagram.data);
            if (msg.kind == Comm.Text)
                Console.WriteLine("MSG {0} player {1}, says: {2}", datagram.packetId, datagram.playerId, msg.GetString());
            else
            {
                Console.WriteLine("other datagram type {0} {1}", datagram.playerId, msg.kind);
            }
        }

        static async Task Start(Client client, Func<Datagram, Task> servercb, Func<Datagram, Task> playercb, int delay = 0)
        {
            await Task.Delay(delay);

            try
            {
                await client.Start();

                Message msg = new Message(Comm.PlayerUuid);
                msg.AddString(client.uuid);
                await client.WriteServer(msg);

                msg = new Message(Comm.RequestPlayerId);
                await client.WriteServer(msg);

                while (true)
                {
                    await Task.Delay(50); // wait for 20 pps roughly

                    // loop through the server messages
                    var server = client.players[0];
                    await server.SendReliables();

                    List<Datagram> datagrams = server.GetDatagrams();
                    foreach (var datagram in datagrams)
                    {
                        if (datagram.playerId != 0) continue;

                        msg = new Message(datagram.data);
                        if (msg.kind == Comm.AssignPlayerId)
                        {
                            if (client.id == 0) // only change if we never had an id
                            {
                                client.id = msg.GetUShort();
                                Console.WriteLine("recv id {0}", client.id);
                            }
                        }
                        else if (msg.kind == Comm.BrokerNewMember)
                        {
                            ushort id = msg.GetUShort();
                            string address = msg.GetString();
                            int port = msg.GetInt();
                            Console.WriteLine("udp endpoint {0} {1} {2}", id, address, port);
                            client.AddPeer(id, address, port);
                        }

                        await servercb(datagram);
                    }



                    // loop through connected players messages
                    if (client.players.Count < 2) continue;
                    ushort[] player_keys = new ushort[client.players.Keys.Count];
                    client.players.Keys.CopyTo(player_keys, 0);
                    foreach (var key in player_keys)
                    {
                        if (key == 0) continue; // skip server key

                        NetPlayer player;
                        if (!client.players.TryGetValue(key, out player)) continue;

                        if (player.shouldDrop) // NOTE we may want to process the final datagrams before dropping player...
                        {
                            client.players.Remove(player.id);
                            continue;
                        }

                        await player.SendReliables();

                        datagrams = player.GetDatagrams();
                        foreach (var datagram in datagrams)
                        {
                            if (datagram.playerId == 0) continue; // ignore unknown player commands

                            await playercb(datagram);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERR {0}", e.ToString());
            }
        }
    }
}
