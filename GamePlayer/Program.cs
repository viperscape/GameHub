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
                Message msg = new Message(Comm.RequestId);
                await client.WriteServer(msg);
                client.BeginUnreliable();

                while (true)
                {
                    foreach (NetPlayer np in client.players.Values)
                    {
                        Message msg_ = new Message(Comm.Empty);
                        await np.Write(msg_);
                    } 

                    msg = new Message(Comm.RequestId);
                    await client.WriteServer(msg);

                    foreach (var player in client.players.Values)
                    {
                        List<Datagram> datagrams = player.GetDatagrams();
                        foreach (var datagram in datagrams)
                        {
                            msg = new Message(datagram.data);
                            if (msg.kind == Comm.Empty) continue;
                            Console.WriteLine(msg.kind);
                            if (msg.kind == Comm.Text)
                                Console.WriteLine("MSG {0} {1}", datagram.playerId, msg.GetString());
                            else if (msg.kind == Comm.RequestId)
                            {
                                player.id = msg.GetUShort();
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
                            else if (msg.kind == Comm.BrokerNewMember)
                            {
                                ushort id = msg.GetUShort();
                                string address = msg.GetString();
                                int port = msg.GetInt();
                                Console.WriteLine("udp endpoint {0} {1} {2}", id, address, port);
                                client.AddPeer(id, address, port);
                            }
                            else if (msg.kind == Comm.Empty)
                            {
                                Console.WriteLine("Empty {0} {1}", player.id, datagram.playerId);
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
