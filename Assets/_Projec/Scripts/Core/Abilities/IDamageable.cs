namespace Game.Core.Abilities
{
    /// <summary>
    /// Contrato mínimo para recibir daño/curación (valor negativo = cura).
    /// Placeholder hasta definir el sistema de salud completo.
    /// </summary>
    public interface IDamageable
    {
        void ApplyDamage(float amount, int instigatorNetworkId);
    }
}
