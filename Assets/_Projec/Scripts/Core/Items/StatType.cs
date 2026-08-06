namespace Game.Core.Items
{
    /// <summary>Stats del jugador que el equipamiento puede modificar.</summary>
    public enum StatType
    {
        ManaRegen,
        JumpForce,
        MoveSpeed,
        Protection,        // porcentaje de reducción de daño (con cap aplicado en PlayerStats)
        DamageMultiplier,  // afecta habilidades (catalizador)
        CastSpeedMultiplier
    }

    /// <summary>Cómo se aplica el valor del modificador.</summary>
    public enum ModifierOperation
    {
        Additive,       // suma plana (ej. +2 regen de maná)
        Multiplicative  // factor (ej. ×1.2 daño)
    }
}