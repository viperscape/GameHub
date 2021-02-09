using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace GameNetwork
{
    class NetPlayer
    {
        public ConcurrentQueue<Datagram> datagrams = new ConcurrentQueue<Datagram>();

        Dictionary<uint, Datagram> reliableQueue = new Dictionary<uint, Datagram>();

        public ushort id;
        public IPEndPoint endpoint;
        public Unreliable unreliable;

        Dictionary<ushort, uint> timestamps = new Dictionary<ushort, uint>();
        public Stopwatch stopwatch { get; private set; }

        public NetPlayer (ushort id_, IPEndPoint endpoint_, Unreliable unreliable_)
        {
            id = id_;
            endpoint = endpoint_;
            unreliable = unreliable_;
            stopwatch = new Stopwatch();
            stopwatch.Start();
        }

        public List<Datagram> GetDatagrams()
        {
            List<Datagram> list = new List<Datagram>();
            Dictionary<ushort, uint> timestamps_ = new Dictionary<ushort, uint>(); // collect timestamps per player over this interval

            Datagram datagram;
            while (datagrams.TryDequeue(out datagram))
            {
                if (datagram.ack == Ack.isAck) 
                {
                    reliableQueue.Remove(datagram.timestamp);
                    continue;
                }
                else if (datagram.ack == Ack.needAck)
                {
                    datagram.ack = Ack.isAck;
                    _ = unreliable.Write(datagram, endpoint);
                }

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
                timestamps.TryAdd(kv.Key, kv.Value);
            }

            return list;
        }

        public async Task Write(Message message, bool isReliable = true)
        {
            Datagram datagram = new Datagram(id, (uint)stopwatch.ElapsedMilliseconds, message.GetRaw());
            if (isReliable)
            {
                datagram.ack = Ack.needAck;
                reliableQueue.TryAdd(datagram.timestamp, datagram);

                foreach(var d in reliableQueue.Values)
                {
                    await unreliable.Write(d, endpoint);
                }
            }

            await unreliable.Write(datagram, endpoint);
        }


    }
}
