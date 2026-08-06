using System;
using UnityEngine;

namespace Game.Core.Items
{
    /// <summary>Un modificador de stat data-driven. Los items llevan una lista de estos.</summary>
    [Serializable]
    public struct StatModifier
    {
        public StatType Stat;
        public ModifierOperation Operation;
        public float Value;
    }
}