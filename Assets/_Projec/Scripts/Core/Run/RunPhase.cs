namespace Game.Core.Run
{
    /// <summary>
    /// Fase de la run. Preparado para agregar fases intermedias (ej. DangerPhase con el
    /// timer) sin romper lo existente.
    /// </summary>
    public enum RunPhase
    {
        InProgress,
        Ended
    }
}