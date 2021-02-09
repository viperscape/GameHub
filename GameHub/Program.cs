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
        static Server server;

        static Dictionary<string, List<ushort>> gameAreas; // areas in the game with players present

        static async Task Main(string[] args)
        {
            gameAreas = new Dictionary<string, List<ushort>>();

            server = new Server(port);
            _ = HandleClients();
            await server.Listen(OnConnect, OnDisonnect);
        }

        static async Task OnConnect(ushort id) // send all game areas listed
        {
            Message msg = GetRawGameAreas();
            await server.WritePlayer(id, msg.GetRaw());
        }

        static Message GetRawGameAreas()
        {
            Message msg = new Message(Comm.GameAreasList);
            msg.AddInt(gameAreas.Count);
            foreach (var area in gameAreas.Keys)
            {
                msg.AddString(area);
            }

            return msg;
        }

        static async Task OnDisonnect(ushort id)
        {
            Message msg = new Message(Comm.Quit);
            msg.AddUShort(id);
            foreach (var player in server.players.Values)
            {
                await server.WritePlayer(player.id, msg.GetRaw());
            }

        }

        static async Task HandleClients()
        {
            while (true)
            {
                foreach (var player in server.players.Values)
                {
                    List<Datagram> datagrams = player.GetDatagrams();
                    foreach (var datagram in datagrams)
                    {
                        Message msg = new Message(datagram.data); // unwrap into a easy to use message type

                        if (msg.kind == Comm.Text)
                        {
                            Console.WriteLine("server msg {0} {1}", datagram.playerId, msg.GetString());
                        }
                        else if (msg.kind == Comm.Ping)
                        {
                            Message msg_ = new Message(Comm.Pong);
                            msg_.AddInt(msg.GetInt());
                            await server.WritePlayer(player.id, msg_.GetRaw());
                        }
                        else if (msg.kind == Comm.RequestGameAreas)
                        {
                            Message msg_ = GetRawGameAreas();
                            await server.WritePlayer(player.id, msg_.GetRaw());
                        }
                        else if (msg.kind == Comm.JoinGameArea)
                        {
                            string area = msg.GetString();
                            Console.WriteLine("join request {0} {1}", datagram.playerId, area);
                            List<ushort> ids;
                            gameAreas.TryGetValue(area, out ids);
                            if (ids != null)
                            {
                                foreach (var id in ids)
                                {
                                    RemotePlayer p;
                                    server.players.TryGetValue(id, out p);
                                    if (p != null)
                                    {
                                        Message msg_ = new Message(Comm.BrokerNewMember);
                                        if (p.udpEndpoint != null) // grab all upd connections and share with new player
                                        {
                                            msg_.AddUShort(p.id);
                                            msg_.AddString(p.udpEndpoint.Address.ToString());
                                            msg_.AddInt(p.udpEndpoint.Port);
                                            await server.WritePlayer(player.id, msg_.GetRaw());
                                        } // we should dump players without established udp endpoints

                                        if (player.udpEndpoint != null) // share new player udp with all existing players
                                        {
                                            msg_ = new Message(Comm.BrokerNewMember);
                                            msg_.AddUShort(player.id);
                                            msg_.AddString(player.udpEndpoint.Address.ToString());
                                            msg_.AddInt(player.udpEndpoint.Port);
                                            await server.WritePlayer(p.id, msg_.GetRaw());
                                        }
                                    }
                                }

                                ids.Add(player.id);
                            }
                            else
                            {
                                List<ushort> li = new List<ushort>();
                                li.Add(player.id);
                                gameAreas.Add(area, li);
                            }
                        }
                        else
                        {
                            foreach (var player_ in server.players.Values) // broadcasting to all players
                            {
                                //await server.WritePlayer(player_.id, datagram.data); // basic broadcast
                            }
                        }

                    }
                }

                await Task.Delay(10);
            }
        }
    }
}
