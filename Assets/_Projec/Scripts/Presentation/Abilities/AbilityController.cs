using FishNet.Object;
using Game.Core.Abilities;
using Game.Presentation.Bootstrap;
using Game.Presentation.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using FishNet.Managing.Timing;
using Game.Presentation.Player;

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
        private PlayerMovementController _movement;
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

        /// <summary>Progreso de cooldown del slot (0 = listo, 1 = recién usado). Usa la predicción local del owner.</summary>
        public float GetCooldownProgress(int slot)
        {
            if (slot < 0 || slot >= _localCooldownEndTime.Length) return 0f;

            AbilitySO ability = _equippedAbilities[slot];
            if (ability == null || ability.Cooldown <= 0f) return 0f;

            float remaining = _localCooldownEndTime[slot] - Time.time;
            if (remaining <= 0f) return 0f;

            return Mathf.Clamp01(remaining / ability.Cooldown);
        }

        private void Awake()
        {
            _castActions = new InputAction[4];
            _mana = GetComponent<Mana>();
            _movement = GetComponent<PlayerMovementController>();
            _stats = GetComponent<Game.Presentation.Combat.PlayerStats>();

            _castActions[0] = new InputAction("CastSlot0", InputActionType.Button, "<Mouse>/leftButton");
            _castActions[1] = new InputAction("CastSlot1", InputActionType.Button, "<Keyboard>/leftShift");
            _castActions[2] = new InputAction("CastSlot2", InputActionType.Button, "<Mouse>/rightButton");
            _castActions[3] = new InputAction("CastSlot3", InputActionType.Button, "<Keyboard>/q");
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

            ResolveAim(out Vector3 aimDirection, out Vector3 aimPoint);

            // Muzzle VFX local en el báculo — solo si la habilidad tiene uno definido.
            // Feedback local instantáneo del tirador (owner): muzzle en el báculo + proyectil cosmético.
            if (base.IsOwner)
            {
                Vector3 muzzlePos = _spellOrigin != null ? _spellOrigin.position : _aimOrigin.position;

                if (ability.MuzzlePrefab != null)
                    VFXManager.PlayProjectileMuzzle(muzzlePos, Quaternion.LookRotation(aimDirection));

                if (ability.TryGetCosmeticProjectile(out GameObject cosmeticPrefab, out float cosmeticSpeed))
                {
                    Vector3 toAim = aimPoint - muzzlePos;
                    Vector3 dir = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : aimDirection;
                    CosmeticProjectileManager.Spawn(cosmeticPrefab, muzzlePos, dir, cosmeticSpeed, transform);
                }
                
                // Dash owner-predicted: el owner lo encola localmente ya, así lo predice al
                // instante (movimiento + audio + FOV). El server también lo aplica; reconcile alinea.
                if (ability.TryGetOwnerDash(aimDirection, out Vector3 dashDir, out float dashSpeed, out float dashDur))
                    _movement?.StartDash(dashDir, dashSpeed, dashDur);
            }

            // Tick de disparo del cliente para lag compensation (el server rebobina a este tick).
            PreciseTick fireTick = base.TimeManager.GetPreciseTick(TickType.Tick);
            CastServerRpc(slot, aimDirection, aimPoint, fireTick);
        }

        // Cliente. Calcula hacia dónde mira el jugador. La aimDirection (mirada con pitch) la usan
        // dash/parry; el aimPoint (objetivo del crosshair) lo usa el proyectil para converger.
        // El origen del disparo NO se calcula acá: el servidor lo pone desde su SpellOrigin autoritativo.
        private void ResolveAim(out Vector3 aimDirection, out Vector3 aimPoint)
        {
            Vector3 cameraOrigin = _aimOrigin.position;
            Vector3 cameraForward = _aimOrigin.forward;

            aimPoint = cameraOrigin + cameraForward * _maxAimDistance;
            if (Physics.Raycast(cameraOrigin, cameraForward, out RaycastHit hit, _maxAimDistance, _aimMask))
                aimPoint = hit.point;

            aimDirection = cameraForward;
        }

        [ServerRpc]
        private void CastServerRpc(int slot, Vector3 aimDirection, Vector3 aimPoint, PreciseTick fireTick)
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

            // Anti-cheat: el origen NO se confía al cliente. Se toma del SpellOrigin autoritativo.
            Vector3 head = _aimOrigin != null ? _aimOrigin.position : transform.position;
            Vector3 origin = _spellOrigin != null ? _spellOrigin.position : head;

            // Sanidad del aimPoint: acotar su distancia a la cabeza para descartar valores absurdos.
            Vector3 toAim = aimPoint - head;
            if (toAim.magnitude > _maxAimDistance)
                aimPoint = head + toAim.normalized * _maxAimDistance;

            var context = new AbilityCastContext(
                casterNetworkId: base.ObjectId,
                origin: origin,
                aimDirection: aimDirection.normalized,
                aimPoint: aimPoint,
                tick: fireTick.Tick,           // tick de disparo del cliente (para el catch-up/rewind)
                damageMultiplier: dmgMul
            );

            ability.Execute(_executor, in context);

            // Muzzle para los demás (el owner ya lo vio local). Desde el SpellOrigin autoritativo.
            if (ability.MuzzlePrefab != null)
                PlayMuzzleObserversRpc(origin, aimDirection);
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


        // Llamado por el proyectil networked (server) al impactar. El caster ve el impacto por su
        // cosmético local; a los demás se lo mandamos acá (ExcludeOwner).
        public void NotifyProjectileImpact(Vector3 point, Vector3 normal)
        {
            PlayImpactObserversRpc(point, normal);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void PlayImpactObserversRpc(Vector3 point, Vector3 normal)
        {
            VFXManager.PlayProjectileHit(point, Quaternion.LookRotation(normal));
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void PlayMuzzleObserversRpc(Vector3 point, Vector3 direction)
        {
            VFXManager.PlayProjectileMuzzle(point, Quaternion.LookRotation(direction));
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
