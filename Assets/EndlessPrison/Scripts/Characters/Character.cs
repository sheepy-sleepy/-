using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    public enum Team { Player, Enemy }

    public class WeaponDef
    {
        public string Name;
        public int DiceCount;
        public int DiceSides;
        public int FlatBonus;      // 空手打擊之類的固定傷害
        public bool Finesse;       // 靈巧武器：用 STR 或 DEX 較高的那個
        public string Visual;      // "bar"、"dagger" 或 null
        public string Note;        // 武器說明（可以帶一點自我懷疑的語氣）

        public static WeaponDef Fists()
        {
            return new WeaponDef { Name = "拳頭", DiceCount = 0, DiceSides = 0, FlatBonus = 1, Note = "指節上有舊繭。你不記得是怎麼磨出來的。" };
        }

        public static WeaponDef IronBar()
        {
            return new WeaponDef { Name = "生鏽鐵條", DiceCount = 1, DiceSides = 6, Visual = "bar", Note = "握起來的重量剛剛好。熟練得令人反胃。" };
        }

        public static WeaponDef Dagger()
        {
            return new WeaponDef { Name = "自製短刀", DiceCount = 1, DiceSides = 4, Finesse = true, Visual = "dagger" };
        }

        public string DamageText(int mod)
        {
            if (DiceCount == 0) return (FlatBonus + mod).ToString();
            return DiceCount + "d" + DiceSides + Dice.Signed(mod);
        }
    }

    /// <summary>
    /// 角色：能力值、HP、AC、AP、移動，以及驅動 HumanoidRig 做動作。
    /// </summary>
    public class Character : MonoBehaviour
    {
        public const int MaxAP = 4;
        public const int Proficiency = 2;

        public string DisplayName;
        public Team Team;
        public int STR = 10, DEX = 10, CON = 10, INT = 10, WIS = 10, CHA = 10;
        public int MaxHP = 10;
        public int HP = 10;
        public int ArmorBonus;     // 護甲提供的額外 AC
        public WeaponDef Weapon = WeaponDef.Fists();
        public float MoveMax = 6f; // 每回合可移動的公尺數（另外計算，不花 AP）

        public int AP;
        public float MoveLeft;
        public bool Prone;
        public bool Dodging;       // 專注閃避：攻擊自己的人有劣勢，直到自己的下個回合
        public bool UsedSneakThisTurn;

        public Vector2Int Cell;
        public GridMap Map;
        public HumanoidRig Rig;

        float targetYaw;
        public bool IsAlive { get { return HP > 0 && gameObject.activeSelf; } }
        public bool IsMoving { get; private set; }

        public int AC { get { return 10 + Dice.Mod(DEX) + ArmorBonus; } }

        public int WeaponMod
        {
            get { return Weapon.Finesse ? Mathf.Max(Dice.Mod(STR), Dice.Mod(DEX)) : Dice.Mod(STR); }
        }

        public int AttackBonus { get { return Proficiency + WeaponMod; } }
        public int ThrowBonus { get { return Proficiency + Dice.Mod(DEX); } }

        public Vector3 Forward { get { return transform.forward; } }

        public void Init(GridMap map, Vector2Int cell, float yaw)
        {
            Map = map;
            Cell = cell;
            transform.position = GridMap.ToWorld(cell);
            targetYaw = yaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (!map.Characters.Contains(this)) map.Characters.Add(this);

            // 點擊用的碰撞體
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.35f;
        }

        public void SetWeapon(WeaponDef w)
        {
            Weapon = w;
            if (Rig != null) Rig.SetWeapon(w.Visual);
        }

        public void StartTurn()
        {
            AP = MaxAP;
            MoveLeft = MoveMax;
            Dodging = false;
            UsedSneakThisTurn = false;
        }

        void Update()
        {
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, 540f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void FaceTowards(Vector3 worldPos)
        {
            Vector3 d = worldPos - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) targetYaw = Quaternion.LookRotation(d).eulerAngles.y;
        }

        public void FaceTowards(Vector2Int cell) { FaceTowards(GridMap.ToWorld(cell)); }

        public void SetYaw(float yaw) { targetYaw = yaw; }

        /// <summary>
        /// 沿路徑一格一格走過去。onStep 在每踏進新的一格時呼叫（用來判斷腳步聲、水窪）。
        /// shouldStop 回傳 true 時會在下一格停下（例如被敵人發現）。
        /// </summary>
        public IEnumerator MoveAlong(List<Vector2Int> path, float speed, Action<Vector2Int> onStep, Func<bool> shouldStop)
        {
            if (path == null || path.Count == 0) yield break;
            IsMoving = true;
            foreach (var next in path)
            {
                if (!IsAlive) break;
                Vector3 from = transform.position;
                Vector3 to = GridMap.ToWorld(next);
                FaceTowards(to);
                float dist = Vector3.Distance(from, to);
                float t = 0f;
                Cell = next;
                while (t < 1f)
                {
                    t += Time.deltaTime * speed / Mathf.Max(0.01f, dist);
                    transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t));
                    if (Rig != null) Rig.MoveSpeed = Mathf.Lerp(Rig.MoveSpeed, speed, Time.deltaTime * 10f);
                    yield return null;
                }
                if (onStep != null) onStep(next);
                if (shouldStop != null && shouldStop()) break;
            }
            if (Rig != null) Rig.MoveSpeed = 0f;
            IsMoving = false;
        }

        void LateUpdate()
        {
            if (!IsMoving && Rig != null) Rig.MoveSpeed = Mathf.MoveTowards(Rig.MoveSpeed, 0f, Time.deltaTime * 8f);
        }

        /// <summary>扣血。回傳是否倒下。</summary>
        public bool TakeDamage(int amount)
        {
            HP = Mathf.Max(0, HP - amount);
            return HP <= 0;
        }

        public void Heal(int amount)
        {
            HP = Mathf.Min(MaxHP, HP + amount);
        }

        public void SetProne(bool value)
        {
            Prone = value;
            if (Rig != null) Rig.SetProne(value);
        }

        /// <summary>目標是否在自己正面 130° 視錐內。</summary>
        public bool InFrontCone(Vector2Int target, float halfAngle = 65f)
        {
            Vector3 d = GridMap.ToWorld(target) - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(Forward, d) <= halfAngle;
        }
    }
}
