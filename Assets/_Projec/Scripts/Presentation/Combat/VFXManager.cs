using System.Collections;
using UnityEngine;

namespace Game.Presentation.Combat
{
    /// <summary>
    /// Singleton local (no networked) que corre corrutinas de VFX.
    /// Necesario porque los NetworkObjects se despawnean antes de que sus corrutinas terminen.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        private static VFXManager _instance;

        private void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static void PlayExplosion(Vector3 point, float radius, Material mat = null)
        {
            if (_instance == null)
            {
                var go = new GameObject("VFXManager");
                _instance = go.AddComponent<VFXManager>();
                DontDestroyOnLoad(go);
            }
            _instance.StartCoroutine(_instance.ExplosionVFX(point, radius, mat));
        }

        private IEnumerator ExplosionVFX(Vector3 point, float radius, Material mat)
        {
            GameObject vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(vfx.GetComponent<Collider>());
            vfx.transform.position = point;

            if (mat != null && vfx.TryGetComponent(out Renderer vfxRend))
                vfxRend.material = new Material(mat); // copia para poder modificar alpha

            float duration = 0.4f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                vfx.transform.localScale = Vector3.one * Mathf.Lerp(0f, radius * 2f, t);

                if (vfx.TryGetComponent(out Renderer r) && r.material.HasProperty("_BaseColor"))
                {
                    Color c = r.material.GetColor("_BaseColor");
                    c.a = Mathf.Lerp(0.8f, 0f, t);
                    r.material.SetColor("_BaseColor", c);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(vfx);
        }
    }
}