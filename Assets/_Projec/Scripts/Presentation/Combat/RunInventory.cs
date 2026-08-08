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
        [SerializeField] private GameObject _worldItemPrefab; // prefab con WorldItem + NetworkObject

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

            while (_backpack.Count < cap) _backpack.Add(ItemStack.Empty);

            // Si la capacidad bajó, rescatar los items de los slots que desaparecen.
            while (_backpack.Count > cap)
            {
                int last = _backpack.Count - 1;
                ItemStack orphan = _backpack[last];
                _backpack.RemoveAt(last);

                if (orphan.IsEmpty) continue;

                // Intentar mover al primer slot libre que queda dentro de la nueva capacidad.
                bool rescued = false;
                for (int i = 0; i < _backpack.Count; i++)
                {
                    if (!_backpack[i].IsEmpty) continue;
                    _backpack[i] = orphan;
                    rescued = true;
                    break;
                }

                if (!rescued)
                    SpawnWorldItem(orphan);
            }
        }

        [Server]
        private void SpawnWorldItem(ItemStack stack)
        {
            SpawnWorldItemPublic(stack, transform.position);
        }



/// <summary>Server-only. Dropea un item del inventario al mundo.</summary>
        [Server]
        public bool TryDropToWorld(int zone, int index, Vector3 nearPosition)
        {
            ItemStack stack = GetSlot(zone, index);
            if (stack.IsEmpty) return false;

            SetSlot(zone, index, ItemStack.Empty);

            // Si era la mochila equipada, RebuildBackpackCapacity rescata/dropea el contenido.
            if (zone == 0 && index == (int)EquipmentSlot.Backpack)
                RebuildBackpackCapacity();

            SpawnWorldItemPublic(stack, nearPosition);
            return true;
        }


        /// <summary>Server-only. Equipa una mochila que viene del mundo (sin origen en la mochila interna).</summary>
        [Server]
        public void EquipBackpackFromWorld(ItemStack stack)
        {
            int slotIndex = (int)EquipmentSlot.Backpack;
            _equipment[slotIndex] = new ItemStack(stack.ItemId, 1, stack.Durability);
            RebuildBackpackCapacity();
        }

        /// <summary>Server-only. Punto de entrada público para spawnear un WorldItem cerca de una posición.</summary>
        [Server]
        public void SpawnWorldItemPublic(ItemStack stack, Vector3 nearPosition)
        {
            if (_worldItemPrefab == null) return;

            // Spawnear a la altura del jugador con offset aleatorio — el Rigidbody hace caer.
            Vector3 pos = nearPosition + Vector3.up * 0.5f;
            pos += new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));

            GameObject obj = Instantiate(_worldItemPrefab, pos, Random.rotation);
            if (obj.TryGetComponent(out WorldItem worldItem))
            {
                InstanceFinder.ServerManager.Spawn(obj);
                worldItem.ServerSetItem(stack);
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
                return false;

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


        /// <summary>Server-only. Mueve un item entre dos slots cualesquiera del inventario propio.
        /// Soporta reorden de mochila, equipar a slot exacto, desequipar, y merge de apilables.</summary>
        [Server]
        public bool TryMoveSlot(int fromZone, int fromIndex, int toZone, int toIndex)
        {
            ItemStack from = GetSlot(fromZone, fromIndex);
            if (from.IsEmpty) return false;

            // Validar destino de equipo: el item debe corresponder al slot exacto.
            if (toZone == 0) // Equipment
            {
                if (_database.GetById(from.ItemId) is not EquipmentItemSO equip) return false;
                if ((int)equip.Slot != toIndex) return false;

                // Regla de mochila: no se puede equipar una mochila si ya hay una puesta
                // y el origen no es ese mismo slot de equipo.
                if (equip.Slot == EquipmentSlot.Backpack && !_equipment[toIndex].IsEmpty
                    && !(fromZone == 0 && fromIndex == toIndex)) return false;
            }

            ItemStack to = GetSlot(toZone, toIndex);

            // Merge: mismo item apilable, destino no vacío.
            if (!to.IsEmpty && to.ItemId == from.ItemId)
            {
                ItemSO def = _database.GetById(from.ItemId);
                if (def != null && def.IsStackable)
                {
                    int space = def.MaxStack - to.Quantity;
                    if (space <= 0) return false;
                    int move = Mathf.Min(space, from.Quantity);
                    SetSlot(toZone, toIndex, new ItemStack(to.ItemId, to.Quantity + move, to.Durability));
                    int remaining = from.Quantity - move;
                    SetSlot(fromZone, fromIndex, remaining > 0
                        ? new ItemStack(from.ItemId, remaining, from.Durability)
                        : ItemStack.Empty);

                    if (toZone == 0 || fromZone == 0) RebuildBackpackCapacity();
                    return true;
                }
            }

            // Swap normal.
            SetSlot(toZone, toIndex, from);
            SetSlot(fromZone, fromIndex, to.IsEmpty ? ItemStack.Empty : to);

            if (toZone == 0 || fromZone == 0) RebuildBackpackCapacity();
            return true;
        }


        /// <summary>Server-only. Mueve entre el inventario propio y un LootContainer externo.</summary>
        [Server]
        public bool TryMoveWithContainer(int fromZone, int fromIndex, int toZone, int toIndex, LootContainer container)
        {
            // fromZone/toZone: 0=Equipment, 1=Backpack, 2=Container
            bool fromContainer = fromZone == 2;
            bool toContainer   = toZone   == 2;

            if (fromContainer && toContainer) return false; // container→container no aplica
            if (!fromContainer && !toContainer) return false; // ambos internos: usar TryMoveSlot

            if (fromContainer)
            {
                // Container → inventario propio (slot exacto con swap/merge)
                if (fromIndex < 0 || fromIndex >= container.Contents.Count) return false;
                ItemStack dragged = container.Contents[fromIndex];
                if (dragged.IsEmpty) return false;

                ItemStack existing = GetSlot(toZone, toIndex);

                // Validar slot de equipo
                if (toZone == 0)
                {
                    if (_database.GetById(dragged.ItemId) is not EquipmentItemSO equip) return false;
                    if ((int)equip.Slot != toIndex) return false;
                }

                // Merge
                if (!existing.IsEmpty && existing.ItemId == dragged.ItemId)
                {
                    ItemSO def = _database.GetById(dragged.ItemId);
                    if (def != null && def.IsStackable)
                    {
                        int space = def.MaxStack - existing.Quantity;
                        if (space <= 0) return false;
                        int move = Mathf.Min(space, dragged.Quantity);
                        SetSlot(toZone, toIndex, new ItemStack(existing.ItemId, existing.Quantity + move, existing.Durability));
                        int remaining = dragged.Quantity - move;
                        // Actualizar o quitar del contenedor
                        container.ServerUpdateAt(fromIndex, remaining > 0
                            ? new ItemStack(dragged.ItemId, remaining, dragged.Durability)
                            : ItemStack.Empty);
                        return true;
                    }
                }

                // Swap: sacar del contenedor, poner en slot, depositar lo que había
                container.ServerUpdateAt(fromIndex, ItemStack.Empty); // quita del contenedor
                SetSlot(toZone, toIndex, dragged);
                if (!existing.IsEmpty)
                    container.ServerDeposit(existing); // lo que había en el slot vuelve al contenedor

                if (toZone == 0) RebuildBackpackCapacity();
                return true;
            }
            else
            {
                // Inventario propio → contenedor (append, sin slot fijo)
                ItemStack dragged = GetSlot(fromZone, fromIndex);
                if (dragged.IsEmpty) return false;

                SetSlot(fromZone, fromIndex, ItemStack.Empty);
                container.ServerDeposit(dragged);

                if (fromZone == 0) RebuildBackpackCapacity();
                return true;
            }
        }



        private ItemStack GetSlot(int zone, int index)
        {
            return zone switch
            {
                0 => (index >= 0 && index < _equipment.Count) ? _equipment[index] : ItemStack.Empty,
                1 => (index >= 0 && index < _backpack.Count)  ? _backpack[index]  : ItemStack.Empty,
                _ => ItemStack.Empty
            };
        }

        private void SetSlot(int zone, int index, ItemStack stack)
        {
            switch (zone)
            {
                case 0: if (index >= 0 && index < _equipment.Count) _equipment[index] = stack; break;
                case 1: if (index >= 0 && index < _backpack.Count)  _backpack[index]  = stack; break;
            }
        }


        // ---------- IRunInventory ----------

        [Server]
        public void CommitToStash()
        {
            // Al extraer, el inventario se persiste como inventario propio (volvés al menú con todo intacto).
            var snapshot = TakeSnapshot();
            Game.Presentation.Run.PlayerLoadoutService.Save(snapshot);
        }

        [Server]
        public void DropAll()
        {
            var loot = new List<ItemStack>();
            foreach (var s in _backpack) if (!s.IsEmpty) loot.Add(s);
            foreach (var s in _equipment) if (!s.IsEmpty) loot.Add(s);

            if (loot.Count > 0 && _lootContainerPrefab != null)
            {
                Vector3 pos = transform.position;
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, 5f))
                    pos = groundHit.point + Vector3.up * 0.1f;

                GameObject obj = Instantiate(_lootContainerPrefab, pos, Quaternion.identity);

                if (obj.TryGetComponent(out LootContainer container))
                {
                    InstanceFinder.ServerManager.Spawn(obj);
                    container.ServerFill(loot);
                }
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