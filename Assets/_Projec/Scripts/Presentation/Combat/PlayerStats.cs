using System.Collections.Generic;
using FishNet.Object;
using Game.Core.Items;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Agrega los modificadores del equipamiento actual y expone los stats finales.
    /// Los sistemas del jugador (Mana, Movement, Health, habilidades) LEEN de acá; nadie
    /// les escribe directo desde el inventario. Recalcula cuando cambia el equipamiento.
    /// Corre en servidor y cliente (ambos tienen el equipo sincronizado vía SyncList).
    /// </summary>
    [RequireComponent(typeof(RunInventory))]
    public class PlayerStats : NetworkBehaviour
    {
        [SerializeField] private ItemDatabase _database;

        [Header("Valores base (sin equipamiento)")]
        [SerializeField] private float _baseManaRegen = 8f;
        [SerializeField] private float _baseJumpForce = 6f;
        [SerializeField] private float _baseMoveSpeed = 6f;
        [SerializeField] private float _baseDamageMultiplier = 1f;
        [SerializeField] private float _baseCastSpeedMultiplier = 1f;

        [Header("Protección")]
        [SerializeField, Range(0f, 0.9f)] private float _protectionCap = 0.6f; // techo de reducción

        private RunInventory _inventory;

        // Stats finales calculados.
        public float ManaRegen { get; private set; }
        public float JumpForce { get; private set; }
        public float MoveSpeed { get; private set; }
        public float DamageMultiplier { get; private set; }
        public float CastSpeedMultiplier { get; private set; }
        public float ProtectionPercent { get; private set; } // 0..cap

        public event System.Action OnStatsChanged;

        private void Awake()
        {
            _inventory = GetComponent<RunInventory>();
            RecalculateToBase();
        }

        public override void OnStartNetwork()
        {
            _inventory.OnInventoryChanged += Recalculate;
            Recalculate();
        }

        public override void OnStopNetwork()
        {
            if (_inventory != null) _inventory.OnInventoryChanged -= Recalculate;
        }

        private void RecalculateToBase()
        {
            ManaRegen = _baseManaRegen;
            JumpForce = _baseJumpForce;
            MoveSpeed = _baseMoveSpeed;
            DamageMultiplier = _baseDamageMultiplier;
            CastSpeedMultiplier = _baseCastSpeedMultiplier;
            ProtectionPercent = 0f;
        }

        private void Recalculate()
        {
            // Acumuladores: aditivos suman, multiplicativos multiplican sobre la base.
            float manaRegen = _baseManaRegen;
            float jump = _baseJumpForce;
            float move = _baseMoveSpeed;
            float dmgMul = _baseDamageMultiplier;
            float castMul = _baseCastSpeedMultiplier;
            float protectionSum = 0f;

            foreach (ItemStack eq in _inventory.Equipment)
            {
                if (eq.IsEmpty) continue;
                if (_database.GetById(eq.ItemId) is not EquipmentItemSO def) continue;

                foreach (StatModifier mod in def.Modifiers)
                {
                    switch (mod.Stat)
                    {
                        case StatType.ManaRegen:
                            manaRegen += Apply(mod); break;
                        case StatType.JumpForce:
                            jump += Apply(mod); break;
                        case StatType.MoveSpeed:
                            move += Apply(mod); break;
                        case StatType.DamageMultiplier:
                            dmgMul += Apply(mod); break;
                        case StatType.CastSpeedMultiplier:
                            castMul += Apply(mod); break;
                        case StatType.Protection:
                            protectionSum += mod.Value; break; // protección: suma de %
                    }
                }
            }

            ManaRegen = manaRegen;
            JumpForce = jump;
            MoveSpeed = move;
            DamageMultiplier = dmgMul;
            CastSpeedMultiplier = castMul;
            ProtectionPercent = Mathf.Clamp(protectionSum, 0f, _protectionCap);

            OnStatsChanged?.Invoke();
        }

        // Para aditivo devuelve el valor; para multiplicativo, lo convierte a delta sobre la base.
        // Simplificación: tratamos ambos como contribución sumable al acumulador.
        private float Apply(StatModifier mod)
        {
            return mod.Value;
        }
    }
}