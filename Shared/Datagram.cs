using System;

namespace GameNetwork
{
    public class Ack
    {
        public const byte
            noAck = 0,
            isAck = 1,
            needAck = 2;
    }

    public class Datagram
    {
        public ushort playerId { get; set; }
        public byte ack;
        public uint timestamp { get; set; }
        public byte[] data { get; set; }

        public byte[] Pack()
        {
            byte[] id = BitConverter.GetBytes(playerId);
            byte[] ack_ = { ack };
            byte[] time = BitConverter.GetBytes(timestamp);
            byte[] payload = new byte[2 + 1 + 4 + data.Length];

            Buffer.BlockCopy(id, 0, payload, 0, id.Length);
            Buffer.BlockCopy(ack_, 0, payload, 2, ack_.Length);
            Buffer.BlockCopy(time, 0, payload, 3, time.Length);
            Buffer.BlockCopy(data, 0, payload, 7, data.Length);

            return payload;
        }

        // build new datagram to send
        public Datagram (ushort id, uint time, byte[] data_, byte ack_ = Ack.noAck)
        {
            playerId = id;
            ack = ack_;
            timestamp = time;
            data = data_;
        }

        // parse from raw data
        public Datagram(byte[] data_)
        {
            // NOTE may need to consider endianess
            playerId = BitConverter.ToUInt16(data_, 0);
            ack = data_[2];
            timestamp = BitConverter.ToUInt32(data_, 3);
            data = new byte[data_.Length - 7];
            Buffer.BlockCopy(data_, 7, data, 0, data_.Length - 7);
        }
    }
}
