using UnityEngine;

namespace EndlessPrison
{
    /// <summary>
    /// 設定整理裡的油畫風配色。不用純黑。
    /// </summary>
    public static class Palette
    {
        public static readonly Color RawUmber = Hex("#2b241e");     // 生赭石（基底）
        public static readonly Color AsphaltBlue = Hex("#1f2429");  // 瀝青灰藍（基底）
        public static readonly Color SkylightCold = Hex("#a8b8c8"); // 天窗冷光
        public static readonly Color CandleOrange = Hex("#d4883a"); // 燭火橘

        public static readonly Color Stone = Hex("#4a4038");
        public static readonly Color StoneDark = Hex("#352e28");
        public static readonly Color Wall = Hex("#3d3530");
        public static readonly Color Iron = Hex("#4d4a47");
        public static readonly Color Rust = Hex("#6e4a32");
        public static readonly Color Straw = Hex("#5e5236");
        public static readonly Color Bone = Hex("#b5a88f");
        public static readonly Color Wood = Hex("#4f3a28");
        public static readonly Color Water = Hex("#2c3640");
        public static readonly Color Abyss = Hex("#0e1013");

        // 人物
        public static readonly Color Skin = Hex("#b98f72");
        public static readonly Color PrisonerCloth = Hex("#6b5d4a");
        public static readonly Color PrisonerPants = Hex("#463b30");
        public static readonly Color RogueCloak = Hex("#33302d");
        public static readonly Color RogueLeather = Hex("#4a3a2c");
        public static readonly Color Hair = Hex("#2e241c");

        // 介面
        public static readonly Color HitGreen = Hex("#7fa65a");
        public static readonly Color HitYellow = Hex("#d6b84a");
        public static readonly Color HitOrange = Hex("#d4883a");
        public static readonly Color HitRed = Hex("#b5473a");

        public static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        /// <summary>輕微抖動顏色，做出手繪色塊的不均勻感。</summary>
        public static Color Jitter(Color c, float amount)
        {
            float d = Random.Range(-amount, amount);
            return new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d * 0.9f), Mathf.Clamp01(c.b + d * 0.8f), c.a);
        }

        /// <summary>命中率顏色：≥75% 綠、50–74% 黃、30–49% 橘、&lt;30% 紅。</summary>
        public static Color ForHitChance(float p)
        {
            if (p >= 0.75f) return HitGreen;
            if (p >= 0.5f) return HitYellow;
            if (p >= 0.3f) return HitOrange;
            return HitRed;
        }
    }
}
