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
        static string host = "44.234.72.181";
        static async Task Main(string[] args)
        {
            await TestClient(15);
        }

        static async Task KeepUDPAlive(LocalPlayer player)
        {
            Comm comm = new Comm(Comm.Kind.Empty);
            var data = comm.Serialize();
            foreach (var k in player.players.Keys)
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
                        else if (comm.kind == Comm.Kind.GameAreasList)
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
