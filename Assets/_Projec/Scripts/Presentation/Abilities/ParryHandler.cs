using System.Collections;
using FishNet.Object;
using Game.Core.Abilities.Abilities;
using Game.Presentation.Combat;
using UnityEngine;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Maneja las tres fases del parry (startup → active → recovery) en el servidor.
    /// Durante la fase activa detecta proyectiles en un radio frente al jugador,
    /// los despawnea y restaura maná al caster.
    /// </summary>
    public class ParryHandler : NetworkBehaviour
    {
        private Mana _mana;
        [SerializeField] private Transform _aimOrigin; // asignar el mismo AimOrigin de la cámara
        private bool _parrying;

        private void Awake()
        {
            _mana = GetComponent<Mana>();
        }

        [Server]
        public void StartParry(ParryAbilitySO data, int casterNetworkId)
        {
            Debug.Log($"[ParryHandler] StartParry llamado. _parrying: {_parrying}");
            if (_parrying) return;
            StartCoroutine(ParryRoutine(data, casterNetworkId));
        }

        [Server]
        private IEnumerator ParryRoutine(ParryAbilitySO data, int casterNetworkId)
        {
            _parrying = true;

            // Fase 1 — Startup: vulnerable, VFX de preparación.
            ShowParryVFXObserversRpc(ParryPhase.Startup);
            yield return new WaitForSeconds(data.StartupDuration);

            // Fase 2 — Active: ventana de detección.
            ShowParryVFXObserversRpc(ParryPhase.Active);
            float elapsed = 0f;
            bool succeeded = false;

            while (elapsed < data.ActiveDuration)
            {
                if (TryDetectProjectile(data.ParryRadius, casterNetworkId))
                {
                    _mana?.Restore(data.ManaRestore);
                    ShowParryVFXObserversRpc(ParryPhase.Success);
                    succeeded = true;
                    break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Fase 3 — Recovery: vulnerable, sin acción.
            if (!succeeded)
                ShowParryVFXObserversRpc(ParryPhase.Recovery);

            yield return new WaitForSeconds(data.RecoveryDuration);
            _parrying = false;
        }

        [Server]
        private bool TryDetectProjectile(float radius, int casterNetworkId)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * (radius * 0.5f);

            Collider[] hits = Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Ignore);

            Debug.Log($"[Parry] OverlapSphere en {origin}, radio {radius}, hits: {hits.Length}");
            foreach (Collider hit in hits)
            {
                Debug.Log($"[Parry] Hit: {hit.name} - tiene Projectile: {hit.TryGetComponent(out Projectile _)}");
                if (!hit.TryGetComponent(out Projectile projectile)) continue;

                NetworkObject nob = projectile.GetComponent<NetworkObject>();
                if (nob == null) continue;

                Vector3 toProjectile = (hit.transform.position - origin).normalized;
                float dot = Vector3.Dot(transform.forward, toProjectile);
                Debug.Log($"[Parry] Dot: {dot}");
                if (dot < 0f) continue;

                projectile.Despawn();
                return true;
            }
            return false;
        }

        [ObserversRpc]
        private void ShowParryVFXObserversRpc(ParryPhase phase)
        {
            // Mismo punto que la detección: adelantado al frente del jugador.
            Vector3 vfxPos = transform.position + Vector3.up * 1.2f + transform.forward * 1.5f;
            switch (phase)
            {
                case ParryPhase.Active:
                    VFXManager.PlayParryActive(vfxPos);
                    break;
                case ParryPhase.Success:
                    VFXManager.PlayParrySuccess(vfxPos);
                    break;
            }
        }

        private enum ParryPhase { Startup, Active, Recovery, Success }
    }
}