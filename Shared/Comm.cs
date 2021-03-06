using System;
using System.Collections.Generic;
using System.Text;

namespace GameNetwork
{
    /// <summary>
    /// Communication and Commands protocol
    /// </summary>
    public class Comm
    {
        public const ushort
            Reserved = 0,
            JoinGameArea = 1,
            GameAreasList = 2,
            RequestGameAreas = 3,
            BrokerNewMember = 4,
            Text = 5,
            Transform = 6,
            Controller = 7,
            Forward3 = 8,
            Rotate3 = 9,
            LevelStatus = 10,
            Died = 11,
            Join = 12,
            Quit = 13,
            Drop = 14,
            Ping = 15,
            Pong = 16,
            PingReport = 17,
            Pickup = 18,
            RequestPlayerId = 19,
            AssignPlayerId = 20,
            PlayerUuid = 21,

            Shoot = 1000,
            DetachHook = 1001;
    }
}
