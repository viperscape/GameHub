using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GameNetwork
{
    class LocalPlayer : NetPlayer
    {

        Unreliable unreliable;
        Stopwatch stopwatch;

        public Dictionary<ushort, IPEndPoint> players { get; private set; }

        public LocalPlayer ()
        {
            remote = new TcpClient();
            stopwatch = new Stopwatch();
            players = new Dictionary<ushort, IPEndPoint>();
        }

        public async Task StartClient(string host, int port)
        {
            stopwatch.Start();
            IPHostEntry host_info = Dns.GetHostEntry(host);
            IPAddress ip;

            if (host_info.AddressList[0].AddressFamily != AddressFamily.InterNetworkV6)
                ip = host_info.AddressList[0];
            else
                ip = host_info.AddressList[1];

            Console.WriteLine("Client socket destination {0}", ip);
            await remote.ConnectAsync(ip, port);
            unreliable = new Unreliable();
            IPEndPoint ep = new IPEndPoint(ip, port);
            players.Add(0, ep);
            //_ = unreliable.Read(AppendDatagrams);
            _ = ReliableRead();
        }

        public void BeginUnreliable()
        {
            _ = unreliable.Read(AppendDatagrams);
        }

        void AppendDatagrams(IPEndPoint remote, Datagram datagram)
        {
            datagrams.Enqueue(datagram);
        }

        public async Task Write(byte[] data, bool reliable = true, ushort target_id = 0)
        {
            Datagram datagram = new Datagram(id, (uint)stopwatch.ElapsedMilliseconds, data);
            if (reliable)
                await WriteDatagram(datagram);
            else
            {
                IPEndPoint ep;
                if (players.TryGetValue(target_id, out ep))
                    await WriteDatagram(datagram, unreliable, ep);
            }
        }

        public void AddPeer (ushort id, string host, int port)
        {
            IPAddress ip = IPAddress.Parse(host);
            players.Add(id, new IPEndPoint(ip, port));
        }
    }
}
