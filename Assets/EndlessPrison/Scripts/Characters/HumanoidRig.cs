using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    public enum ActionAnim { Attack, HeavyAttack, Shove, Throw, Hit, Dodge, Interact, Kick }

    /// <summary>人物外觀設定。</summary>
    public class RigLook
    {
        public Color Shirt = Palette.PrisonerCloth;
        public Color Pants = Palette.PrisonerPants;
        public Color Skin = Palette.Skin;
        public Color Hair = Palette.Hair;
        public Color Feet = Palette.StoneDark;
        public bool Hood;
        public Color HoodColor = Palette.RogueCloak;
        public bool Cloak;
        public bool PocketWatch;
        public float Height = 1f;
        public float Bulk = 1f;
    }

    /// <summary>
    /// 用方塊拼成的關節人偶，加上程式算出來的動作（走路、攻擊、推擊、投擲、受擊、倒地、掉落）。
    /// 刻意保留刻面硬邊，配合設定裡「中低面數」的方向。
    /// 之後如果換成真正的 3D 模型（例如 Mixamo），只要換掉這個元件，Character 的呼叫方式不用改。
    /// </summary>
    public class HumanoidRig : MonoBehaviour
    {
        // 關節順序
        const int Torso = 0, Head = 1, ShL = 2, ElL = 3, ShR = 4, ElR = 5, HipL = 6, KnL = 7, HipR = 8, KnR = 9;
        const int JointCount = 10;

        class Pose
        {
            public readonly Vector3[] J = new Vector3[JointCount];
            public Vector3 BodyPos;
            public Vector3 BodyRot;

            public Pose Clone()
            {
                var p = new Pose { BodyPos = BodyPos, BodyRot = BodyRot };
                Array.Copy(J, p.J, JointCount);
                return p;
            }

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                var p = new Pose
                {
                    BodyPos = Vector3.LerpUnclamped(a.BodyPos, b.BodyPos, t),
                    BodyRot = Vector3.LerpUnclamped(a.BodyRot, b.BodyRot, t)
                };
                for (int i = 0; i < JointCount; i++) p.J[i] = Vector3.LerpUnclamped(a.J[i], b.J[i], t);
                return p;
            }
        }

        // 動作關鍵影格：時間（0~1）＋姿勢。姿勢是 null 代表「當下的基本姿勢」。
        struct Key
        {
            public float T;
            public Pose P;
            public Key(float t, Pose p) { T = t; P = p; }
        }

        Transform body;
        readonly Transform[] joints = new Transform[JointCount];
        Vector3 bodyBasePos;
        Transform rightHand;
        GameObject weapon;

        public float MoveSpeed;     // 由 Character 每幀設定（公尺／秒）
        public bool CombatStance;
        float walkPhase;
        float proneWeight;
        bool prone;
        bool dead;
        bool falling;

        List<Key> actionKeys;
        float actionTime;
        float actionDuration;
        int actionId;

        public Transform RightHand { get { return rightHand; } }

        public static HumanoidRig Build(Transform root, RigLook look)
        {
            var rig = root.gameObject.AddComponent<HumanoidRig>();
            rig.Construct(look);
            return rig;
        }

        Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        void Construct(RigLook look)
        {
            float b = look.Bulk;
            body = Pivot("Body", transform, new Vector3(0f, 0.95f * look.Height, 0f));
            body.localScale = Vector3.one * look.Height;
            bodyBasePos = body.localPosition;

            MatLib.Prim(PrimitiveType.Cube, body, Vector3.zero, new Vector3(0.36f * b, 0.18f, 0.22f * b), look.Pants, true, "Hips");

            var torso = joints[Torso] = Pivot("Torso", body, new Vector3(0f, 0.08f, 0f));
            MatLib.Prim(PrimitiveType.Cube, torso, new Vector3(0f, 0.3f, 0f), new Vector3(0.42f * b, 0.55f, 0.24f * b), look.Shirt, true, "Chest");
            // 腰帶
            MatLib.Prim(PrimitiveType.Cube, torso, new Vector3(0f, 0.04f, 0f), new Vector3(0.44f * b, 0.07f, 0.26f * b), Palette.RogueLeather, true, "Belt");
            if (look.Cloak)
                MatLib.Prim(PrimitiveType.Cube, torso, new Vector3(0f, 0.18f, -0.15f * b), new Vector3(0.5f * b, 0.85f, 0.06f), look.HoodColor, true, "Cloak");
            if (look.PocketWatch)
            {
                // 主角那只銀懷錶，掛在腰帶上
                MatLib.Prim(PrimitiveType.Cylinder, torso, new Vector3(0.15f * b, 0.0f, 0.14f * b), new Vector3(0.08f, 0.012f, 0.08f), Palette.SkylightCold, true, "PocketWatch")
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            var head = joints[Head] = Pivot("Head", torso, new Vector3(0f, 0.6f, 0f));
            MatLib.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.05f, 0f), new Vector3(0.11f, 0.1f, 0.11f), look.Skin, true, "Neck");
            MatLib.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.2f, 0.01f), new Vector3(0.24f, 0.27f, 0.25f), look.Skin, true, "Skull");
            MatLib.Prim(PrimitiveType.Cube, head, new Vector3(-0.055f, 0.22f, 0.13f), new Vector3(0.04f, 0.03f, 0.02f), Palette.AsphaltBlue, true, "EyeL");
            MatLib.Prim(PrimitiveType.Cube, head, new Vector3(0.055f, 0.22f, 0.13f), new Vector3(0.04f, 0.03f, 0.02f), Palette.AsphaltBlue, true, "EyeR");
            if (look.Hood)
            {
                MatLib.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.26f, -0.02f), new Vector3(0.3f, 0.3f, 0.3f), look.HoodColor, true, "HoodBack");
                MatLib.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.37f, 0.08f), new Vector3(0.3f, 0.08f, 0.16f), look.HoodColor, true, "HoodBrim");
            }
            else
            {
                MatLib.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.34f, -0.01f), new Vector3(0.26f, 0.06f, 0.27f), look.Hair, true, "Hair");
                MatLib.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.24f, -0.11f), new Vector3(0.26f, 0.2f, 0.06f), look.Hair, true, "HairBack");
            }

            BuildArm(torso, -1, look, out joints[ShL], out joints[ElL]);
            Transform hand;
            BuildArm(torso, 1, look, out joints[ShR], out joints[ElR], out hand);
            rightHand = hand;

            BuildLeg(body, -1, look, out joints[HipL], out joints[KnL]);
            BuildLeg(body, 1, look, out joints[HipR], out joints[KnR]);
        }

        void BuildArm(Transform torso, int side, RigLook look, out Transform shoulder, out Transform elbow)
        {
            Transform hand;
            BuildArm(torso, side, look, out shoulder, out elbow, out hand);
        }

        void BuildArm(Transform torso, int side, RigLook look, out Transform shoulder, out Transform elbow, out Transform hand)
        {
            float b = look.Bulk;
            shoulder = Pivot(side < 0 ? "ShoulderL" : "ShoulderR", torso, new Vector3(0.27f * side * b, 0.52f, 0f));
            MatLib.Prim(PrimitiveType.Cube, shoulder, new Vector3(0f, -0.15f, 0f), new Vector3(0.12f * b, 0.3f, 0.12f * b), look.Shirt, true, "UpperArm");
            elbow = Pivot(side < 0 ? "ElbowL" : "ElbowR", shoulder, new Vector3(0f, -0.3f, 0f));
            MatLib.Prim(PrimitiveType.Cube, elbow, new Vector3(0f, -0.14f, 0f), new Vector3(0.1f * b, 0.28f, 0.1f * b), look.Skin, true, "Forearm");
            hand = Pivot(side < 0 ? "HandL" : "HandR", elbow, new Vector3(0f, -0.3f, 0f));
            MatLib.Prim(PrimitiveType.Cube, hand, Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), look.Skin, true, "Fist");
        }

        void BuildLeg(Transform bodyT, int side, RigLook look, out Transform hip, out Transform knee)
        {
            float b = look.Bulk;
            hip = Pivot(side < 0 ? "HipL" : "HipR", bodyT, new Vector3(0.11f * side * b, -0.05f, 0f));
            MatLib.Prim(PrimitiveType.Cube, hip, new Vector3(0f, -0.225f, 0f), new Vector3(0.15f * b, 0.45f, 0.15f * b), look.Pants, true, "Thigh");
            knee = Pivot(side < 0 ? "KneeL" : "KneeR", hip, new Vector3(0f, -0.45f, 0f));
            MatLib.Prim(PrimitiveType.Cube, knee, new Vector3(0f, -0.21f, 0f), new Vector3(0.13f * b, 0.42f, 0.13f * b), look.Pants, true, "Shin");
            MatLib.Prim(PrimitiveType.Cube, knee, new Vector3(0f, -0.43f, 0.05f), new Vector3(0.14f * b, 0.08f, 0.24f), look.Feet, true, "Foot");
        }

        /// <summary>手上拿的武器（鐵條、短刀…）。傳 null 代表空手。</summary>
        public void SetWeapon(string kind)
        {
            if (weapon != null) Destroy(weapon);
            weapon = null;
            if (string.IsNullOrEmpty(kind)) return;

            weapon = new GameObject("Weapon");
            weapon.transform.SetParent(rightHand, false);
            if (kind == "bar")
            {
                // 生鏽鐵條：沿著手掌往前伸
                MatLib.Prim(PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0f, 0.3f), new Vector3(0.05f, 0.05f, 0.75f), Palette.Rust);
            }
            else if (kind == "dagger")
            {
                MatLib.Prim(PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0f, 0.08f), new Vector3(0.05f, 0.05f, 0.1f), Palette.Wood);
                MatLib.Prim(PrimitiveType.Cube, weapon.transform, new Vector3(0f, 0f, 0.24f), new Vector3(0.02f, 0.05f, 0.24f), Palette.SkylightCold);
            }
        }

        public void SetProne(bool value) { prone = value; }

        /// <summary>直接切到倒地／站立姿勢，不播過渡（開場躺在地上用）。</summary>
        public void SnapProne(bool value)
        {
            prone = value;
            proneWeight = value ? 1f : 0f;
        }

        public void SetDead()
        {
            dead = true;
            prone = true;
            actionId++;
            actionKeys = null;
        }

        public bool IsBusy { get { return actionKeys != null; } }

        // ───────────────────────── 動作播放 ─────────────────────────

        /// <summary>
        /// 播放一個動作。impactAt（0~1）時呼叫 onImpact（例如攻擊打中的瞬間扣血）。
        /// 用法：yield return rig.Play(ActionAnim.Attack, () => { ... });
        /// </summary>
        public IEnumerator Play(ActionAnim anim, Action onImpact)
        {
            float duration;
            float impactAt;
            int id = ++actionId;
            actionKeys = BuildKeys(anim, out duration, out impactAt);
            actionDuration = duration;
            actionTime = 0f;
            bool fired = false;
            // 如果中途被別的動作打斷（例如攻擊到一半被打中），就交給新的動作
            while (actionId == id && actionTime < actionDuration)
            {
                actionTime += Time.deltaTime;
                if (!fired && actionTime / actionDuration >= impactAt)
                {
                    fired = true;
                    if (onImpact != null) onImpact();
                }
                yield return null;
            }
            if (!fired && onImpact != null) onImpact();
            if (actionId == id) actionKeys = null;
        }

        /// <summary>跌進深坑：手腳亂揮，一路往下掉，最後消失。</summary>
        public IEnumerator FallInto(Vector3 pitWorld)
        {
            falling = true;
            Vector3 start = transform.position;
            Vector3 edge = new Vector3(pitWorld.x, start.y, pitWorld.z);
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, edge, t / 0.35f) + Vector3.up * Mathf.Sin(t / 0.35f * Mathf.PI) * 0.3f;
                yield return null;
            }
            float v = 0f;
            float fallTime = 0f;
            while (fallTime < 1.6f)
            {
                fallTime += Time.deltaTime;
                v += 9.8f * Time.deltaTime;
                transform.position += Vector3.down * v * Time.deltaTime;
                transform.Rotate(Vector3.right, 140f * Time.deltaTime, Space.Self);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        static Pose P() { return new Pose(); }

        List<Key> BuildKeys(ActionAnim anim, out float duration, out float impactAt)
        {
            var k = new List<Key> { new Key(0f, null) };
            Pose a, c;
            switch (anim)
            {
                case ActionAnim.Attack:
                case ActionAnim.HeavyAttack:
                {
                    bool heavy = anim == ActionAnim.HeavyAttack;
                    duration = heavy ? 0.95f : 0.7f;
                    impactAt = 0.55f;
                    a = P(); // 舉高蓄力
                    a.J[ShR] = new Vector3(heavy ? -175f : -150f, 0f, 15f);
                    a.J[ElR] = new Vector3(-50f, 0f, 0f);
                    a.J[ShL] = new Vector3(-40f, 0f, -10f);
                    a.J[ElL] = new Vector3(-60f, 0f, 0f);
                    a.J[Torso] = new Vector3(-8f, -25f, 0f);
                    a.J[HipR] = new Vector3(10f, 0f, 0f);
                    a.J[KnR] = new Vector3(20f, 0f, 0f);
                    a.BodyPos = new Vector3(0f, -0.04f, -0.05f);
                    c = P(); // 往前劈下
                    c.J[ShR] = new Vector3(-25f, 0f, 0f);
                    c.J[ElR] = new Vector3(-5f, 0f, 0f);
                    c.J[ShL] = new Vector3(15f, 0f, -20f);
                    c.J[Torso] = new Vector3(heavy ? 28f : 18f, 18f, 0f);
                    c.J[Head] = new Vector3(-10f, 0f, 0f);
                    c.J[HipL] = new Vector3(-30f, 0f, 0f);
                    c.J[KnL] = new Vector3(35f, 0f, 0f);
                    c.J[HipR] = new Vector3(20f, 0f, 0f);
                    c.BodyPos = new Vector3(0f, -0.1f, heavy ? 0.28f : 0.18f);
                    k.Add(new Key(heavy ? 0.45f : 0.4f, a));
                    k.Add(new Key(0.58f, c));
                    k.Add(new Key(0.75f, c));
                    break;
                }
                case ActionAnim.Shove:
                {
                    duration = 0.7f;
                    impactAt = 0.5f;
                    a = P(); // 雙手收到胸前、蹲低
                    a.J[ShL] = new Vector3(-30f, 0f, -10f);
                    a.J[ShR] = new Vector3(-30f, 0f, 10f);
                    a.J[ElL] = new Vector3(-110f, 0f, 0f);
                    a.J[ElR] = new Vector3(-110f, 0f, 0f);
                    a.J[Torso] = new Vector3(5f, 0f, 0f);
                    a.J[HipL] = new Vector3(-20f, 0f, 0f);
                    a.J[KnL] = new Vector3(30f, 0f, 0f);
                    a.J[HipR] = new Vector3(15f, 0f, 0f);
                    a.J[KnR] = new Vector3(25f, 0f, 0f);
                    a.BodyPos = new Vector3(0f, -0.12f, -0.05f);
                    c = P(); // 雙手猛推出去
                    c.J[ShL] = new Vector3(-88f, 0f, 8f);
                    c.J[ShR] = new Vector3(-88f, 0f, -8f);
                    c.J[ElL] = new Vector3(-5f, 0f, 0f);
                    c.J[ElR] = new Vector3(-5f, 0f, 0f);
                    c.J[Torso] = new Vector3(22f, 0f, 0f);
                    c.J[Head] = new Vector3(-12f, 0f, 0f);
                    c.J[HipL] = new Vector3(-40f, 0f, 0f);
                    c.J[KnL] = new Vector3(40f, 0f, 0f);
                    c.J[HipR] = new Vector3(28f, 0f, 0f);
                    c.J[KnR] = new Vector3(5f, 0f, 0f);
                    c.BodyPos = new Vector3(0f, -0.1f, 0.32f);
                    k.Add(new Key(0.35f, a));
                    k.Add(new Key(0.52f, c));
                    k.Add(new Key(0.72f, c));
                    break;
                }
                case ActionAnim.Kick:
                {
                    duration = 0.7f;
                    impactAt = 0.5f;
                    a = P();
                    a.J[HipR] = new Vector3(-30f, 0f, 0f);
                    a.J[KnR] = new Vector3(100f, 0f, 0f);
                    a.J[Torso] = new Vector3(-10f, 0f, 0f);
                    a.J[ShL] = new Vector3(-30f, 0f, -30f);
                    a.J[ShR] = new Vector3(-30f, 0f, 30f);
                    c = P();
                    c.J[HipR] = new Vector3(-80f, 0f, 0f);
                    c.J[KnR] = new Vector3(5f, 0f, 0f);
                    c.J[Torso] = new Vector3(-18f, 0f, 0f);
                    c.J[ShL] = new Vector3(-20f, 0f, -40f);
                    c.J[ShR] = new Vector3(-20f, 0f, 40f);
                    c.BodyPos = new Vector3(0f, 0f, 0.1f);
                    k.Add(new Key(0.35f, a));
                    k.Add(new Key(0.52f, c));
                    k.Add(new Key(0.7f, c));
                    break;
                }
                case ActionAnim.Throw:
                {
                    duration = 0.7f;
                    impactAt = 0.55f;
                    a = P();
                    a.J[ShR] = new Vector3(-155f, 0f, 25f);
                    a.J[ElR] = new Vector3(-95f, 0f, 0f);
                    a.J[ShL] = new Vector3(-75f, 0f, 0f);
                    a.J[Torso] = new Vector3(-6f, -35f, 0f);
                    a.J[HipR] = new Vector3(12f, 0f, 0f);
                    a.BodyPos = new Vector3(0f, 0f, -0.06f);
                    c = P();
                    c.J[ShR] = new Vector3(-55f, 0f, 0f);
                    c.J[ElR] = new Vector3(-10f, 0f, 0f);
                    c.J[ShL] = new Vector3(10f, 0f, -15f);
                    c.J[Torso] = new Vector3(15f, 25f, 0f);
                    c.J[HipL] = new Vector3(-25f, 0f, 0f);
                    c.J[KnL] = new Vector3(25f, 0f, 0f);
                    c.BodyPos = new Vector3(0f, -0.05f, 0.12f);
                    k.Add(new Key(0.4f, a));
                    k.Add(new Key(0.58f, c));
                    k.Add(new Key(0.72f, c));
                    break;
                }
                case ActionAnim.Hit:
                {
                    duration = 0.45f;
                    impactAt = 0f;
                    a = P();
                    a.J[Torso] = new Vector3(-20f, 8f, 0f);
                    a.J[Head] = new Vector3(-18f, 0f, 0f);
                    a.J[ShL] = new Vector3(25f, 0f, -25f);
                    a.J[ShR] = new Vector3(25f, 0f, 25f);
                    a.J[HipL] = new Vector3(10f, 0f, 0f);
                    a.J[KnL] = new Vector3(20f, 0f, 0f);
                    a.BodyPos = new Vector3(0f, -0.04f, -0.15f);
                    k.Add(new Key(0.25f, a));
                    break;
                }
                case ActionAnim.Dodge:
                {
                    duration = 0.55f;
                    impactAt = 0.4f;
                    a = P();
                    a.J[Torso] = new Vector3(15f, 0f, 18f);
                    a.J[HipL] = new Vector3(-35f, 0f, -10f);
                    a.J[KnL] = new Vector3(55f, 0f, 0f);
                    a.J[HipR] = new Vector3(-20f, 0f, 10f);
                    a.J[KnR] = new Vector3(45f, 0f, 0f);
                    a.J[ShL] = new Vector3(-50f, 0f, -20f);
                    a.J[ElL] = new Vector3(-80f, 0f, 0f);
                    a.J[ShR] = new Vector3(-50f, 0f, 20f);
                    a.J[ElR] = new Vector3(-80f, 0f, 0f);
                    a.BodyPos = new Vector3(-0.25f, -0.2f, -0.05f);
                    k.Add(new Key(0.35f, a));
                    k.Add(new Key(0.6f, a));
                    break;
                }
                default: // Interact：蹲下翻找
                {
                    duration = 0.9f;
                    impactAt = 0.55f;
                    a = P();
                    a.J[HipL] = new Vector3(-75f, 0f, 0f);
                    a.J[KnL] = new Vector3(110f, 0f, 0f);
                    a.J[HipR] = new Vector3(-40f, 0f, 0f);
                    a.J[KnR] = new Vector3(120f, 0f, 0f);
                    a.J[Torso] = new Vector3(35f, 0f, 0f);
                    a.J[Head] = new Vector3(10f, 0f, 0f);
                    a.J[ShR] = new Vector3(-70f, 0f, 0f);
                    a.J[ElR] = new Vector3(-20f, 0f, 0f);
                    a.J[ShL] = new Vector3(-40f, 0f, -10f);
                    a.BodyPos = new Vector3(0f, -0.38f, 0f);
                    c = a.Clone();
                    c.J[ShR] = new Vector3(-55f, -20f, 0f);
                    k.Add(new Key(0.35f, a));
                    k.Add(new Key(0.6f, c));
                    k.Add(new Key(0.75f, a));
                    break;
                }
            }
            k.Add(new Key(1f, null));
            return k;
        }

        // ───────────────────────── 每幀計算姿勢 ─────────────────────────

        Pose BasePose(float time)
        {
            var p = new Pose();
            float walk = Mathf.Clamp01(MoveSpeed / 2.2f);
            walkPhase += Time.deltaTime * (2f + MoveSpeed * 3.2f) * (walk > 0.01f ? 1f : 0f);
            float s = Mathf.Sin(walkPhase);
            float cL = Mathf.Max(0f, -Mathf.Cos(walkPhase));
            float cR = Mathf.Max(0f, Mathf.Cos(walkPhase));

            // 待機：呼吸、手微微張開
            float breathe = Mathf.Sin(time * 1.7f) * 1.5f;
            p.J[Torso] = new Vector3(breathe, 0f, 0f);
            p.J[Head] = new Vector3(-breathe * 0.5f, Mathf.Sin(time * 0.37f) * 6f, 0f);
            p.J[ShL] = new Vector3(0f, 0f, -6f);
            p.J[ShR] = new Vector3(0f, 0f, 6f);
            p.J[ElL] = new Vector3(-12f, 0f, 0f);
            p.J[ElR] = new Vector3(-12f, 0f, 0f);

            // 戰鬥架勢：膝蓋微彎、手抬起
            if (CombatStance)
            {
                float g = 1f - walk;
                p.J[Torso] += new Vector3(8f, 0f, 0f) * g;
                p.J[ShR] += new Vector3(-35f, 0f, 0f) * g;
                p.J[ElR] += new Vector3(-55f, 0f, 0f) * g;
                p.J[ShL] += new Vector3(-25f, 0f, -6f) * g;
                p.J[ElL] += new Vector3(-70f, 0f, 0f) * g;
                p.J[HipL] = new Vector3(-14f, 0f, -4f) * g;
                p.J[KnL] = new Vector3(20f, 0f, 0f) * g;
                p.J[HipR] = new Vector3(-6f, 0f, 4f) * g;
                p.J[KnR] = new Vector3(14f, 0f, 0f) * g;
                p.BodyPos = new Vector3(0f, -0.05f, 0f) * g;
            }

            // 走路：腿前後擺、膝蓋在抬腿時彎、手反方向擺
            p.J[HipL] += new Vector3(-s * 32f, 0f, 0f) * walk;
            p.J[HipR] += new Vector3(s * 32f, 0f, 0f) * walk;
            p.J[KnL] += new Vector3(cL * 50f, 0f, 0f) * walk;
            p.J[KnR] += new Vector3(cR * 50f, 0f, 0f) * walk;
            p.J[ShL] += new Vector3(s * 28f, 0f, 0f) * walk;
            p.J[ShR] += new Vector3(-s * 28f, 0f, 0f) * walk;
            p.J[ElL] += new Vector3(-15f, 0f, 0f) * walk;
            p.J[ElR] += new Vector3(-15f, 0f, 0f) * walk;
            p.J[Torso] += new Vector3(6f, s * 6f, 0f) * walk;
            p.BodyPos += new Vector3(0f, Mathf.Abs(Mathf.Cos(walkPhase)) * 0.05f - 0.02f, 0f) * walk;
            return p;
        }

        static Pose PronePose(bool isDead)
        {
            var p = new Pose();
            p.BodyRot = new Vector3(-88f, 0f, 0f);
            p.BodyPos = new Vector3(0f, -0.78f, -0.2f);
            p.J[ShL] = new Vector3(-20f, 0f, -55f);
            p.J[ShR] = new Vector3(-30f, 0f, 60f);
            p.J[ElL] = new Vector3(-30f, 0f, 0f);
            p.J[ElR] = new Vector3(-15f, 0f, 0f);
            p.J[KnL] = new Vector3(25f, 0f, 0f);
            p.J[HipL] = new Vector3(-20f, 0f, -6f);
            p.J[HipR] = new Vector3(0f, 0f, 8f);
            p.J[Head] = new Vector3(0f, isDead ? 50f : 15f, 0f);
            return p;
        }

        Pose SampleAction(Pose basePose)
        {
            float t = Mathf.Clamp01(actionTime / actionDuration);
            for (int i = 0; i < actionKeys.Count - 1; i++)
            {
                var k0 = actionKeys[i];
                var k1 = actionKeys[i + 1];
                if (t <= k1.T)
                {
                    float u = Mathf.InverseLerp(k0.T, k1.T, t);
                    u = u * u * (3f - 2f * u); // 平滑
                    return Pose.Lerp(k0.P ?? basePose, k1.P ?? basePose, u);
                }
            }
            return basePose;
        }

        void LateUpdate()
        {
            if (body == null || falling) return;

            Pose pose = BasePose(Time.time);
            if (actionKeys != null && !dead) pose = SampleAction(pose);

            proneWeight = Mathf.MoveTowards(proneWeight, prone ? 1f : 0f, Time.deltaTime * 2.5f);
            if (proneWeight > 0f)
            {
                float w = proneWeight * proneWeight * (3f - 2f * proneWeight);
                pose = Pose.Lerp(pose, PronePose(dead), w);
            }

            body.localPosition = bodyBasePos + pose.BodyPos;
            body.localRotation = Quaternion.Euler(pose.BodyRot);
            for (int i = 0; i < JointCount; i++)
                if (joints[i] != null) joints[i].localRotation = Quaternion.Euler(pose.J[i]);
        }
    }
}
