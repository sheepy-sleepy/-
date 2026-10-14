using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    /// <summary>
    /// 「不懷好意的男人」的 AI。
    /// 探索時：站著東張西望（看得到 130° 視錐），聽到腳步聲會轉頭。
    /// 戰鬥時，每回合 4 AP，依序考慮：
    ///   1. 你站在沒有石欄的坑邊 → 推你下去（簡單難度會先露出意圖，給你一回合躲開）
    ///   2. 貼身 → 用短刀攻擊（簡單難度每回合最多 2 次）
    ///   3. 太遠 → 先走近；走不到又在 8m 內 → 丟飛刃
    /// </summary>
    public class EnemyBrain : MonoBehaviour
    {
        public bool Aware;
        public int Knives = 2;
        public bool ShoveTelegraphed;     // 上回合已經「盯著你身後的坑」
        public float[] LookAngles = { 90f, 30f, 90f, 150f, 90f, 270f };

        Character self;
        int lookIndex;
        float lookTimer = 2.5f;
        float heardTimer;

        void Awake()
        {
            self = GetComponent<Character>();
        }

        void Update()
        {
            if (Aware || self == null || !self.IsAlive) return;
            if (heardTimer > 0f)
            {
                heardTimer -= Time.deltaTime;
                return;
            }
            lookTimer -= Time.deltaTime;
            if (lookTimer <= 0f)
            {
                lookIndex = (lookIndex + 1) % LookAngles.Length;
                self.SetYaw(LookAngles[lookIndex]);
                lookTimer = Random.Range(2.2f, 3.6f);
            }
        }

        public void HearNoise(Vector2Int cell)
        {
            if (Aware) return;
            self.FaceTowards(cell);
            heardTimer = 2.5f;
        }

        public bool CanSee(Character target, float range)
        {
            if (GridMap.Distance(self.Cell, target.Cell) > range) return false;
            if (!self.InFrontCone(target.Cell)) return false;
            return self.Map.HasLineOfSight(self.Cell, target.Cell);
        }

        // ───────────────────────── 戰鬥回合 ─────────────────────────

        public IEnumerator TakeTurn(GameManager gm)
        {
            var d = gm.Diff;
            var map = self.Map;
            var player = gm.Player;
            int attacks = 0, throws = 0;
            bool warnedThisTurn = false;
            yield return new WaitForSeconds(0.5f);

            if (self.Prone)
            {
                if (self.AP >= 2) yield return gm.StartCoroutine(gm.StandUpRoutine(self));
                else yield break;
            }

            for (int guard = 0; guard < 12 && self.AP > 0 && player.IsAlive && self.IsAlive; guard++)
            {
                if (GridMap.Adjacent(self.Cell, player.Cell))
                {
                    // 1. 推下深坑
                    var dir = new Vector2Int(System.Math.Sign(player.Cell.x - self.Cell.x), System.Math.Sign(player.Cell.y - self.Cell.y));
                    bool canDropPlayer = map.IsPit(player.Cell + dir) && !map.IsRailed(player.Cell);
                    if (canDropPlayer)
                    {
                        if (!d.EnemyWarnsBeforeShove || ShoveTelegraphed)
                        {
                            ShoveTelegraphed = false;
                            self.AP -= 1;
                            yield return gm.StartCoroutine(gm.ShoveRoutine(self, player));
                            continue;
                        }
                        if (!warnedThisTurn)
                        {
                            warnedThisTurn = true;
                            ShoveTelegraphed = true;
                            gm.Hud.Float(transform.position + Vector3.up * 2.1f, "……", Palette.HitOrange);
                            gm.Hud.Log("他的目光越過你的肩膀，飄向你身後沒有石欄的深坑……（小心：下回合他可能會把你推下去）");
                            yield return new WaitForSeconds(0.6f);
                        }
                    }
                    else ShoveTelegraphed = false;

                    // 2. 近戰攻擊
                    if (attacks < d.EnemyMaxAttacksPerTurn)
                    {
                        attacks++;
                        self.AP -= 1;
                        yield return gm.StartCoroutine(gm.AttackRoutine(self, player, AttackKind.Melee));
                        continue;
                    }
                    break;
                }

                // 3. 不在身邊：能走近就走近，走不到就丟飛刃
                List<Vector2Int> path = ChooseApproach(gm);
                float len = GridMap.PathLength(self.Cell, path);
                bool canReach = path != null && len <= self.MoveLeft + 0.01f;
                float dist = GridMap.Distance(self.Cell, player.Cell);

                if (!canReach && Knives > 0 && throws < d.EnemyMaxThrowsPerTurn && dist <= GameManager.ThrowRange
                    && map.HasLineOfSight(self.Cell, player.Cell))
                {
                    throws++;
                    Knives--;
                    self.AP -= 1;
                    gm.Hud.Log(self.DisplayName + "甩出一把飛刃。（剩 " + Knives + " 把）");
                    yield return gm.StartCoroutine(gm.AttackRoutine(self, player, AttackKind.Throw));
                    continue;
                }

                if (path != null && path.Count > 0 && self.MoveLeft >= 1f)
                {
                    var trimmed = Trim(self.Cell, path, self.MoveLeft);
                    if (trimmed.Count == 0) break;
                    self.MoveLeft -= GridMap.PathLength(self.Cell, trimmed);
                    yield return gm.StartCoroutine(self.MoveAlong(trimmed, GameManager.CombatSpeed, null, null));
                    self.FaceTowards(player.Cell);
                    continue;
                }
                break;
            }
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>選要走去哪一格。普通難度會挑你背後的位置。</summary>
        List<Vector2Int> ChooseApproach(GameManager gm)
        {
            var map = self.Map;
            var player = gm.Player;
            List<Vector2Int> best = null;
            float bestScore = float.MaxValue;
            foreach (var n in map.Neighbours(player.Cell))
            {
                if (!map.IsWalkable(n, self)) continue;
                var path = n == self.Cell ? new List<Vector2Int>() : map.FindPath(self.Cell, n, self);
                if (path == null) continue;
                float score = GridMap.PathLength(self.Cell, path);
                if (gm.Diff.EnemyFlanks && !player.InFrontCone(n)) score -= 2.5f;
                // 想辦法站到「能把你推下坑」的位置
                var dir = new Vector2Int(System.Math.Sign(player.Cell.x - n.x), System.Math.Sign(player.Cell.y - n.y));
                if (map.IsPit(player.Cell + dir) && !map.IsRailed(player.Cell)) score -= 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = path;
                }
            }
            return best;
        }

        /// <summary>把路徑截到這回合剩下的移動距離。</summary>
        static List<Vector2Int> Trim(Vector2Int start, List<Vector2Int> path, float budget)
        {
            var result = new List<Vector2Int>();
            Vector2Int prev = start;
            float used = 0f;
            foreach (var p in path)
            {
                float step = GridMap.Distance(prev, p);
                if (used + step > budget + 0.01f) break;
                used += step;
                result.Add(p);
                prev = p;
            }
            return result;
        }
    }
}
