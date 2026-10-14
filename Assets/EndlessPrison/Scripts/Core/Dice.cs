using UnityEngine;

namespace EndlessPrison
{
    public enum RollMode { Normal, Advantage, Disadvantage }

    public struct D20Result
    {
        public int first;
        public int second;   // 優勢／劣勢時的第二顆骰，沒有就是 0
        public int natural;  // 實際採用的骰面
        public int bonus;
        public RollMode mode;
        public int Total { get { return natural + bonus; } }
        public bool IsCrit { get { return natural == 20; } }
        public bool IsFumble { get { return natural == 1; } }
    }

    public static class Dice
    {
        public static int Roll(int sides)
        {
            return Random.Range(1, sides + 1);
        }

        public static int Roll(int count, int sides)
        {
            int sum = 0;
            for (int i = 0; i < count; i++) sum += Roll(sides);
            return sum;
        }

        public static D20Result D20(int bonus, RollMode mode)
        {
            var r = new D20Result { bonus = bonus, mode = mode, first = Roll(20) };
            r.natural = r.first;
            if (mode != RollMode.Normal)
            {
                r.second = Roll(20);
                r.natural = mode == RollMode.Advantage ? Mathf.Max(r.first, r.second) : Mathf.Min(r.first, r.second);
            }
            return r;
        }

        public static int Mod(int score)
        {
            return Mathf.FloorToInt((score - 10) / 2f);
        }

        public static string Signed(int v)
        {
            return v >= 0 ? "+" + v : v.ToString();
        }

        /// <summary>
        /// 命中率：(21 − (AC − 命中加值)) / 20，限制在 5%–95%。
        /// 優勢 1 − (1−P)²，劣勢 P²。
        /// </summary>
        public static float HitChance(int attackBonus, int ac, RollMode mode)
        {
            float p = Mathf.Clamp((21f - (ac - attackBonus)) / 20f, 0.05f, 0.95f);
            if (mode == RollMode.Advantage) p = 1f - (1f - p) * (1f - p);
            else if (mode == RollMode.Disadvantage) p = p * p;
            return p;
        }

        /// <summary>同時有優勢和劣勢來源時互相抵銷。</summary>
        public static RollMode Combine(bool advantage, bool disadvantage)
        {
            if (advantage && !disadvantage) return RollMode.Advantage;
            if (disadvantage && !advantage) return RollMode.Disadvantage;
            return RollMode.Normal;
        }

        public static string ModeLabel(RollMode mode)
        {
            switch (mode)
            {
                case RollMode.Advantage: return "優勢";
                case RollMode.Disadvantage: return "劣勢";
                default: return "";
            }
        }
    }
}
