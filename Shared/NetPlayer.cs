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
        public uint ping { get; private set; } = 0;
        public int pingLoss = 0;
        public int packetLoss { get; private set; } = 0;
        public uint lastSeen { get; private set; } = 0;
        public bool shouldDrop = false;

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
            _ = StartPing();
        }

        public async Task StartPing()
        {
            long rangeTime = stopwatch.ElapsedMilliseconds;
            while (true)
            {
                await Task.Delay(250);

                if (stopwatch.ElapsedMilliseconds - rangeTime > 5000)
                {
                    packetLoss = pingLoss / 5; // average over 5 seconds
                    rangeTime = stopwatch.ElapsedMilliseconds;
                    pingLoss = 0;
                    
                    //Console.WriteLine("packet loss per second: {0}% {1}", ((float)packetLoss/4) * 100, endpoint.ToString());

                    if (stopwatch.ElapsedMilliseconds - lastSeen > 5000)
                    {
                        shouldDrop = true;
                        break;
                    }
                }

                pingLoss++;
                await Write(new Message(0), id, false);
            }
        }

        public void ResetStats ()
        {
            ping = 0;
            pingLoss = 0;
            packetLoss = 0;
            //stopwatch.Restart();
            lastSeen = (uint) stopwatch.ElapsedMilliseconds;
            //packetCount = 0;
            shouldDrop = false;
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

            if (BitConverter.ToUInt16(datagram.data, 0) == 0)  // reserved packet? ignore, this is for lower level stuff only
            {
                if (datagram.playerId == id) // our pingback?
                {
                    pingLoss--;

                    if (lastSeen > datagram.timestamp)
                        ping = datagram.timestamp - (uint)stopwatch.ElapsedMilliseconds;
                }
                else
                    await unreliable.Write(datagram, endpoint); // return packet as is


                lastSeen = (uint)stopwatch.ElapsedMilliseconds;

                return;
            }


            if (datagrams.Count < MAX_DATAGRAMS) // buffer only a few seconds of packets worth
            {
                bool isUnique = true;
                foreach (var d in datagrams)
                {
                    if (d.packetId == datagram.packetId)
                    {
                        isUnique = false;
                        break;
                    }
                }

                if (isUnique)
                    datagrams.Add(datagram);
            }
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
