using FishNet.Object;
using Game.Core.Abilities;
using Game.Presentation.Bootstrap;
using Game.Presentation.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Game.Presentation.Abilities
{
    /// <summary>
    /// Lee input de casteo, valida cooldown localmente (feedback inmediato de UI/anim) y
    /// pide ejecución real al servidor vía ServerRpc. El servidor es quien valida cooldown
    /// "de verdad" y ejecuta el efecto — el cliente nunca es fuente de verdad de daño/loot.
    /// </summary>
    public class AbilityController : NetworkBehaviour
    {
        [SerializeField] private AbilitySO[] _equippedAbilities = new AbilitySO[4];
        [SerializeField] private Transform _aimOrigin;
        [SerializeField] private Transform _spellOrigin; // punto de salida del hechizo (báculo, mano, etc.)
        [SerializeField] private LayerMask _aimMask;
        [SerializeField] private float _maxAimDistance = 50f;

        private readonly float[] _localCooldownEndTime = new float[4];
        // Cooldowns autoritativos del servidor (Time.time del servidor en que cada slot vuelve a estar listo).
        private readonly float[] _serverCooldownEndTime = new float[4];
        private Mana _mana;
        [SerializeField] private Game.Presentation.Combat.PlayerStats _stats;
        private AbilityExecutor _executor;
        private InputAction[] _castActions;

        private bool _inputBlocked;

        // VContainer no puede inyectar automáticamente en prefabs instanciados por el
        // NetworkManager de Fish-Net (spawn fuera del LifetimeScope normal). Por eso se
        // resuelve manualmente acá en vez de usar [Inject] en el spawn.
        [Inject]
        public void Construct(AbilityExecutor executor)
        {
            _executor = executor;
        }

        public override void OnStartNetwork()
        {
            // Este objeto fue instanciado por el NetworkManager de Fish-Net, por lo que
            // VContainer no lo inyectó automáticamente. Se resuelve manualmente acá.
            if (_executor == null)
            {
                var scope = FindFirstObjectByType<GameLifetimeScope>();
                scope?.InjectSpawnedObject(base.NetworkObject);
            }
        }

        private void Awake()
        {
            _castActions = new InputAction[4];
            _mana = GetComponent<Mana>();
            _stats = GetComponent<Game.Presentation.Combat.PlayerStats>();
            string[] keys = { "1", "2", "3", "4" };
            for (int i = 0; i < 4; i++)
                _castActions[i] = new InputAction($"CastSlot{i}", InputActionType.Button, $"<Keyboard>/{keys[i]}");
        }

        private void OnEnable()
        {
            foreach (var action in _castActions) action.Enable();
        }

        private void OnDisable()
        {
            foreach (var action in _castActions) action.Disable();
        }

        private void Update()
        {
            if (!base.IsOwner) return;
            if (_inputBlocked) return;   // ← nuevo: sin castear con el inventario abierto

            for (int i = 0; i < _castActions.Length; i++)
            {
                if (_castActions[i].WasPressedThisFrame())
                    TryCast(i);
            }
        }

        private void TryCast(int slot)
        {
            AbilitySO ability = _equippedAbilities[slot];
            if (ability == null) return;

            // Chequeos locales (feedback inmediato, no autoritativos).
            if (Time.time < _localCooldownEndTime[slot]) return;
            if (_mana != null && _mana.Current < ability.ResourceCost) return;

            // Predicción local de cooldown solamente. El maná lo descuenta y sincroniza el servidor.
            float castSpeed = _stats != null ? _stats.CastSpeedMultiplier : 1f;
            float effectiveCooldown = ability.Cooldown / Mathf.Max(0.1f, castSpeed);
            _localCooldownEndTime[slot] = Time.time + effectiveCooldown;

            ResolveAim(out Vector3 origin, out Vector3 direction, out Vector3 aimPoint);
            CastServerRpc(slot, origin, direction, aimPoint);
        }

        private void ResolveAim(out Vector3 origin, out Vector3 direction, out Vector3 aimPoint)
        {
            // El aimPoint se calcula desde la cámara (donde mira el jugador).
            Vector3 cameraOrigin = _aimOrigin != null ? _aimOrigin.position : transform.position;
            Vector3 cameraForward = _aimOrigin != null ? _aimOrigin.forward : transform.forward;

            aimPoint = cameraOrigin + cameraForward * _maxAimDistance;
            if (Physics.Raycast(cameraOrigin, cameraForward, out RaycastHit hit, _maxAimDistance, _aimMask))
                aimPoint = hit.point;

            // El proyectil sale del SpellOrigin si existe, sino de la cámara.
            origin = _spellOrigin != null ? _spellOrigin.position : cameraOrigin;

            // La dirección va desde el origen del hechizo hacia el aimPoint.
            direction = (aimPoint - origin).normalized;
        }

        [ServerRpc]
        private void CastServerRpc(int slot, Vector3 origin, Vector3 direction, Vector3 aimPoint)
        {
            if (slot < 0 || slot >= _equippedAbilities.Length) return;
            AbilitySO ability = _equippedAbilities[slot];
            if (ability == null) return;

            // 1. Validar cooldown autoritativo.
            if (Time.time < _serverCooldownEndTime[slot])
            {
                RejectCastTargetRpc(base.Owner, slot, _serverCooldownEndTime[slot]);
                return;
            }

            // 2. Validar y descontar maná autoritativo.
            if (_mana != null && !_mana.TrySpend(ability.ResourceCost))
            {
                RejectCastTargetRpc(base.Owner, slot, _serverCooldownEndTime[slot]);
                return;
            }

            // 3. Cast válido: registrar cooldown (reducido por velocidad de casteo) y ejecutar.
            float castSpeed = _stats != null ? _stats.CastSpeedMultiplier : 1f;
            float effectiveCooldown = ability.Cooldown / Mathf.Max(0.1f, castSpeed);
            _serverCooldownEndTime[slot] = Time.time + effectiveCooldown;

            float dmgMul = _stats != null ? _stats.DamageMultiplier : 1f;

            var context = new AbilityCastContext(
                casterNetworkId: base.ObjectId,
                origin: origin,
                aimDirection: direction.normalized,
                aimPoint: aimPoint,
                tick: base.TimeManager.LocalTick,
                damageMultiplier: dmgMul
            );

            ability.Execute(_executor, in context);
        }

        /// <summary>
        /// Solo al cliente dueño: el servidor rechazó el cast. Corrige la predicción local
        /// (restaura cooldown real y deja que el SyncVar de maná se reconcilie solo).
        /// </summary>
        [TargetRpc]
        private void RejectCastTargetRpc(FishNet.Connection.NetworkConnection conn, int slot, float serverCooldownEnd)
        {
            // El cast fue rechazado: revertir la predicción de cooldown local.
            // Restauramos al cooldown REAL del servidor (si el rechazo fue por maná,
            // serverCooldownEnd refleja el estado real, que puede ser "ya listo").
            float remaining = serverCooldownEnd - Time.time;
            _localCooldownEndTime[slot] = remaining > 0f ? serverCooldownEnd : 0f;

            // El maná se corrige solo vía SyncVar en el próximo sync.
        }


[Server]
        public void NotifyProjectileImpact()
        {
            NotifyImpactTargetRpc(base.Owner);
        }

        [TargetRpc]
        private void NotifyImpactTargetRpc(FishNet.Connection.NetworkConnection conn)
        {
            Game.Presentation.Combat.ScreenShake.Shake(0.8f);
        }




        public void SetInputBlocked(bool blocked) => _inputBlocked = blocked;

        public int AbilitySlotCount => _equippedAbilities.Length;

        public AbilitySO GetAbility(int slot)
        {
            if (slot < 0 || slot >= _equippedAbilities.Length) return null;
            return _equippedAbilities[slot];
        }

        /// <summary>
        /// Fracción de cooldown restante (0 = listo, 1 = recién casteado). Cálculo local
        /// para feedback de UI; el cooldown autoritativo se valida en servidor aparte.
        /// </summary>
        public float GetCooldownNormalized(int slot)
        {
            if (slot < 0 || slot >= _equippedAbilities.Length) return 0f;
            AbilitySO ability = _equippedAbilities[slot];
            if (ability == null || ability.Cooldown <= 0f) return 0f;

            float remaining = _localCooldownEndTime[slot] - Time.time;
            if (remaining <= 0f) return 0f;
            return Mathf.Clamp01(remaining / ability.Cooldown);
        }
    }
}
