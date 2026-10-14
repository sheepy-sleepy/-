using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    public enum Ability { None, STR, DEX, INT, WIS }

    /// <summary>調查物件時可以選的一個做法（例如「擠過去（敏捷 DC 10）」）。</summary>
    public class InteractOption
    {
        public string Label;
        public Ability Check = Ability.None;
        public int DC;
        public bool Advantage;
        public Func<bool> Available;      // null 代表永遠可選
        public Action<bool> OnResult;     // 參數是檢定成功與否（沒有檢定時永遠是 true）
        public string Hint;               // 按鈕旁的小字說明
        public ActionAnim Anim = ActionAnim.Interact;

        public static string AbilityName(Ability a)
        {
            switch (a)
            {
                case Ability.STR: return "力量";
                case Ability.DEX: return "敏捷";
                case Ability.INT: return "智力";
                case Ability.WIS: return "感知";
                default: return "";
            }
        }
    }

    /// <summary>場景裡可以點擊調查的東西。</summary>
    public class Interactable : MonoBehaviour
    {
        public string Title;
        public Vector2Int Cell;
        public bool Blocks = true;          // 擋不擋路
        public bool Done;                   // 已經沒東西可做了
        public Func<string> Describe;       // 調查時顯示的文字
        public Func<List<InteractOption>> Options;
        public Action OnFirstLook;

        bool looked;

        public void MarkLooked()
        {
            if (looked) return;
            looked = true;
            if (OnFirstLook != null) OnFirstLook();
        }

        public void Init(Vector2Int cell, GridMap map, bool blocksSight)
        {
            Cell = cell;
            transform.position = GridMap.ToWorld(cell);
            if (Blocks) map.SetBlocked(cell, true, blocksSight);

            // 點擊用的碰撞體，蓋住整格
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(0.95f, 1f, 0.95f);
        }
    }

    /// <summary>火光閃爍。</summary>
    public class Flicker : MonoBehaviour
    {
        public Light Target;
        public float BaseIntensity = 1.5f;
        public float Amount = 0.25f;
        float seed;

        void Start() { seed = UnityEngine.Random.value * 100f; }

        void Update()
        {
            if (Target == null) return;
            float n = Mathf.PerlinNoise(seed, Time.time * 3.2f) - 0.5f;
            Target.intensity = BaseIntensity * (1f + n * Amount * 2f);
        }
    }
}
