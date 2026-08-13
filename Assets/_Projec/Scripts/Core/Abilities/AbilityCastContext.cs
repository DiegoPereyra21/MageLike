using UnityEngine;

namespace Game.Core.Abilities
{
    public readonly struct AbilityCastContext
    {
        public readonly int CasterNetworkId;
        public readonly Vector3 Origin;
        public readonly Vector3 AimDirection;
        public readonly Vector3 AimPoint;
        public readonly uint Tick;
        public readonly float DamageMultiplier;
        public readonly int Slot; // slot casteado; sirve para resolver el AudioClip localmente en cada cliente

        public AbilityCastContext(int casterNetworkId, Vector3 origin, Vector3 aimDirection, Vector3 aimPoint, uint tick, float damageMultiplier, int slot)
        {
            CasterNetworkId = casterNetworkId;
            Origin = origin;
            AimDirection = aimDirection;
            AimPoint = aimPoint;
            Tick = tick;
            DamageMultiplier = damageMultiplier;
            Slot = slot;
        }
    }
}