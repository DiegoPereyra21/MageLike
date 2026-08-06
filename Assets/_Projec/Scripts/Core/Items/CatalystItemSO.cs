using UnityEngine;

namespace Game.Core.Items
{
    /// <summary>
    /// Catalizador: el 'arma' del mago. Es equipamiento del slot Catalyst con un perfil
    /// (varita / bastón / libro) que modifica daño y velocidad de casteo de las habilidades.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Items/Catalyst", fileName = "Catalyst_")]
    public class CatalystItemSO : EquipmentItemSO
    {
        [Header("Catalizador")]
        [SerializeField] private CatalystType _catalystType;

        public CatalystType CatalystType => _catalystType;
    }
}