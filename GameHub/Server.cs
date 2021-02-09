using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetGame
{
    class Server
    {
        private int port;

        public Dictionary<ushort, RemotePlayer> players;

        public Stopwatch stopwatch { get; private set; }

        Unreliable unreliable;

        public Server(int port_ = 7070)
        {
            port = port_;
            players = new Dictionary<ushort, RemotePlayer>();
            stopwatch = new Stopwatch();
            stopwatch.Start();

            unreliable = new Unreliable(port);
        }

        public async Task Listen(Func<ushort, Task> onconn, Func<ushort, Task> ondisc)
        {
            Console.WriteLine("Listening on {0}", port);

            _ = unreliable.Read(LinkPlayer);
            await TcpListen(onconn, ondisc);
        }

        void LinkPlayer(IPEndPoint remote, Datagram datagram)
        {
            RemotePlayer player;
            if (players.TryGetValue(datagram.playerId, out player))
            {
                player.datagrams.Enqueue(datagram);

                player.udpEndpoint = remote; // update udp endpoint
                //Console.WriteLine("UDP Linked {0}", datagram.playerId);
            }

        }

        async Task TcpListen(Func<ushort, Task> onconn, Func<ushort, Task> ondisc)
        {
            var rand = new Random();

            IPAddress ip = IPAddress.Parse("0.0.0.0");
            TcpListener listen = new TcpListener(ip, port);
            Console.WriteLine("Server socket source {0}", ip);
            //listen.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            listen.Start();

            while (true)
            {
                TcpClient socket = await listen.AcceptTcpClientAsync();
                ushort id = (ushort)rand.Next(65000);
                _ = HandlePlayer(socket, id, onconn, ondisc);
            }
        }


        async Task HandlePlayer(TcpClient socket, ushort id, Func<ushort, Task> onconn, Func<ushort, Task> ondisc)
        {
            socket.ReceiveBufferSize = 1024;
            RemotePlayer player = new RemotePlayer(socket, id);
            
            Console.WriteLine("Client Connected {0}", id);
            players.Add(id, player);

            _ = onconn(id);

            try
            {
                await player.ReliableRead();
            }
            finally
            {
                players.Remove(player.id);
                player.Shutdown();
                Console.WriteLine("Client Disconnected {0}", id);
                await ondisc(player.id);
            }
        }

        public async Task WritePlayer(ushort id, byte[] data, bool reliable = true)
        {
            RemotePlayer player;
            if (players.TryGetValue(id, out player))
            {

                if (reliable)
                    await player.Write(data, (uint) stopwatch.ElapsedMilliseconds);
                else
                    await player.Write(data, (uint) stopwatch.ElapsedMilliseconds, unreliable);
            }
        }
    }
}
