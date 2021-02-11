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
            await TestClient();
        }


        static async Task TestClient(int delay = 0) // simulate client
        {
            await Task.Delay(delay);

            try
            {
                Client client = new Client(host, port);
                await client.Start();

                Message msg = new Message(Comm.PlayerUuid);
                msg.AddString(client.uuid);
                await client.WriteServer(msg);

                msg = new Message(Comm.RequestPlayerId);
                await client.WriteServer(msg);

                msg = new Message(Comm.JoinGameArea);
                msg.AddString("test");
                await client.WriteServer(msg);


                while (true)
                {
                    ushort[] player_keys = new ushort[client.players.Keys.Count];
                    client.players.Keys.CopyTo(player_keys, 0);
                    foreach (var key in player_keys)
                    {
                        NetPlayer player;
                        if (!client.players.TryGetValue(key, out player)) continue;

                        await player.SendReliables();

                        List<Datagram> datagrams = player.GetDatagrams();
                        foreach (var datagram in datagrams)
                        {
                            msg = new Message(datagram.data);
                            
                            //Console.WriteLine("{0} {1}", msg.kind, datagram.packetId);
                            
                            if (msg.kind == Comm.Text)
                                Console.WriteLine("MSG {0} {1}", datagram.playerId, msg.GetString());
                            else if (msg.kind == Comm.AssignPlayerId)
                            {
                                if (player.id == 0) // only change if we never had an id
                                {
                                    player.id = msg.GetUShort();
                                    Console.WriteLine("recv id {0}", player.id);
                                }
                            }
                            else if (msg.kind == Comm.GameAreasList)
                            {
                                int count = msg.GetInt();
                                for (; count > 0; count--)
                                {
                                    string area = msg.GetString();
                                    Console.WriteLine("area {0}", area);
                                }
                            }
                            else if (msg.kind == Comm.DenyJoinGameArea)
                                Console.WriteLine("Deny {0}", msg.GetString());
                            else if (msg.kind == Comm.BrokerNewMember)
                            {
                                ushort id = msg.GetUShort();
                                string address = msg.GetString();
                                int port = msg.GetInt();
                                Console.WriteLine("udp endpoint {0} {1} {2}", id, address, port);
                                client.AddPeer(id, address, port);
                            }
                            else if (msg.kind == Comm.Quit)
                            {
                                ushort id = msg.GetUShort();
                                client.players.Remove(id);
                            }
                            else
                            {
                                Console.WriteLine("other datagram type {0}", datagram.playerId);
                            }
                        }
                    }


                    await Task.Delay(50); // wait for 20 pps roughly
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERR {0}", e.ToString());
            }
        }
    }
}
