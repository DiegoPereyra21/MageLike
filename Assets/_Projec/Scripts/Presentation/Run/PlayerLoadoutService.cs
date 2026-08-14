using Game.Core.Items;

namespace Game.Presentation.Run
{
    /// <summary>
    /// Persiste el inventario propio del jugador (snapshot) entre runs, en memoria por ahora
    /// (se pierde al cerrar; listo para persistencia real después). Provee el kit inicial la
    /// primera vez.
    /// </summary>
    public static class PlayerLoadoutService
    {
        private static InventorySnapshot _snapshot;
        private static bool _initialized;

        /// <summary>El inventario propio persistente actual. Null si nunca se inicializó.</summary>
        public static InventorySnapshot Current => _snapshot;

        public static bool HasSnapshot => _initialized && _snapshot != null;

        /// <summary>Guarda una foto nueva (al extraer / al gestionar en el menú).</summary>
        public static void Save(InventorySnapshot snapshot)
        {
            _snapshot = snapshot;
            _initialized = true;
        }

        /// <summary>Vacía el inventario propio (al morir: volvés desnudo, pero con los slots de equipo visibles).</summary>
        public static void Clear()
        {
            _snapshot = BuildEmptySnapshot();
            _initialized = true;
        }

        /// <summary>Inicializa con el kit de principiante si nunca hubo inventario.</summary>
        public static void EnsureInitialized(StartingKitSO kit)
        {
            if (_initialized) return;

            var snap = BuildEmptySnapshot();

            if (kit != null)
            {
                // Colocar cada pieza del kit en su slot correcto (según su EquipmentSlot).
                foreach (var e in kit.Equipment)
                {
                    if (e.Item == null) continue;
                    int slotIndex = (int)e.Slot;
                    if (slotIndex >= 0 && slotIndex < snap.Equipment.Count)
                        snap.Equipment[slotIndex] = new ItemStack(e.Item.ItemId, 1, 1f);
                }

                // Los items sueltos del kit arrancan en Pocket L. Si el kit trae más items de
                // los que la capacidad real termine permitiendo, RunInventory los rescata/dropea
                // igual que hoy hace con la mochila (misma lógica de RebuildBackpackCapacity).
                foreach (var b in kit.Backpack)
                    if (b.Item != null)
                        snap.PocketL.Add(new ItemStack(b.Item.ItemId, b.Quantity, 1f));
            }

            _snapshot = snap;
            _initialized = true;
        }

        /// <summary>Snapshot con un slot vacío por cada EquipmentSlot (sin items). Base común de Clear/EnsureInitialized.</summary>
        private static InventorySnapshot BuildEmptySnapshot()
        {
            var snap = new InventorySnapshot();
            int slotCount = System.Enum.GetValues(typeof(EquipmentSlot)).Length;
            for (int i = 0; i < slotCount; i++)
                snap.Equipment.Add(ItemStack.Empty);
            return snap;
        }
    }
}