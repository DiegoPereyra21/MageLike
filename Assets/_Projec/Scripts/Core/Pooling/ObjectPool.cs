using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Pooling
{
    /// <summary>
    /// Pool genérico para objetos LOCALES (VFX de impacto, sonidos, etc.) que no requieren
    /// sincronización de red. Para prefabs con NetworkObject (proyectiles reales), usar el
    /// pooling nativo de Fish-Net (NetworkObject "Enable Pooling" + ServerManager.Spawn),
    /// que ya maneja el ciclo de vida en red — NO usar este pool para eso.
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _inactive = new Stack<T>();

        public ObjectPool(T prefab, int prewarmCount = 0, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;

            for (int i = 0; i < prewarmCount; i++)
            {
                T instance = CreateInstance();
                Release(instance);
            }
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T instance = _inactive.Count > 0 ? _inactive.Pop() : CreateInstance();
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            return instance;
        }

        public void Release(T instance)
        {
            instance.gameObject.SetActive(false);
            _inactive.Push(instance);
        }

        private T CreateInstance()
        {
            T instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}
