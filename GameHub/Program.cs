﻿using System;
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
            await HandleClients();
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

        static async Task HandleClients()
        {
            while (true)
            {
                //Console.Write(".");

                foreach (var player in server.players.Values)
                {
                    await player.SendReliables();

                    List<Datagram> datagrams = player.GetDatagrams();
                    foreach (var datagram in datagrams)
                    {
                        Message msg = new Message(datagram.data); // unwrap into a easy to use message type

                        //Console.WriteLine("{0} {1}", msg.kind, datagram.packetId);

                        if (msg.kind == Comm.Text)
                        {
                            Console.WriteLine("server msg {0} {1}", datagram.playerId, msg.GetString());
                        }
                        else if (msg.kind == Comm.RequestPlayerId)
                        {
                            Message msg_ = new Message(Comm.AssignPlayerId);
                            msg_.AddUShort(player.id);
                            await player.Write(msg_);
                        }
                        else if (msg.kind == Comm.PlayerUuid)
                        {
                            player.uuid = msg.GetString();
                        }
                        else if (msg.kind == Comm.Ping)
                        {
                            Message msg_ = new Message(Comm.Pong);
                            msg_.AddInt(msg.GetInt());
                            await player.Write(msg_);
                        }
                        else if (msg.kind == Comm.RequestGameAreas)
                        {
                            Message msg_ = GetRawGameAreas();
                            await player.Write(msg_);
                        }
                        else if (msg.kind == Comm.JoinGameArea)
                        {
                            string area = msg.GetString();

                            if (player.id == 0)
                            {
                                Message msg_ = new Message(Comm.DenyJoinGameArea);
                                msg.AddString("Invalid player id");
                                await player.Write(msg_);
                                continue;
                            }

                            List<ushort> ids;
                            gameAreas.TryGetValue(area, out ids);
                            if (ids != null)
                            {
                                foreach (var id in ids)
                                {
                                    NetPlayer p;
                                    server.players.TryGetValue(id, out p);
                                    if (p != null)
                                    {
                                        Message msg_ = new Message(Comm.BrokerNewMember);
                                        if (p.endpoint != null) // grab all upd connections and share with new player
                                        {
                                            msg_.AddUShort(p.id);
                                            msg_.AddString(p.endpoint.Address.ToString());
                                            msg_.AddInt(p.endpoint.Port);
                                            await player.Write(msg_);
                                        } // we should dump players without established udp endpoints

                                        if (player.endpoint != null) // share new player udp with all existing players
                                        {
                                            msg_ = new Message(Comm.BrokerNewMember);
                                            msg_.AddUShort(player.id);
                                            msg_.AddString(player.endpoint.Address.ToString());
                                            msg_.AddInt(player.endpoint.Port);
                                            await player.Write(msg_);
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
                    }
                }

                await Task.Delay(10);
            }
        }
    }
}
