using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheDawn
{
    /// <summary>Grey-box helpers: cached materials and primitive builders.</summary>
    public static class Visuals
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static Shader litShader;

        public static Shader LitShader
        {
            get
            {
                if (litShader == null)
                {
                    if (GraphicsSettings.currentRenderPipeline != null)
                        litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (litShader == null) litShader = Shader.Find("Standard");
                    if (litShader == null) litShader = Shader.Find("Diffuse");
                }
                return litShader;
            }
        }

        public static Material Mat(Color c)
        {
            Material m;
            if (cache.TryGetValue(c, out m) && m != null) return m;
            m = new Material(LitShader);
            m.enableInstancing = true;
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            cache[c] = m;
            return m;
        }

        public static Material Emissive(Color c, float strength)
        {
            var m = new Material(LitShader);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * strength);
            return m;
        }

        /// <summary>Creates a primitive as a child, without a collider unless asked for.</summary>
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return go;
        }

        public static void SetColor(GameObject root, Color c)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = Mat(c);
        }
    }
}
