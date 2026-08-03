using FishNet.Object;
using Game.Core.Abilities;
using Game.Presentation.Bootstrap;
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
        [SerializeField] private LayerMask _aimMask;
        [SerializeField] private float _maxAimDistance = 50f;

        private readonly float[] _localCooldownEndTime = new float[4];
        private AbilityExecutor _executor;
        private InputAction[] _castActions;

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
            if (Time.time < _localCooldownEndTime[slot]) return; // feedback local, no autoritativo

            _localCooldownEndTime[slot] = Time.time + ability.Cooldown;

            ResolveAim(out Vector3 origin, out Vector3 direction, out Vector3 aimPoint);
            CastServerRpc(slot, origin, direction, aimPoint);
        }

        private void ResolveAim(out Vector3 origin, out Vector3 direction, out Vector3 aimPoint)
        {
            origin = _aimOrigin.position;
            direction = _aimOrigin.forward;
            aimPoint = origin + direction * _maxAimDistance;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, _maxAimDistance, _aimMask))
                aimPoint = hit.point;
        }

        [ServerRpc]
        private void CastServerRpc(int slot, Vector3 origin, Vector3 direction, Vector3 aimPoint)
        {
            if (slot < 0 || slot >= _equippedAbilities.Length) return;
            AbilitySO ability = _equippedAbilities[slot];
            if (ability == null) return;

            // TODO: validar cooldown real server-side (tabla de cooldowns por NetworkObject)
            // y costo de recurso antes de ejecutar. Se omite en este prototipo del vertical slice.

            var context = new AbilityCastContext(
                casterNetworkId: base.ObjectId,
                origin: origin,
                aimDirection: direction.normalized,
                aimPoint: aimPoint,
                tick: base.TimeManager.LocalTick
            );

            ability.Execute(_executor, in context);
        }
    }
}
