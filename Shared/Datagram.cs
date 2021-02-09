using System;

namespace NetGame
{
    public class Datagram
    {
        public ushort playerId { get; set; }
        public uint timestamp { get; set; }
        public byte[] data { get; set; }

        public byte[] Pack()
        {
            byte[] id = BitConverter.GetBytes(playerId);
            byte[] time = BitConverter.GetBytes(timestamp);
            
            byte[] payload = new byte[2 + 4 + data.Length];
            Buffer.BlockCopy(id, 0, payload, 0, id.Length);
            Buffer.BlockCopy(time, 0, payload, 2, time.Length);
            Buffer.BlockCopy(data, 0, payload, 6, data.Length);

            return payload;
        }

        // build new datagram to send
        public Datagram (ushort id, uint time, byte[] data_)
        {
            playerId = id;
            timestamp = time;
            data = data_;
        }

        // parse from raw data
        public Datagram(byte[] data_)
        {
            // NOTE may need to consider endianess
            playerId = BitConverter.ToUInt16(data_, 0);
            timestamp = BitConverter.ToUInt32(data_, 2);
            data = new byte[data_.Length - 6];
            Buffer.BlockCopy(data_, 6, data, 0, data_.Length - 6);
        }
    }
}
