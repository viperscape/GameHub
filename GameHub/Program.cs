﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace NetGame
{
    class Program
    {
        static int port = 7070;
        static string host = "44.234.72.181";
        static Server server;

        static Dictionary<string, List<ushort>> gameAreas; // areas in the game with players present

        static async Task Main(string[] args)
        {
            gameAreas = new Dictionary<string, List<ushort>>();

            //server = new Server(port);
            //_ =  HandleClients();
            //await server.Listen(OnConnect,OnDisonnect);

           // _ = TestClient(5);
            await TestClient(15);
        }

        static async Task OnConnect(ushort id) // send all game areas listed
        {
            Comm comm = SerializeGameAreas();
            await server.WritePlayer(id, comm.Serialize());
        }

        static Comm SerializeGameAreas ()
        {
            Comm comm = new Comm(Comm.Kind.GameAreasList);
            comm.message.AddInt(gameAreas.Count);
            foreach (var area in gameAreas.Keys)
            {
                comm.message.AddString(area);
            }

            return comm;
        }

        static async Task OnDisonnect(ushort id)
        {
            Comm comm = new Comm(Comm.Kind.Quit, id);
            foreach (var player in server.players.Values)
            {
                await server.WritePlayer(player.id, comm.message.GetRaw());
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
                        Comm comm = new Comm(datagram.data); // unwrap into a easy to use message type

                        if (comm.kind == Comm.Kind.Text)
                        {
                            Console.WriteLine("server msg {0} {1}", datagram.playerId, comm.message.GetString());
                        }
                        else if (comm.kind == Comm.Kind.Ping)
                        {
                            Comm comm_ = new Comm(Comm.Kind.Pong);
                            comm_.message.AddInt(comm.message.GetInt());
                            await server.WritePlayer(player.id, comm_.Serialize());
                        }
                        else if (comm.kind == Comm.Kind.RequestGameAreas)
                        {
                            Comm comm_ = SerializeGameAreas();
                            await server.WritePlayer(player.id, comm_.Serialize());
                        }
                        else if (comm.kind == Comm.Kind.JoinGameArea)
                        {
                            string area = comm.message.GetString();
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
                                        Comm comm_ = new Comm(Comm.Kind.BrokerNewMember);
                                        if (p.udpEndpoint != null) // grab all upd connections and share with new player
                                        {
                                            comm_.message.AddUShort(p.id);
                                            comm_.message.AddString(p.udpEndpoint.Address.ToString());
                                            comm_.message.AddInt(p.udpEndpoint.Port);
                                            await server.WritePlayer(player.id, comm_.Serialize());
                                        } // we should dump players without established udp endpoints

                                        if (player.udpEndpoint != null) // share new player udp with all existing players
                                        {
                                            comm_ = new Comm(Comm.Kind.BrokerNewMember);
                                            comm_.message.AddUShort(player.id);
                                            comm_.message.AddString(player.udpEndpoint.Address.ToString());
                                            comm_.message.AddInt(player.udpEndpoint.Port);
                                            await server.WritePlayer(p.id, comm_.Serialize());
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

        static async Task KeepUDPAlive (LocalPlayer player)
        {
            Comm comm = new Comm(Comm.Kind.Empty);
            var data = comm.Serialize();
            foreach(var k in player.players.Keys)
                await player.Write(data, false, k);
        }

        static async Task TestClient(int delay = 0) // simulate client
        {
            await Task.Delay(delay);

            try
            {
                LocalPlayer player = new LocalPlayer();
                await player.StartClient(host, port);

                await Task.Delay(150);
                Comm comm = new Comm(Comm.Kind.JoinGameArea);
                comm.message.AddString("test");
                await player.Write(comm.Serialize());

                await KeepUDPAlive(player);
                player.BeginUnreliable();
                await Task.Delay(150);
                while (player.isConnected())
                {
                    await KeepUDPAlive(player);
                    List<Datagram> datagrams = player.GetDatagrams();
                    foreach (var datagram in datagrams)
                    {
                        comm = new Comm(datagram.data);
                        if (comm.kind == Comm.Kind.Text)
                            Console.WriteLine("MSG {0} {1}", datagram.playerId, comm.message.GetString());
                        else if(comm.kind == Comm.Kind.GameAreasList)
                        {
                            int count = comm.message.GetInt();
                            for (; count > 0; count--)
                            {
                                string area = comm.message.GetString();
                                Console.WriteLine("area {0}", area);
                            }
                        }
                        else if (comm.kind == Comm.Kind.BrokerNewMember)
                        {
                            ushort id = comm.message.GetUShort();
                            string address = comm.message.GetString();
                            int port = comm.message.GetInt();
                            Console.WriteLine("udp endpoint {0} {1} {2}", id, address, port);
                            player.AddPeer(id, address, port);
                        }
                        else if (comm.kind == Comm.Kind.Empty)
                        {
                            Console.WriteLine("Empty {0} {1}", player.id, datagram.playerId);
                        }
                        else if (comm.kind == Comm.Kind.Quit)
                        {
                            ushort id = comm.message.GetUShort();
                            player.players.Remove(id);
                        }
                        else
                        {
                            Console.WriteLine("other datagram type {0}", datagram.playerId);
                        }
                    }

                    
                    await Task.Delay(50); // wait for 20 pps roughly
                }

                player.Shutdown();
            }
            catch (Exception e)
            {
                Console.WriteLine("ERR {0}", e.ToString());
            }
        }
    }
}
