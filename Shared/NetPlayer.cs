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
        ConcurrentBag<Datagram> datagrams = new ConcurrentBag<Datagram>();
        ConcurrentDictionary<uint, Datagram> ackGrams = new ConcurrentDictionary<uint, Datagram>();
        ConcurrentDictionary<uint, Datagram> reliableQueue = new ConcurrentDictionary<uint, Datagram>(); // track based on packet id

        public ushort id; // session id
        public string uuid; // long term unique player id
        public IPEndPoint endpoint;
        public Unreliable unreliable;

        uint packetCount = 0; // outbound packet count, used for tracking

        public Stopwatch stopwatch { get; private set; }

        int MAX_DATAGRAMS = 100; // max datagrams per queue
        int RELIABLE_TTL = 800; // max time in ms a reliable should wait before starting over as a new datagram

        public NetPlayer(ushort id_, IPEndPoint endpoint_, Unreliable unreliable_)
        {
            id = id_;
            endpoint = endpoint_;
            unreliable = unreliable_;
            stopwatch = new Stopwatch();
            stopwatch.Start();
        }

        public async Task Enqueue(Datagram datagram)
        {
            // we should check the ttl on old archived messages
            foreach (var d in ackGrams.Values)
            {
                if (stopwatch.ElapsedMilliseconds - d.timestamp > RELIABLE_TTL)
                {
                    ackGrams.TryRemove(d.packetId, out _);
                }
            }

            if (datagram.ack == Ack.isAck)
            {
                reliableQueue.TryRemove(datagram.packetId, out _);
                return;
            }
            else if (datagram.ack == Ack.needAck)
            {
                Datagram datagram_ = new Datagram(datagram.Pack()); // recreate as a copy
                datagram_.ack = Ack.isAck;
                //datagram_.playerId = id; // recreate as from us
                byte[] data = new byte[1]; // wipe payload
                datagram_.data = data;
                await unreliable.Write(datagram_, endpoint);

                if (!ackGrams.TryAdd(datagram.packetId, datagram_)) // make note that we saw this already
                {
                    // we already processed this in a previous frame
                    return;
                }
            }

           /* foreach (var d in datagrams) // no dupe datagrams
            {
              //  if (d.packetId == datagram.packetId)
                    //return;
            }*/
            

            if (BitConverter.ToUInt16(datagram.data, 0) == 0) return; // reserved packet? ignore, this is for lower level stuff only

            if (datagrams.Count < MAX_DATAGRAMS) // buffer only a few seconds of packets worth
                datagrams.Add(datagram);
        }

        public List<Datagram> GetDatagrams()
        {
            List<Datagram> list = new List<Datagram>();

            Datagram datagram;
            while (datagrams.TryTake(out datagram))
            {
                list.Add(datagram);
            }

            list.Sort((x, y) => x.timestamp.CompareTo(y.timestamp)); // reorder based on timestamp
            
            return list;
        }

        public async Task Write(Message message, ushort fromId = 0, bool isReliable = true)
        {
            Datagram datagram = new Datagram(fromId, packetCount, (uint)stopwatch.ElapsedMilliseconds, message.GetRaw());
            if (isReliable)
            {
                datagram.ack = Ack.needAck;
                reliableQueue.TryAdd(datagram.packetId, datagram);
            }

            await unreliable.Write(datagram, endpoint);
            packetCount++;
        }

        public async Task SendReliables()
        {
            foreach (var datagram in reliableQueue.Values)
            {
                if (stopwatch.ElapsedMilliseconds - datagram.timestamp > RELIABLE_TTL)
                {
                    datagram.timestamp = (uint) stopwatch.ElapsedMilliseconds;
                }

                await unreliable.Write(datagram, endpoint);
            }
        }
    }
}
