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

        int MAX_DATAGRAMS = 100; // max datagrams per queue

        public NetPlayer(ushort id_, IPEndPoint endpoint_, Unreliable unreliable_)
        {
            id = id_;
            endpoint = endpoint_;
            unreliable = unreliable_;
            stopwatch = new Stopwatch();
            stopwatch.Start();
        }

        public void Enqueue(Datagram datagram)
        {
            if (datagram.ack == Ack.isAck)
            {
                reliableQueue.Remove(datagram.timestamp);
                return;
            }
            else if (datagram.ack == Ack.needAck)
            {
                datagram.ack = Ack.isAck;
                _ = unreliable.Write(datagram, endpoint);

                if (BitConverter.ToUInt16(datagram.data, 0) == 0) return; // reserved packet? ignore
            }

            foreach (var d in datagrams) // no dupe datagrams
            {
                if ((d.timestamp == datagram.timestamp) && (d.playerId == datagram.playerId))
                    return;
            }

            if (datagrams.Count < MAX_DATAGRAMS)
                datagrams.Enqueue(datagram);
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
                    if (datagram.ack == Ack.noAck && datagram.timestamp < t) continue; // ignore really old datagrams
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

            foreach (var kv in timestamps_)
            {
                timestamps.TryAdd(kv.Key, kv.Value);
            }

            return list;
        }

        public async Task Write(Message message, ushort fromId = 0, bool isReliable = true)
        {
            Datagram datagram = new Datagram(fromId, (uint)stopwatch.ElapsedMilliseconds, message.GetRaw());
            if (isReliable)
            {
                datagram.ack = Ack.needAck;
                reliableQueue.TryAdd(datagram.timestamp, datagram);
            }

            await unreliable.Write(datagram, endpoint);
        }

        public async Task SendReliables()
        {
            foreach (var datagram in reliableQueue.Values)
            {
                await unreliable.Write(datagram, endpoint);
            }
        }
    }
}
