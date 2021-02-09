using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace GameNetwork
{
    class NetPlayer
    {
        public ConcurrentQueue<Datagram> datagrams = new ConcurrentQueue<Datagram>();

        protected TcpClient remote;
        public ushort id { get; set; }
        Dictionary<ushort, uint> timestamps = new Dictionary<ushort, uint>();

        public bool isConnected()
        {
            return remote.Client.Connected;
        }


        public List<Datagram> GetDatagrams()
        {
            List<Datagram> list = new List<Datagram>();
            Dictionary<ushort, uint> timestamps_ = new Dictionary<ushort, uint>(); // collect timestamps per player over this interval

            Datagram datagram;
            while (datagrams.TryDequeue(out datagram))
            {
                uint t;
                if (timestamps.TryGetValue(datagram.playerId, out t))
                {
                    if (datagram.timestamp < t) continue; // ignore really old datagrams
                }

                // sort timestamps for this fetch interval
                if (timestamps_.TryGetValue(datagram.playerId, out t))
                {
                    if (datagram.timestamp > t)
                    {
                        timestamps_[datagram.playerId] = datagram.timestamp;
                    }
                }
                else
                {
                    timestamps_.Add(datagram.playerId, datagram.timestamp);
                }

                bool added = false;
                for (int i = 0; i < list.Count; i++) // let's reorder the list in case udp packets arrive out of order
                {
                    if (list[i].timestamp > datagram.timestamp)
                    {
                        list.Insert(i, datagram);
                        added = true;
                    }
                }

                if (!added)
                    list.Add(datagram);
            }

            foreach(var kv in timestamps_)
            {
                if (!timestamps.TryAdd(kv.Key, kv.Value))
                    timestamps[kv.Key] = kv.Value;
            }

            return list;
        }


        public async Task ReliableRead()
        {
            NetworkStream stream = remote.GetStream();
            stream.ReadTimeout = 1000;

            while (remote.Connected)
            {
                byte[] header = new byte[2];
                await stream.ReadAsync(header, 0, 2); // NOTE may need to consider endianess
                ushort len = 0;

                if (header.Length > 0)
                    len = BitConverter.ToUInt16(header, 0);

                if (len > 0)
                {
                    byte[] gzip = new byte[len];
                    await stream.ReadAsync(gzip, 0, len);
                    byte[] data = await Compression.Decompress(gzip);
                    Datagram datagram = new Datagram(data);
                    if (id == 0) id = datagram.playerId; // the first msg we recv should give us our unique player id
                    datagrams.Enqueue(datagram);
                }
            }
        }

        protected async Task WriteDatagram(Datagram datagram, Unreliable unreliable = null, IPEndPoint remote = null)
        {
            if (unreliable != null)
                await unreliable.Write(datagram, remote);
            else
                await ReliableWrite(datagram);
        }

        async Task ReliableWrite(Datagram datagram)
        {
            NetworkStream stream = remote.GetStream();

            byte[] data = datagram.Pack();
            data = await Compression.Compress(data);

            byte[] len = BitConverter.GetBytes((ushort)data.Length); // force as 2 byte ushort representation

            byte[] payload = new byte[2 + data.Length];
            Buffer.BlockCopy(len, 0, payload, 0, len.Length);
            Buffer.BlockCopy(data, 0, payload, 2, data.Length);
            await stream.WriteAsync(payload, 0, payload.Length);
        }

        public void Shutdown()
        {
            remote.Close();
        }
    }
}
