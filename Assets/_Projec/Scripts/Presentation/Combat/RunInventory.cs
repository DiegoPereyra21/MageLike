using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game.Core.Items;
using Game.Core.Run;
using UnityEngine;
using FishNet;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Inventario de run server-authoritative. Mochila (slots) y equipamiento (por slot),
    /// ambos sincronizados por SyncList. Implementa IRunInventory (extracción salva, muerte pierde).
    /// </summary>
    public class RunInventory : NetworkBehaviour, IRunInventory
    {
        [SerializeField] private ItemDatabase _database;
        [SerializeField] private int _fallbackBackpackSlots = 8; // capacidad si no hay mochila equipada
        [SerializeField] private GameObject _lootContainerPrefab; // asignar en el Inspector
        [SerializeField] private Game.Core.Items.StartingKitSO _startingKit;

        // Mochila: lista de stacks. Vacíos representan slots libres.
        private readonly SyncList<ItemStack> _backpack = new SyncList<ItemStack>();

        // Equipamiento por slot. Indexado por (int)EquipmentSlot. Vacío = nada equipado.
        private readonly SyncList<ItemStack> _equipment = new SyncList<ItemStack>();

        public IReadOnlyList<ItemStack> Backpack => _backpack;
        public IReadOnlyList<ItemStack> Equipment => _equipment;

        public event System.Action OnInventoryChanged;

        public override void OnStartServer()
        {
            // Inicializar los slots de equipamiento (uno por cada EquipmentSlot, vacío).
            int slotCount = System.Enum.GetValues(typeof(EquipmentSlot)).Length;
            for (int i = 0; i < slotCount; i++)
                _equipment.Add(ItemStack.Empty);

            RebuildBackpackCapacity();

            // Cargar el inventario propio persistente (o el kit inicial la primera vez).
            Game.Presentation.Run.PlayerLoadoutService.EnsureInitialized(_startingKit);
            ApplySnapshot(Game.Presentation.Run.PlayerLoadoutService.Current);
        }

        public override void OnStartClient()
        {
            _backpack.OnChange += (op, index, oldItem, newItem, asServer) => OnInventoryChanged?.Invoke();
            _equipment.OnChange += (op, index, oldItem, newItem, asServer) => OnInventoryChanged?.Invoke();
        }

        // ---------- Capacidad de mochila ----------

        private int CurrentBackpackCapacity()
        {
            ItemStack bp = _equipment[(int)EquipmentSlot.Backpack];
            if (!bp.IsEmpty && _database.GetById(bp.ItemId) is EquipmentItemSO e && e.Slot == EquipmentSlot.Backpack)
                return Mathf.Max(e.BackpackSlots, 0);
            return _fallbackBackpackSlots;
        }

        private void RebuildBackpackCapacity()
        {
            int cap = CurrentBackpackCapacity();
            // Ajusta la cantidad de slots de la mochila a la capacidad actual.
            while (_backpack.Count < cap) _backpack.Add(ItemStack.Empty);
            while (_backpack.Count > cap && _backpack.Count > 0)
            {
                // Nota: en esta capa asumimos que no se reduce capacidad con items dentro
                // (regla de mochila: hay que sacar la actual primero). Simplificado.
                _backpack.RemoveAt(_backpack.Count - 1);
            }
        }

        // ---------- Agregar item (con stacking) ----------

        /// <summary>Server-only. Intenta agregar cantidad de un item. Devuelve lo que NO entró.</summary>
        [Server]
        public int TryAddItem(string itemId, int quantity)
        {
            ItemSO def = _database.GetById(itemId);
            if (def == null || quantity <= 0) return quantity;

            int remaining = quantity;

            // 1. Si es apilable, rellenar pilas existentes del mismo item.
            if (def.IsStackable)
            {
                for (int i = 0; i < _backpack.Count && remaining > 0; i++)
                {
                    ItemStack s = _backpack[i];
                    if (s.IsEmpty || s.ItemId != itemId) continue;

                    int space = def.MaxStack - s.Quantity;
                    if (space <= 0) continue;

                    int add = Mathf.Min(space, remaining);
                    s.Quantity += add;
                    _backpack[i] = s;
                    remaining -= add;
                }
            }

            // 2. Ocupar slots vacíos con nuevas pilas.
            for (int i = 0; i < _backpack.Count && remaining > 0; i++)
            {
                if (!_backpack[i].IsEmpty) continue;

                int add = def.IsStackable ? Mathf.Min(def.MaxStack, remaining) : 1;
                _backpack[i] = new ItemStack(itemId, add, 1f);
                remaining -= add;
            }

            return remaining; // lo que no entró (mochila llena)
        }


        /// <summary>Server-only. Crea un snapshot del inventario actual (para persistir al extraer).</summary>
        [Server]
        public Game.Core.Items.InventorySnapshot TakeSnapshot()
        {
            var snap = new Game.Core.Items.InventorySnapshot();
            foreach (var s in _equipment) snap.Equipment.Add(s);   // incluye vacíos: preserva los slots
            foreach (var s in _backpack) if (!s.IsEmpty) snap.Backpack.Add(s);
            return snap;
        }


        /// <summary>Server-only. Restaura el inventario desde un snapshot (inventario propio persistente).</summary>
        [Server]
        public void ApplySnapshot(Game.Core.Items.InventorySnapshot snap)
        {
            // Limpiar primero.
            ClearAll();

            if (snap == null) return;

            // Restaurar equipamiento por slot (el snapshot guarda un stack por cada slot, en orden).
            for (int i = 0; i < snap.Equipment.Count && i < _equipment.Count; i++)
                _equipment[i] = snap.Equipment[i];

            // Recalcular capacidad de mochila según la mochila equipada del snapshot.
            RebuildBackpackCapacity();

            // Restaurar items de mochila.
            foreach (var stack in snap.Backpack)
            {
                if (stack.IsEmpty) continue;
                // Colocar en el primer slot libre.
                for (int i = 0; i < _backpack.Count; i++)
                {
                    if (_backpack[i].IsEmpty)
                    {
                        _backpack[i] = stack;
                        break;
                    }
                }
            }
        }



        // ---------- Equipar / desequipar ----------

        /// <summary>Server-only. Equipa un item desde un slot de mochila.</summary>
        [Server]
        public bool TryEquipFromBackpack(int backpackIndex)
        {
            if (backpackIndex < 0 || backpackIndex >= _backpack.Count) return false;
            ItemStack stack = _backpack[backpackIndex];
            if (stack.IsEmpty) return false;

            if (_database.GetById(stack.ItemId) is not EquipmentItemSO equip) return false;

            int slotIndex = (int)equip.Slot;

            // Regla de mochila: no se puede equipar una mochila si ya hay una puesta.
            if (equip.Slot == EquipmentSlot.Backpack && !_equipment[slotIndex].IsEmpty)
            {
                Debug.Log("[RunInventory] Ya tenés una mochila equipada. Sacala primero.");
                return false;
            }

            // Si el slot está ocupado (no-mochila), mover lo actual a la mochila.
            if (!_equipment[slotIndex].IsEmpty)
            {
                ItemStack current = _equipment[slotIndex];
                _backpack[backpackIndex] = current; // swap
            }
            else
            {
                _backpack[backpackIndex] = ItemStack.Empty;
            }

            _equipment[slotIndex] = new ItemStack(stack.ItemId, 1, stack.Durability);

            if (equip.Slot == EquipmentSlot.Backpack)
                RebuildBackpackCapacity();

            return true;
        }
        /// <summary>Server-only. Desequipa el slot de equipo dado, mandando el item a la mochila.</summary>
        [Server]
        public bool TryUnequip(int equipmentSlotIndex)
        {
            if (equipmentSlotIndex < 0 || equipmentSlotIndex >= _equipment.Count) return false;
            ItemStack equipped = _equipment[equipmentSlotIndex];
            if (equipped.IsEmpty) return false;

            // Buscar un slot libre en la mochila.
            int freeSlot = -1;
            for (int i = 0; i < _backpack.Count; i++)
                if (_backpack[i].IsEmpty) { freeSlot = i; break; }

            if (freeSlot < 0) return false; // mochila llena, no se puede desequipar

            _backpack[freeSlot] = equipped;
            _equipment[equipmentSlotIndex] = ItemStack.Empty;

            if ((EquipmentSlot)equipmentSlotIndex == EquipmentSlot.Backpack)
                RebuildBackpackCapacity();

            return true;
        }

        /// <summary>Server-only. Agrega un stack ya formado a la mochila (para saqueo de contenedores).</summary>
        [Server]
        public int TryAddStack(ItemStack stack)
        {
            return TryAddItem(stack.ItemId, stack.Quantity);
        }
        // ---------- IRunInventory ----------

        [Server]
        public void CommitToStash()
        {
            // Modelo actual: al extraer, el inventario NO va al stash automáticamente.
            // Se persiste como inventario propio (volvés al menú con todo intacto).
            var snapshot = TakeSnapshot();
            Game.Presentation.Run.PlayerLoadoutService.Save(snapshot);
            Debug.Log("[RunInventory] Extracción: inventario propio guardado (persistente).");
            // No se limpia el inventario de la run acá: el jugador ya está extraído.
        }

        [Server]
        public void DropAll()
        {
            var loot = new List<ItemStack>();
            foreach (var s in _backpack) if (!s.IsEmpty) loot.Add(s);
            foreach (var s in _equipment) if (!s.IsEmpty) loot.Add(s);

            if (loot.Count > 0 && _lootContainerPrefab != null)
            {
                // Spawnear el contenedor en la posición del jugador, un poco arriba del piso.
                Vector3 pos = transform.position;
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, 5f))
                    pos = groundHit.point + Vector3.up * 0.1f; // justo sobre el suelo

                GameObject obj = Instantiate(_lootContainerPrefab, pos, Quaternion.identity);

                if (obj.TryGetComponent(out LootContainer container))
                {
                    InstanceFinder.ServerManager.Spawn(obj);
                    container.ServerFill(loot);
                }
                Debug.Log($"[RunInventory] Muerte: {loot.Count} items soltados en un contenedor.");
            }
            else
            {
                Debug.Log("[RunInventory] Muerte: sin loot que soltar.");
            }
            // Al morir, el inventario propio persistente se vacía: volvés "desnudo" al menú.
            Game.Presentation.Run.PlayerLoadoutService.Clear();
            ClearAll();
        }

        [Server]
        private void ClearAll()
        {
            for (int i = 0; i < _backpack.Count; i++) _backpack[i] = ItemStack.Empty;
            for (int i = 0; i < _equipment.Count; i++) _equipment[i] = ItemStack.Empty;
            RebuildBackpackCapacity();
        }

        // ---------- Debug helper (para InventoryDebugger) ----------

        public void DebugLogContents()
        {
            Debug.Log("--- Inventario ---");
            for (int i = 0; i < _equipment.Count; i++)
                if (!_equipment[i].IsEmpty)
                    Debug.Log($"  [Equip {(EquipmentSlot)i}] {_equipment[i].ItemId} (dur {_equipment[i].Durability:0.00})");
            for (int i = 0; i < _backpack.Count; i++)
                if (!_backpack[i].IsEmpty)
                    Debug.Log($"  [Slot {i}] {_backpack[i].ItemId} x{_backpack[i].Quantity}");
        }
    }
}