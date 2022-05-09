using System;
using System.Collections.Generic;
using System.Text;

namespace GameNetwork
{
    [System.Serializable]
    public enum Kinds : ushort
    {
        Player = 0,
        Ballistic = 1,
        HookPoint = 2
    }
}
