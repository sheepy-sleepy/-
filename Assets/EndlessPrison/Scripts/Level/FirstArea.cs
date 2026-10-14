using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    /// <summary>
    /// 第一區的內容：地圖、物件擺放、調查選項、劇情文字。
    /// 想改文字或檢定難度，改這個檔案就好。
    /// </summary>
    public static class FirstArea
    {
        // 地圖：# 牆　. 地板　, 水窪　O 深坑　G 鐵柵欄　X 往下的階梯
        // 最上面一行是北邊。左下角的 6×6 是起始牢房，右邊是繞著深坑的「深淵環道」。
        public static readonly string[] Layout =
        {
            "##########################",
            "########................X#",
            "#......#.................#",
            "#......#.....OOOOOO......#",
            "#......#.....OOOOOO......#",
            "#......G.....OOOOOO......#",
            "#......#.....OOOOOO......#",
            "#......#........,,.......#",
            "########........,........#",
            "##########################",
        };

        // 石欄斷掉、可以把人推下去的位置（就在敵人旁邊）
        static readonly Vector2Int[] BrokenRail = { new Vector2Int(19, 4), new Vector2Int(19, 5) };

        static readonly Vector2Int PlayerStart = new Vector2Int(3, 4);
        static readonly Vector2Int EnemyStart = new Vector2Int(20, 5);

        public static void Build(GameManager gm)
        {
            var map = new GridMap(Layout);
            gm.Map = map;
            var root = LevelBuilder.Build(map, new HashSet<Vector2Int>(BrokenRail));
            var props = new GameObject("Props").transform;
            props.SetParent(root, false);

            // 燈光：牢房天窗的冷光、環道的火把
            LevelBuilder.AddSkylightBeam(props, new Vector3(3f, 0f, 5f));
            LevelBuilder.AddTorch(props, new Vector3(12f, 1.7f, 8.5f), Vector3.back);
            LevelBuilder.AddTorch(props, new Vector3(20f, 1.7f, 8.5f), Vector3.back);
            LevelBuilder.AddTorch(props, new Vector3(14f, 1.7f, 0.5f), Vector3.forward);
            LevelBuilder.AddTorch(props, new Vector3(24.5f, 1.7f, 3f), Vector3.left);

            BuildStraw(gm, props, new Vector2Int(2, 7));
            BuildShacklePillar(gm, props, new Vector2Int(5, 7));
            BuildTallyWall(gm, props, new Vector2Int(0, 4));
            BuildBowl(gm, props, new Vector2Int(6, 2));
            BuildGate(gm, props, new Vector2Int(7, 4));

            BuildBrazier(gm, props, new Vector2Int(10, 7));
            BuildSkeleton(gm, props, new Vector2Int(9, 1));
            BuildBarrels(gm, props, new Vector2Int(21, 7));
            BuildBarrels(gm, props, new Vector2Int(22, 7));
            BuildStairs(props, new Vector2Int(24, 8));

            gm.Player = CreatePlayer(map);
            gm.Enemies.Add(CreateRogue(map));
        }

        // ───────────────────────── 角色 ─────────────────────────

        static Character CreatePlayer(GridMap map)
        {
            var go = new GameObject("Player");
            var ch = go.AddComponent<Character>();
            ch.DisplayName = "你";
            ch.Team = Team.Player;
            ch.STR = 15; ch.DEX = 13; ch.CON = 14; ch.INT = 10; ch.WIS = 12; ch.CHA = 8;
            ch.MaxHP = ch.HP = 12;
            ch.Rig = HumanoidRig.Build(go.transform, new RigLook { PocketWatch = true });
            ch.Init(map, PlayerStart, 90f);
            ch.SetWeapon(WeaponDef.Fists());
            ch.SetProne(true); // 一開始躺在地上，看完開場才起身
            ch.Rig.SnapProne(true);
            return ch;
        }

        static Character CreateRogue(GridMap map)
        {
            var go = new GameObject("Rogue");
            var ch = go.AddComponent<Character>();
            ch.DisplayName = "不懷好意的男人";
            ch.Team = Team.Enemy;
            ch.STR = 10; ch.DEX = 12; ch.CON = 12; ch.INT = 11; ch.WIS = 12; ch.CHA = 9;
            ch.MaxHP = ch.HP = 11;
            ch.Rig = HumanoidRig.Build(go.transform, new RigLook
            {
                Shirt = Palette.RogueLeather,
                Pants = Palette.RogueCloak,
                Skin = Palette.Hex("#a88470"),
                Hood = true,
                Cloak = true,
                HoodColor = Palette.RogueCloak,
                Height = 1.04f,
                Bulk = 0.88f,
            });
            ch.Init(map, EnemyStart, 90f);
            ch.SetWeapon(WeaponDef.Dagger());
            go.AddComponent<EnemyBrain>();
            return ch;
        }

        public static void ApplyDifficulty(Character enemy, Difficulty d)
        {
            enemy.DEX = d.EnemyDEX;
            enemy.MaxHP = enemy.HP = d.EnemyHP;
            var brain = enemy.GetComponent<EnemyBrain>();
            if (brain != null) brain.Knives = d.EnemyKnives;
        }

        // ───────────────────────── 起始牢房的物件 ─────────────────────────

        static Interactable MakeInteractable(GameManager gm, Transform parent, Vector2Int cell, string title, bool blocks, bool blocksSight)
        {
            var go = new GameObject(title);
            go.transform.SetParent(parent, false);
            var it = go.AddComponent<Interactable>();
            it.Title = title;
            it.Blocks = blocks;
            it.Init(cell, gm.Map, blocksSight);
            go.AddComponent<Sparkle>().Owner = it;
            return it;
        }

        static void BuildStraw(GameManager gm, Transform parent, Vector2Int cell)
        {
            var it = MakeInteractable(gm, parent, cell, "發黑的稻草堆", true, false);
            for (int i = 0; i < 7; i++)
            {
                MatLib.Prim(PrimitiveType.Cube, it.transform, new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0.08f + i * 0.025f, UnityEngine.Random.Range(-0.3f, 0.3f)),
                    new Vector3(UnityEngine.Random.Range(0.4f, 0.7f), 0.08f, UnityEngine.Random.Range(0.15f, 0.3f)),
                    Palette.Jitter(Palette.Straw, 0.05f)).transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 180f), UnityEngine.Random.Range(-8f, 8f));
            }
            bool found = false;
            it.Describe = () => found
                ? "只剩濕冷發霉的稻草。"
                : "發黑的稻草堆，散發著霉味。底下好像壓著什麼東西。";
            it.Options = () => new List<InteractOption>
            {
                new InteractOption
                {
                    Label = "翻找稻草", Check = Ability.INT, DC = 10,
                    Available = () => !found,
                    OnResult = ok =>
                    {
                        if (ok)
                        {
                            found = true;
                            it.Done = true;
                            gm.AddItem("木籤");
                            gm.Hud.ShowModal("木籤", "你摸到一根被削尖的木籤。\n削得很仔細，一刀一刀，像是有人花了很多天。\n\n（可以用來撬鎖）");
                        }
                        else gm.Hud.Log("只摸到濕冷的爛草。（可以再試一次）");
                    }
                }
            };
        }

        static void BuildShacklePillar(GameManager gm, Transform parent, Vector2Int cell)
        {
            var it = MakeInteractable(gm, parent, cell, "斷裂的腳鐐", true, true);
            MatLib.Prim(PrimitiveType.Cylinder, it.transform, new Vector3(0f, 1.3f, 0f), new Vector3(0.55f, 1.3f, 0.55f), Palette.Stone);
            for (int i = 0; i < 4; i++)
                MatLib.Prim(PrimitiveType.Cube, it.transform, new Vector3(0.3f, 0.7f - i * 0.15f, 0.05f * i), new Vector3(0.06f, 0.12f, 0.06f), Palette.Iron)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 20f : -20f);
            var bar = MatLib.Prim(PrimitiveType.Cube, it.transform, new Vector3(0.35f, 0.04f, -0.25f), new Vector3(0.06f, 0.06f, 0.7f), Palette.Rust, true, "LooseBar");
            bar.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);

            bool taken = false;
            it.Describe = () => taken
                ? "空蕩蕩的石柱，只剩半截腳鐐。"
                : "石柱上釘著一副斷裂的腳鐐。鐵環內側磨得發亮，有人戴著它很久很久。\n旁邊有一根鬆脫的生鏽鐵條。";
            it.OnFirstLook = () => gm.AddClue("腳鐐鐵環的尺寸，剛好合你的腳踝。");
            it.Options = () => new List<InteractOption>
            {
                new InteractOption
                {
                    Label = "扳下鐵條", Check = Ability.STR, DC = 9,
                    Available = () => !taken,
                    OnResult = ok =>
                    {
                        if (ok)
                        {
                            taken = true;
                            it.Done = true;
                            UnityEngine.Object.Destroy(bar);
                            gm.Player.SetWeapon(WeaponDef.IronBar());
                            gm.Hud.ShowModal("生鏽鐵條（1d6）", "鐵條在你手中轉了半圈，停在最順手的角度。\n握起來的重量剛剛好。熟練得令人反胃。\n\n（已裝備。攻擊傷害從 1 變成 1d6）");
                        }
                        else gm.Hud.Log("鐵條卡得很死。（可以再試一次）");
                    }
                }
            };
        }

        static void BuildTallyWall(GameManager gm, Transform parent, Vector2Int cell)
        {
            var it = MakeInteractable(gm, parent, cell, "刻痕牆", false, true);
            // 牆面上的刻痕
            for (int g = 0; g < 18; g++)
            {
                int col = g % 6, row = g / 6;
                Vector3 basePos = new Vector3(0.52f, 1.6f - row * 0.28f, -0.38f + col * 0.15f);
                for (int s = 0; s < 4; s++)
                    MatLib.Prim(PrimitiveType.Cube, it.transform, basePos + new Vector3(0f, 0f, s * 0.025f), new Vector3(0.01f, 0.18f, 0.01f), Palette.RawUmber);
                MatLib.Prim(PrimitiveType.Cube, it.transform, basePos + new Vector3(0f, 0f, 0.04f), new Vector3(0.01f, 0.01f, 0.14f), Palette.RawUmber)
                    .transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            }
            it.Describe = () => "牆上刻了 87 道「正」字。最後一道被抓爛了，指甲留下的血痕早已發黑。\n\n旁邊有一行很小的字：\n「深淵不吃活人，但祂討厭噪音……」";
            var bc = it.GetComponent<BoxCollider>();
            bc.center = new Vector3(0.1f, 1.2f, 0f);
            bc.size = new Vector3(1f, 2.4f, 1f);
            it.OnFirstLook = () =>
            {
                it.Done = true;
                gm.AddClue("牆上 87 道正字。「深淵不吃活人，但祂討厭噪音。」");
            };
            it.Options = () => new List<InteractOption>();
        }

        static void BuildBowl(GameManager gm, Transform parent, Vector2Int cell)
        {
            var it = MakeInteractable(gm, parent, cell, "空陶碗", true, false);
            var bowl = MatLib.Prim(PrimitiveType.Cylinder, it.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.3f, 0.06f, 0.3f), Palette.Hex("#7a5a3e"));
            bool broken = false;
            it.Describe = () => broken
                ? "碎掉的陶碗。"
                : "一只空陶碗，碗底黏著乾掉的糊狀物。\n旁邊用炭畫了三道線：三天份的口糧。";
            it.OnFirstLook = () => gm.AddClue("口糧只配給三天。不像是讓人回去的份量。");
            it.Options = () => new List<InteractOption>
            {
                new InteractOption
                {
                    Label = "敲碎，撿起碎片",
                    Hint = "可以投擲（1d4，射程 8m）",
                    Available = () => !broken,
                    OnResult = ok =>
                    {
                        broken = true;
                        it.Done = true;
                        bowl.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
                        gm.AddItem("陶片", 3);
                    }
                }
            };
        }

        static void BuildGate(GameManager gm, Transform parent, Vector2Int cell)
        {
            var it = MakeInteractable(gm, parent, cell, "鐵柵欄", false, false);
            var bars = new GameObject("Bars").transform;
            bars.SetParent(it.transform, false);
            for (int i = 0; i < 7; i++)
            {
                float z = -0.45f + i * 0.15f;
                var b = MatLib.Prim(PrimitiveType.Cube, bars, new Vector3(0f, 1.2f, z), new Vector3(0.06f, 2.4f, 0.06f), Palette.Iron);
                if (i == 3) b.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);  // 被掰彎的那根
                if (i == 4) b.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            }
            MatLib.Prim(PrimitiveType.Cube, bars, new Vector3(0f, 2.3f, 0f), new Vector3(0.08f, 0.08f, 1f), Palette.Iron);
            MatLib.Prim(PrimitiveType.Cube, bars, new Vector3(0f, 0.15f, 0f), new Vector3(0.08f, 0.08f, 1f), Palette.Iron);
            // 鎖：在外側（+x 那一面）
            MatLib.Prim(PrimitiveType.Cube, bars, new Vector3(0.12f, 1.1f, 0.38f), new Vector3(0.12f, 0.22f, 0.14f), Palette.Rust, true, "Lock");

            gm.OnGateOpen = () => gm.StartCoroutine(GateFall(bars));

            it.Describe = () => "鐵柵欄被人從外面掰開了一道縫。\n你湊近一看：鎖孔和門栓，全都在柵欄外側。從裡面根本碰不到。";
            it.OnFirstLook = () => gm.AddClue("牢門的鎖孔和門栓都在外側。這扇門是用來關人的。");
            Action<bool> open = ok =>
            {
                if (!ok) { gm.Hud.Log("柵欄紋絲不動。（可以再試一次，或換個方法）"); return; }
                it.Done = true;
                var col = it.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                gm.OpenGate();
                gm.Hud.Log("柵欄打開了。外面是一條繞著深坑的環道。");
            };
            it.Options = () => new List<InteractOption>
            {
                new InteractOption { Label = "擠過縫隙", Check = Ability.DEX, DC = 12, OnResult = open },
                new InteractOption { Label = "踹開", Check = Ability.STR, DC = 12, Anim = ActionAnim.Kick, OnResult = open },
                new InteractOption
                {
                    Label = "用木籤撬鎖", Check = Ability.DEX, DC = 8, Advantage = true,
                    Hint = "有優勢",
                    Available = () => gm.ItemCount("木籤") > 0,
                    OnResult = open
                },
            };
        }

        static IEnumerator GateFall(Transform bars)
        {
            float t = 0f;
            Quaternion from = bars.localRotation;
            Quaternion to = Quaternion.Euler(0f, 0f, -82f);
            Vector3 p0 = bars.localPosition;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.6f;
                float e = t * t;
                bars.localRotation = Quaternion.Slerp(from, to, e);
                bars.localPosition = p0 + new Vector3(0.1f, 0f, 0f) * e;
                yield return null;
            }
        }

        // ───────────────────────── 深淵環道的物件 ─────────────────────────

        static void BuildBrazier(GameManager gm, Transform parent, Vector2Int cell)
        {
            var it = MakeInteractable(gm, parent, cell, "倒下的火盆", true, false);
            var bowl = MatLib.Prim(PrimitiveType.Cylinder, it.transform, new Vector3(0.1f, 0.3f, 0f), new Vector3(0.7f, 0.18f, 0.7f), Palette.Iron);
            bowl.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            for (int i = 0; i < 6; i++)
            {
                MatLib.Prim(PrimitiveType.Cube, it.transform, new Vector3(-0.2f + UnityEngine.Random.Range(-0.3f, 0.2f), 0.04f, UnityEngine.Random.Range(-0.35f, 0.35f)),
                    Vector3.one * UnityEngine.Random.Range(0.06f, 0.12f), Palette.CandleOrange)
                    .GetComponent<Renderer>().sharedMaterial = MatLib.Glow(Palette.CandleOrange, 2.5f);
            }
            LevelBuilder.AddPointLight(it.transform, GridMap.ToWorld(cell) + new Vector3(-0.2f, 0.5f, 0f), Palette.CandleOrange, 6f, 1.4f, true);

            int stonesLeft = 4;
            it.Describe = () => stonesLeft > 0
                ? "翻倒的火盆，餘燼還在悶燒。四周散落著碎石。"
                : "翻倒的火盆，餘燼還在悶燒。";
            it.Options = () => new List<InteractOption>
            {
                new InteractOption
                {
                    Label = "撿碎石",
                    Hint = "可以投擲（1d4，射程 8m）",
                    Available = () => stonesLeft > 0,
                    OnResult = ok =>
                    {
                        gm.AddItem("碎石", stonesLeft);
                        stonesLeft = 0;
                        it.Done = true;
                    }
                }
            };
        }

        static void BuildSkeleton(GameManager gm, Transform parent, Vector2Int cell)
        {
            var it = MakeInteractable(gm, parent, cell, "靠牆的枯骨", true, false);
            var t = it.transform;
            var c = Palette.Bone;
            MatLib.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.75f, -0.3f), new Vector3(0.2f, 0.22f, 0.22f), c, true, "Skull").transform.localRotation = Quaternion.Euler(25f, 15f, 0f);
            MatLib.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.45f, -0.35f), new Vector3(0.3f, 0.35f, 0.14f), c, true, "Ribs").transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            MatLib.Prim(PrimitiveType.Cube, t, new Vector3(-0.1f, 0.1f, -0.05f), new Vector3(0.06f, 0.06f, 0.55f), c, true, "LegL");
            MatLib.Prim(PrimitiveType.Cube, t, new Vector3(0.12f, 0.1f, -0.02f), new Vector3(0.06f, 0.06f, 0.5f), c, true, "LegR").transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            MatLib.Prim(PrimitiveType.Cube, t, new Vector3(0.22f, 0.25f, -0.2f), new Vector3(0.05f, 0.3f, 0.05f), c, true, "Arm").transform.localRotation = Quaternion.Euler(40f, 0f, 20f);
            var paper = MatLib.Prim(PrimitiveType.Cube, t, new Vector3(0.25f, 0.06f, 0.05f), new Vector3(0.18f, 0.01f, 0.24f), Palette.Hex("#c8b89a"), true, "Letter");

            bool searched = false;
            it.Describe = () => searched
                ? "枯骨安靜地靠著牆。"
                : "靠牆坐著一具枯骨。指骨還扣著一張紙，旁邊有個小布包。";
            it.Options = () => new List<InteractOption>
            {
                new InteractOption
                {
                    Label = "搜身、讀那張紙",
                    Available = () => !searched,
                    OnResult = ok =>
                    {
                        searched = true;
                        it.Done = true;
                        UnityEngine.Object.Destroy(paper);
                        gm.AddItem("乾糧");
                        gm.ReadEscapeLetter = true;
                        gm.AddClue("逃亡遺書：「沒有援軍，也沒有人回去。」");
                        gm.Hud.ShowModal("逃亡遺書",
                            "「如果有人讀到這個——別相信紙條。\n" +
                            "我在這裡等了很多天。沒有援軍，也沒有人回去。\n\n" +
                            "前面那個傢伙不是守衛。他跟我們一樣……只是比我們早醒來。\n" +
                            "他喜歡站在石欄斷掉的地方。別靠近坑邊。\n" +
                            "也別踩水，他耳朵很尖。」\n\n" +
                            "（你提高了警覺：戰鬥先攻 +2。布包裡有一份乾糧，可以回復 HP。）");
                    }
                }
            };
        }

        static void BuildBarrels(GameManager gm, Transform parent, Vector2Int cell)
        {
            var it = MakeInteractable(gm, parent, cell, "碎木桶", true, true);
            MatLib.Prim(PrimitiveType.Cylinder, it.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.65f, 0.5f, 0.65f), Palette.Wood);
            MatLib.Prim(PrimitiveType.Cylinder, it.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.68f, 0.04f, 0.68f), Palette.Iron);
            MatLib.Prim(PrimitiveType.Cylinder, it.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.68f, 0.04f, 0.68f), Palette.Iron);
            it.Describe = () => "幾個碎掉的木桶，散發著酸臭味。\n（木桶會擋住視線，可以躲在後面。）";
            it.Options = () => new List<InteractOption>();
            it.OnFirstLook = () => it.Done = true;
        }

        static void BuildStairs(Transform parent, Vector2Int cell)
        {
            var t = new GameObject("StairsDown").transform;
            t.SetParent(parent, false);
            t.position = GridMap.ToWorld(cell);
            MatLib.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.005f, 0f), new Vector3(0.9f, 0.02f, 0.9f), Palette.Abyss);
            for (int i = 0; i < 3; i++)
                MatLib.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.02f, 0.3f - i * 0.25f), new Vector3(0.8f, 0.03f, 0.2f), Palette.StoneDark);
            LevelBuilder.AddPointLight(t, GridMap.ToWorld(cell) + Vector3.up * 0.4f, Palette.Hex("#5b7a99"), 3f, 1.2f, false);
        }

        // ───────────────────────── 劇情 ─────────────────────────

        public static void ShowTitle(GameManager gm)
        {
            gm.Hud.ShowModal("無盡牢獄",
                "第一區：起始牢房與深淵環道\n\n選擇難度：",
                new ModalButton("簡單（推薦）", () => gm.Begin(Difficulty.Easy()), Difficulty.Easy().Description),
                new ModalButton("普通", () => gm.Begin(Difficulty.Normal()), Difficulty.Normal().Description));
        }

        public static void ShowIntro(GameManager gm, Action onDone)
        {
            gm.Hud.ShowModal("醒來",
                "冰冷的石地貼著你的臉。頭痛欲裂。\n\n" +
                "你想不起自己是怎麼到這裡的，只記得一件事：\n" +
                "你是王國的先遣死士，奉命深入這座地底要塞，討伐盤踞在最底層的巨龍。\n" +
                "為了國家，也為了在家等你的妻子和女兒。\n\n" +
                "腰間的銀懷錶還在。手邊有一張蓋著王室蠟印的羊皮紙。",
                new ModalButton("讀羊皮紙", () =>
                {
                    gm.Hud.ShowModal("王室敕令",
                        "致奉命深入之死士：\n\n" +
                        "汝已越過熔封之門，再無退路。\n" +
                        "向下，直抵龍巢。斬殺巨龍，踏入其後之法陣，即可凱旋。\n\n" +
                        "法陣僅能承載一人之魂。\n" +
                        "願光指引汝之刀刃。\n\n" +
                        "　　　　　　　　　　　　——蓋有王室蠟印",
                        new ModalButton("收起敕令，站起來", () =>
                        {
                            gm.Player.SetProne(false);
                            if (onDone != null) onDone();
                        }));
                }));
        }

        /// <summary>介面上「檢查自己」按鈕。</summary>
        public static void InspectSelf(GameManager gm)
        {
            gm.Hud.ShowModal("檢查自己",
                "你低頭檢查自己。\n\n" +
                "粗布衣服，衣領內側用烙鐵燙著一串編號：IV-089。\n" +
                "銀懷錶的錶鍊扣在腰帶上，有點緊。鎖扣有被硬扯過的變形。\n\n" +
                "……先遣死士，需要編號嗎？");
            gm.AddClue("衣領內側烙著編號 IV-089。");
            gm.AddClue("懷錶的錶鍊有點緊，鎖扣有被硬扯過的變形。");
        }

        public static void OnEnemyKilled(GameManager gm, Character enemy)
        {
            if (enemy.gameObject.activeSelf)
                gm.AddClue("那個男人的後頸也烙著編號：IV-071。他身上沒有任何王國的徽記。");
            else
                gm.AddClue("那個男人墜落前，你瞥見他後頸的烙印：IV-071。");
        }

        public static void ShowVictory(GameManager gm)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("往下的階梯在黑暗中延伸。\n你握緊懷錶。錶鍊扣在手腕上，有點緊。\n……你得繼續往下。為了她們。\n\n");
            if (gm.Clues.Count > 0)
            {
                sb.Append("你記下的線索（" + gm.Clues.Count + "／8）：\n");
                foreach (var c in gm.Clues) sb.Append("・").Append(c).Append("\n");
            }
            else sb.Append("你什麼都沒有察覺。\n");
            if (gm.AnyEnemyAlive()) sb.Append("\n你沒有驚動那個男人，悄悄溜了過去。");
            gm.Hud.ShowModal("第一區　完成", sb.ToString(),
                new ModalButton("重新開始", gm.Restart));
        }

        public static void ShowGameOver(GameManager gm)
        {
            gm.Hud.ShowModal("你倒下了",
                "黑暗裡，你握著懷錶，喃喃說著：「我馬上回去……」\n\n" +
                "提示：\n" +
                "・先在牢房裡撿起鐵條（傷害更高）和陶片（可以遠距離投擲）。\n" +
                "・他站在石欄斷掉的地方，推擊可以直接把他推下坑。\n" +
                "・別站在沒有石欄的坑邊。\n" +
                "・從他背後攻擊有優勢。",
                new ModalButton("重新開始", gm.Restart));
        }
    }

    /// <summary>可調查物件上方的小光點，調查完就消失。</summary>
    public class Sparkle : MonoBehaviour
    {
        public Interactable Owner;
        GameObject dot;

        void Start()
        {
            dot = MatLib.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 1.3f, 0f), Vector3.one * 0.09f, Palette.SkylightCold, true, "Sparkle");
            dot.GetComponent<Renderer>().sharedMaterial = MatLib.Glow(Palette.SkylightCold, 2f);
            dot.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            if (Owner != null && Owner.Title == "刻痕牆") dot.transform.localPosition = new Vector3(0.7f, 1.9f, 0f);
        }

        void Update()
        {
            if (dot == null) return;
            bool show = Owner != null && !Owner.Done;
            if (dot.activeSelf != show) dot.SetActive(show);
            if (!show) return;
            Vector3 p = dot.transform.localPosition;
            p.y = (Owner.Title == "刻痕牆" ? 1.9f : 1.3f) + Mathf.Sin(Time.time * 2f + transform.position.x) * 0.06f;
            dot.transform.localPosition = p;
            dot.transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }
    }
}
