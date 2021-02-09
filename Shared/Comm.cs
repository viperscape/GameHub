using System;
using System.Collections.Generic;
using System.Text;

namespace NetGame
{
    /// <summary>
    /// Communication and Commands protocol
    /// </summary>
    class Comm
    {
        public enum Kind : ushort
        {
            Empty,
            JoinGameArea,
            GameAreasList,
            RequestGameAreas,
            BrokerNewMember,
            Text,
            Transform,
            Position3,
            Forward3,
            Rotate3,
            LevelStatus,
            Died,
            Join,
            Quit,
            Drop,
            Ping,
            Pong,
            PingReport,
            Pickup
        }

        public Kind kind { get; private set; }
        public Message message { get; private set; }

        public byte[] Serialize()
        {
            return message.GetRaw();
        }

        public Comm() // empty Comm
        {

        }

        public Comm(Kind kind) // build without message data
        {
            message = new Message((ushort)kind);
        }

        public Comm(Kind kind, ushort id) // one off message about player
        {
            message = new Message((ushort) kind);
            message.AddInt(id);
        }

        public Comm(byte[] data) // recreate from raw data (deserialize)
        {
            message = new Message(data);
            kind = (Kind)message.kind;
        }

        public Comm(Message message_) // recreate from a message
        {
            message = message_;
            kind = (Kind)message.kind;
        }

        public Comm(string msg)
        {
            kind = Kind.Text;
            message = new Message((ushort)kind);
            message.AddString(msg);
        }

        public Comm(Kind kind, float[] f)
        {
            message = new Message((ushort)kind);
            AddTupleFloat(f);
        }

        public float[] GetTupleFloat()
        {
            float[] f = new float[3];
            f[0] = message.GetFloat();
            f[1] = message.GetFloat();
            f[2] = message.GetFloat();

            return f;
        }

        public void AddTupleFloat(float[] f)
        {
            message.AddFloat(f[0]);
            message.AddFloat(f[1]);
            message.AddFloat(f[2]);
        }

        public Comm(Kind kind, string msg)
        {
            message = new Message((ushort)kind);
            message.AddString(msg);
        }

        public Comm(Kind kind, string name, float[] f)
        {
            Comm comm = new Comm();
            Message message = new Message((ushort)kind);
            message.AddString(name);
            AddTupleFloat(f);
        }

        public Comm(float[] position, float[] forward)
        {
            message = new Message((ushort)Kind.Transform);
            AddTupleFloat(position);
            AddTupleFloat(forward);
        }
    }
}
