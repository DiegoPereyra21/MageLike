using System.Collections;
using FishNet.Object;
using Game.Core.Abilities.Abilities;
using Game.Presentation.Combat;
using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Maneja las tres fases del parry (startup → active → recovery) en el servidor.
    /// Durante la fase activa detecta proyectiles frente al jugador (en la dirección de
    /// mirada), los despawnea y restaura maná. VFX y detección comparten el mismo punto.
    /// </summary>
    public class ParryHandler : NetworkBehaviour
    {
        [Header("Posición del parry (relativa al jugador)")]
        [SerializeField] private float _heightOffset = 1f;   // altura del efecto
        [SerializeField] private float _forwardOffset = 1.5f; // distancia al frente

        private Mana _mana;
        private bool _parrying;

        private void Awake()
        {
            _mana = GetComponent<Mana>();
        }

        [Server]
        public void StartParry(ParryAbilitySO data, int casterNetworkId, Vector3 aimDirection)
        {
            if (_parrying) return;
            StartCoroutine(ParryRoutine(data, casterNetworkId, aimDirection.normalized));
        }

        [Server]
        private IEnumerator ParryRoutine(ParryAbilitySO data, int casterNetworkId, Vector3 aimDir)
        {
            _parrying = true;

            yield return new WaitForSeconds(data.StartupDuration);

            ShowParryVFXObserversRpc(ParryPhase.Active, aimDir, data.ParryRadius);
            float elapsed = 0f;
            bool succeeded = false;

            while (elapsed < data.ActiveDuration)
            {
                if (TryDetectProjectile(data.ParryRadius, aimDir))
                {
                    _mana?.Restore(data.ManaRestore);
                    ShowParryVFXObserversRpc(ParryPhase.Success, aimDir, data.ParryRadius);
                    succeeded = true;
                    break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(data.RecoveryDuration);
            _parrying = false;
        }

        /// <summary>Punto central del parry: al frente del jugador siguiendo la mirada.</summary>
        private Vector3 GetParryPoint(Vector3 aimDir)
            => transform.position + Vector3.up * _heightOffset + aimDir * _forwardOffset;

        [Server]
        private bool TryDetectProjectile(float radius, Vector3 aimDir)
        {
            Vector3 origin = GetParryPoint(aimDir);
            Collider[] hits = Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                if (!hit.TryGetComponent(out Projectile projectile)) continue;

                NetworkObject nob = projectile.GetComponent<NetworkObject>();
                if (nob == null) continue;

                Vector3 toProjectile = (hit.transform.position - origin).normalized;
                if (Vector3.Dot(aimDir, toProjectile) < 0f) continue; // solo hemisferio frontal

                projectile.Despawn();
                return true;
            }
            return false;
        }

        [ObserversRpc]
        private void ShowParryVFXObserversRpc(ParryPhase phase, Vector3 aimDir, float radius)
        {
            Vector3 vfxPos = GetParryPoint(aimDir.normalized);
            switch (phase)
            {
                case ParryPhase.Active:
                    VFXManager.PlayParryActive(vfxPos, radius);
                    break;
                case ParryPhase.Success:
                    VFXManager.PlayParrySuccess(vfxPos, radius);
                    break;
            }
        }

        private enum ParryPhase { Active, Success }
    }
}