using UnityEngine;

namespace Game.Core.Abilities
{
    /// <summary>
    /// Datos puros necesarios para ejecutar una habilidad. Sin dependencias de Unity Networking
    /// ni de MonoBehaviour concretos: solo lo que la lógica de la habilidad necesita para actuar.
    /// </summary>
    public readonly struct AbilityCastContext
    {
        public readonly int CasterNetworkId;
        public readonly Vector3 Origin;
        public readonly Vector3 AimDirection;
        public readonly Vector3 AimPoint;
        public readonly uint Tick;

        public AbilityCastContext(int casterNetworkId, Vector3 origin, Vector3 aimDirection, Vector3 aimPoint, uint tick)
        {
            CasterNetworkId = casterNetworkId;
            Origin = origin;
            AimDirection = aimDirection;
            AimPoint = aimPoint;
            Tick = tick;
        }
    }
}
