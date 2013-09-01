using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wolfje.Plugins.SEconomy.Packets {
    /// <summary>
    /// The NPC strike packet 0x1C
    /// </summary>
    public struct DamageNPC {
        public short NPCID;
        public short Damage;
        public float Knockback;
        public byte Direction;
        public bool CrititcalHit;
    }
}
