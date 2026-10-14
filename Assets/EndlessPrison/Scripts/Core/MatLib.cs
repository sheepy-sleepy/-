using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    /// <summary>
    /// 用程式產生材質。自動判斷專案用的是 URP、HDRP 還是內建管線，避免物件變成粉紅色。
    /// </summary>
    public static class MatLib
    {
        static Shader litShader;
        static Shader unlitShader;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Shader Lit
        {
            get
            {
                if (litShader == null)
                {
                    litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (litShader == null) litShader = Shader.Find("HDRP/Lit");
                    if (litShader == null) litShader = Shader.Find("Standard");
                    if (litShader == null) litShader = Shader.Find("Diffuse");
                }
                return litShader;
            }
        }

        /// <summary>不受光、可半透明，用來畫視錐、路徑、範圍圈。</summary>
        static Shader Unlit
        {
            get
            {
                if (unlitShader == null)
                {
                    unlitShader = Shader.Find("Sprites/Default");
                    if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                return unlitShader;
            }
        }

        /// <summary>霧面材質。smoothness 低一點比較像油畫顏料，不會有塑膠反光。</summary>
        public static Material Solid(Color color, float smoothness = 0.08f)
        {
            string key = "S" + ColorUtility.ToHtmlStringRGBA(color) + smoothness.ToString("F2");
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;

            m = new Material(Lit);
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            cache[key] = m;
            return m;
        }

        /// <summary>會自己發光的材質（火盆餘燼、燭火）。</summary>
        public static Material Glow(Color color, float intensity = 2f)
        {
            string key = "G" + ColorUtility.ToHtmlStringRGBA(color) + intensity.ToString("F1");
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;

            m = new Material(Lit);
            SetColor(m, color);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            cache[key] = m;
            return m;
        }

        /// <summary>不受光的半透明材質，支援頂點顏色。</summary>
        public static Material Overlay(Color color)
        {
            var m = new Material(Unlit);
            SetColor(m, color);
            m.renderQueue = 3100;
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }

        /// <summary>建立一個方塊／球體等基本形狀，套上材質。removeCollider 預設會拿掉碰撞體。</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color,
            bool removeCollider = true, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            if (removeCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Solid(color);
            return go;
        }
    }
}
