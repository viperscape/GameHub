using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace GameNetwork
{
    class Unreliable
    {
        UdpClient udp;
        bool isClient;

        public Unreliable(int port = 7070)
        {
            isClient = false;
            udp = new UdpClient(port);
            Console.WriteLine("Listening on {0}", port);
            Setup();
        }

        public Unreliable()
        {
            isClient = true;
            udp = new UdpClient();
            Setup();
        }

        void Setup ()
        {
            if (isClient) udp.AllowNatTraversal(true);
            udp.DontFragment = true;
            udp.Client.ReceiveBufferSize = 1024;

            if (isClient)
                udp.Client.IOControl(
                    (IOControlCode) (-1744830452),
                    new byte[] { 0, 0, 0, 0 },
                    null
            );
        }

        public async Task Read(Action<IPEndPoint, Datagram> cb = null)
        {
            while (true)
            {
                if (udp.Client == null) break; // socket forcefully closed?
                if (!udp.Client.IsBound) continue; // not yet ready to read?

                try
                {
                    UdpReceiveResult res = await udp.ReceiveAsync();
                    IPEndPoint remote = (IPEndPoint)res.RemoteEndPoint;
                
                    byte[] data = await Compression.Decompress(res.Buffer);
                    Datagram datagram = new Datagram(data);

                    if (cb != null) cb(remote, datagram);
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERR {0}", e.ToString());
                }
            }

        }

        public async Task Write(Datagram datagram, IPEndPoint remote)
        {
            try
            {
                byte[] data = datagram.Pack();
                data = await Compression.Compress(data);

                if (data.Length > 508)
                {
                    Console.WriteLine("cannot compress udp to fit single frag");
                    return;
                }

                await udp.SendAsync(data, data.Length, remote);
            }
            catch (Exception e)
            {
                Console.WriteLine("ERR {0}", e.ToString());
            }
        }

        public void Shutdown()
        {
            udp.Close();
        }
    }
}
