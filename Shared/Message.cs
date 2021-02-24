using System;
using System.Collections.Generic;
using System.Text;

namespace GameNetwork
{
    /// <summary>
    ///  packable message for serializing to bytes
    ///  TODO consider builder pattern
    /// </summary>
    public class Message
    {
        public ushort kind { get; }
        List<byte> payload;
        byte[] raw;
        int idx = 0; // for parsing the data, keeps track of current read state's index


        public Message (ushort kind_)
        {
            kind = kind_;
            payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(kind));
        }

        // build for sending as raw
        public Message (ushort kind_, byte[] data)
        {
            kind = kind_;

            payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(kind_));
            payload.AddRange(data);
        }

        // build from recv raw data
        public Message (byte[] payload_)
        {
            payload = new List<byte>(payload_);
            kind = BitConverter.ToUInt16(payload_, 0);
            idx += 2;
        }

        public byte[] GetRaw()
        {
            if (raw == null) raw = payload.ToArray();
            return raw;
        }

        public void AddInt(int i)
        {
            payload.AddRange(BitConverter.GetBytes(i));
        }

        public int GetInt()
        {
            int i = BitConverter.ToInt32(GetRaw(), idx);
            idx += 4;
            return i;
        }

        public void AddUShort(ushort i)
        {
            payload.AddRange(BitConverter.GetBytes(i));
        }

        public ushort GetUShort()
        {
            ushort i = BitConverter.ToUInt16(GetRaw(), idx);
            idx += 2;
            return i;
        }

        public void AddLong(long i)
        {
            payload.AddRange(BitConverter.GetBytes(i));
        }

        public long GetLong()
        {
            long i = BitConverter.ToInt64(GetRaw(), idx);
            idx += 8;
            return i;
        }

        public void AddFloat(float f)
        {
            payload.AddRange(BitConverter.GetBytes(f));
        }

        public float GetFloat()
        {
            float f = BitConverter.ToSingle(GetRaw(), idx);
            idx += 4;
            return f;
        }

        public void AddString(string s)
        {
            payload.AddRange(BitConverter.GetBytes((ushort) s.Length));
            payload.AddRange(Encoding.UTF8.GetBytes(s));
        }

        public string GetString()
        {
            ushort len = BitConverter.ToUInt16(GetRaw(), idx);
            string msg = Encoding.UTF8.GetString(GetRaw(), idx + 2, len);
            idx += 2 + len;
            return msg;
        }

        public void AddGeneric(byte[] b)
        {
            payload.AddRange(BitConverter.GetBytes((ushort)b.Length));
            payload.AddRange(b);
        }

        public byte[] GetGeneric() // NOTE this is untested and broken
        {
            ushort len = BitConverter.ToUInt16(GetRaw(), idx);
            byte[] b = new byte[len];
            Buffer.BlockCopy(GetRaw(), idx + 2, b, 0, len);//payload.GetRange(idx + 2, len).ToArray();
            idx += 2 + len;
            return b;
        }

        public void AddBool(bool b)
        {
            payload.Add(Convert.ToByte(b));
        }

        public bool GetBool()
        {
            bool b = BitConverter.ToBoolean(GetRaw(), idx);
            idx += 1;
            return b;
        }
    }
}
