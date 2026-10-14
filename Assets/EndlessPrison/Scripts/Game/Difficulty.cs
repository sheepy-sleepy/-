namespace EndlessPrison
{
    /// <summary>
    /// 難度設定。數字集中在這裡，想調整敵人強弱直接改這個檔案就好。
    /// </summary>
    public class Difficulty
    {
        public string Name;
        public string Description;

        // 敵人
        public int EnemyHP;
        public int EnemyDEX;
        public int EnemyMaxAttacksPerTurn;  // 每回合最多攻擊幾次
        public int EnemyMaxThrowsPerTurn;
        public int EnemyKnives;             // 飛刃數量
        public bool EnemyWarnsBeforeShove;  // 推人之前會先露出意圖，給玩家一回合躲開
        public bool EnemySneakAttack;       // 偷襲 +1d6
        public bool EnemyFlanks;            // 會刻意繞到背後
        public float EnemyVisionRange;
        public float EnemyHearingRange;

        // 玩家
        public bool PlayerFallIsDeath;      // 被推下深坑會不會直接死
        public int CheckDCOffset;           // 調查／開門等檢定的 DC 加減

        public static Difficulty Easy()
        {
            return new Difficulty
            {
                Name = "簡單",
                Description = "敵人比較弱、推人前會先露出意圖；被推下坑會抓住崖邊。推薦第一次玩。",
                EnemyHP = 11,
                EnemyDEX = 12,
                EnemyMaxAttacksPerTurn = 2,
                EnemyMaxThrowsPerTurn = 1,
                EnemyKnives = 2,
                EnemyWarnsBeforeShove = true,
                EnemySneakAttack = false,
                EnemyFlanks = false,
                EnemyVisionRange = 6f,
                EnemyHearingRange = 3.5f,
                PlayerFallIsDeath = false,
                CheckDCOffset = -2,
            };
        }

        public static Difficulty Normal()
        {
            return new Difficulty
            {
                Name = "普通",
                Description = "照原本設計：會繞背偷襲、直接推人下坑。",
                EnemyHP = 15,
                EnemyDEX = 16,
                EnemyMaxAttacksPerTurn = 4,
                EnemyMaxThrowsPerTurn = 2,
                EnemyKnives = 4,
                EnemyWarnsBeforeShove = false,
                EnemySneakAttack = true,
                EnemyFlanks = true,
                EnemyVisionRange = 7f,
                EnemyHearingRange = 4f,
                PlayerFallIsDeath = true,
                CheckDCOffset = 0,
            };
        }
    }
}
