using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    public class ModalButton
    {
        public string Label;
        public Action OnClick;
        public string Note;

        public ModalButton(string label, Action onClick, string note = null)
        {
            Label = label;
            OnClick = onClick;
            Note = note;
        }
    }

    /// <summary>
    /// 畫面介面（用 Unity 內建的 OnGUI，不需要另外安裝套件）：
    /// 狀態欄、行動列、命中率提示、骰子動畫、對話視窗、紀錄、線索本。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public GameManager Gm;

        const float RefHeight = 900f;
        float scale = 1f;
        float W { get { return Screen.width / scale; } }
        float H { get { return Screen.height / scale; } }

        readonly List<string> logs = new List<string>();
        readonly List<Rect> uiRects = new List<Rect>();

        class FloatText { public Vector3 World; public string Text; public Color Color; public float T; }
        readonly List<FloatText> floats = new List<FloatText>();

        string bannerText;
        float bannerTimer;
        string flashText;
        float flashTimer;

        // 對話視窗
        string modalTitle;
        string modalBody;
        List<ModalButton> modalButtons;
        Interactable modalInteract;
        Vector2 modalScroll;

        // 骰子
        bool rollVisible;
        bool rollRolling;
        string rollLabel;
        D20Result rollResult;
        int rollTarget;
        string rollTargetLabel;
        bool rollSuccess;
        float rollTimer;
        int rollFace = 20;

        bool showJournal;

        GUIStyle sBox, sText, sSmall, sTitle, sBig, sButton, sButtonOn, sCenter, sBanner, sLog;
        Texture2D texPanel, texDark, texButton, texButtonHover, texButtonOn, texWhite;
        Font font;

        public bool HasModal { get { return modalTitle != null; } }

        public void ResetAll()
        {
            logs.Clear();
            floats.Clear();
            modalTitle = null;
            modalButtons = null;
            modalInteract = null;
            rollVisible = false;
            showJournal = false;
            bannerTimer = 0f;
            flashTimer = 0f;
        }

        // ───────────────────────── 給 GameManager 用的 API ─────────────────────────

        public void Log(string line)
        {
            logs.Add(line);
            while (logs.Count > 60) logs.RemoveAt(0);
            Debug.Log("[無盡牢獄] " + line);
        }

        public void Float(Vector3 world, string text, Color color)
        {
            floats.Add(new FloatText { World = world, Text = text, Color = color, T = 0f });
        }

        public void Banner(string text)
        {
            bannerText = text;
            bannerTimer = 1.6f;
        }

        public void Flash(string text)
        {
            flashText = text;
            flashTimer = 1.8f;
        }

        public void ShowModal(string title, string body, params ModalButton[] buttons)
        {
            modalTitle = title;
            modalBody = body;
            modalInteract = null;
            modalScroll = Vector2.zero;
            modalButtons = new List<ModalButton>(buttons);
            if (modalButtons.Count == 0) modalButtons.Add(new ModalButton("繼續", null));
        }

        public void ShowInteract(Interactable it)
        {
            modalTitle = it.Title;
            modalBody = it.Describe != null ? it.Describe() : "";
            modalInteract = it;
            modalButtons = null;
            modalScroll = Vector2.zero;
        }

        void CloseModal()
        {
            modalTitle = null;
            modalButtons = null;
            modalInteract = null;
        }

        /// <summary>顯示擲骰動畫。用 yield return 等它跑完。</summary>
        public IEnumerator ShowRoll(string label, D20Result result, int target, string targetLabel, bool success)
        {
            rollVisible = true;
            rollRolling = true;
            rollLabel = label;
            rollResult = result;
            rollTarget = target;
            rollTargetLabel = targetLabel;
            rollSuccess = success;
            rollTimer = 0f;
            float t = 0f;
            while (t < 0.55f)
            {
                t += Time.deltaTime;
                rollFace = UnityEngine.Random.Range(1, 21);
                yield return null;
            }
            rollRolling = false;
            rollTimer = 1.6f;
            yield return new WaitForSeconds(0.7f);
        }

        public bool IsPointerOverUI(Vector2 mouseBottomLeft)
        {
            if (HasModal) return true;
            Vector2 p = new Vector2(mouseBottomLeft.x, Screen.height - mouseBottomLeft.y);
            foreach (var r in uiRects) if (r.Contains(p)) return true;
            return false;
        }

        // ───────────────────────── 每幀 ─────────────────────────

        void Update()
        {
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                floats[i].T += Time.deltaTime;
                if (floats[i].T > 1.4f) floats.RemoveAt(i);
            }
            if (bannerTimer > 0f) bannerTimer -= Time.deltaTime;
            if (flashTimer > 0f) flashTimer -= Time.deltaTime;
            if (rollVisible && !rollRolling)
            {
                rollTimer -= Time.deltaTime;
                if (rollTimer <= 0f) rollVisible = false;
            }
            if (InputCompat.KeyDown(KeyCode.Tab)) showJournal = !showJournal;
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void EnsureStyles()
        {
            if (sBox != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[]
            {
                "Microsoft JhengHei UI", "Microsoft JhengHei", "Microsoft YaHei UI", "Microsoft YaHei",
                "PingFang TC", "Heiti TC", "Noto Sans CJK TC", "Noto Sans TC", "Arial Unicode MS", "Arial"
            }, 16);

            texPanel = Tex(new Color(0.12f, 0.11f, 0.1f, 0.86f));
            texDark = Tex(new Color(0.07f, 0.07f, 0.08f, 0.94f));
            texButton = Tex(new Color(0.24f, 0.2f, 0.17f, 0.95f));
            texButtonHover = Tex(new Color(0.36f, 0.27f, 0.18f, 0.98f));
            texButtonOn = Tex(new Color(0.55f, 0.35f, 0.15f, 1f));
            texWhite = Tex(Color.white);

            Color ink = Palette.Hex("#e6dccb");

            sBox = new GUIStyle { normal = { background = texPanel }, padding = new RectOffset(12, 12, 10, 10), border = new RectOffset(1, 1, 1, 1) };
            sText = new GUIStyle { font = font, fontSize = 16, wordWrap = true, richText = true, normal = { textColor = ink } };
            sSmall = new GUIStyle(sText) { fontSize = 13, normal = { textColor = Palette.Hex("#b8ad9a") } };
            sLog = new GUIStyle(sText) { fontSize = 14 };
            sTitle = new GUIStyle(sText) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Palette.Hex("#e8b77a") } };
            sBig = new GUIStyle(sText) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sCenter = new GUIStyle(sText) { alignment = TextAnchor.MiddleCenter };
            sBanner = new GUIStyle(sText) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Palette.Hex("#e8b77a") } };
            sButton = new GUIStyle(sText)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                padding = new RectOffset(8, 8, 6, 6),
                normal = { background = texButton, textColor = ink },
                hover = { background = texButtonHover, textColor = Color.white },
                active = { background = texButtonOn, textColor = Color.white },
            };
            sButtonOn = new GUIStyle(sButton) { normal = { background = texButtonOn, textColor = Color.white } };
        }

        Rect Panel(Rect r, Texture2D bg = null)
        {
            GUI.DrawTexture(r, bg ?? texPanel);
            uiRects.Add(new Rect(r.x * scale, r.y * scale, r.width * scale, r.height * scale));
            return r;
        }

        bool Button(Rect r, string label, bool on = false)
        {
            uiRects.Add(new Rect(r.x * scale, r.y * scale, r.width * scale, r.height * scale));
            return GUI.Button(r, label, on ? sButtonOn : sButton);
        }

        void Bar(Rect r, float t, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, texWhite);
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), texWhite);
            GUI.color = old;
        }

        void OnGUI()
        {
            if (Gm == null || Gm.Player == null) return;
            EnsureStyles();
            if (Event.current.type == EventType.Layout) uiRects.Clear();

            scale = Mathf.Max(0.7f, Screen.height / RefHeight);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            DrawFloats();
            if (Gm.State != GameState.Title)
            {
                DrawStatus();
                DrawHelp();
                DrawLog();
                if (Gm.State == GameState.Combat) { DrawInitiative(); DrawActionBar(); }
                else if (Gm.State == GameState.Explore) DrawInventory();
                if (showJournal) DrawJournal();
                if (!HasModal) DrawHover();
            }
            DrawRoll();
            DrawBannerAndFlash();
            if (HasModal) DrawModal();
        }

        // ───────────────────────── 各區塊 ─────────────────────────

        void DrawStatus()
        {
            var p = Gm.Player;
            var r = Panel(new Rect(16, 16, 320, Gm.State == GameState.Combat ? 150 : 104));
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.Label("<b>你</b>", sText);
            Bar(GUILayoutUtility.GetRect(280, 12), p.HP / (float)p.MaxHP, Palette.HitRed);
            GUILayout.Label("HP " + p.HP + " / " + p.MaxHP + "　　AC " + p.AC, sText);
            GUILayout.Label("武器：" + p.Weapon.Name + "（" + p.Weapon.DamageText(p.WeaponMod) + "）　命中 " + Dice.Signed(p.AttackBonus), sSmall);
            if (Gm.State == GameState.Combat)
            {
                string pips = "";
                for (int i = 0; i < Character.MaxAP; i++) pips += i < p.AP ? "● " : "○ ";
                GUILayout.Label("<color=#e8b77a>AP  " + pips + "</color>　移動 " + p.MoveLeft.ToString("0.#") + " m", sText);
                string st = "";
                if (p.Prone) st += "<color=#d4883a>倒地</color>　";
                if (p.Dodging) st += "<color=#a8b8c8>閃避中</color>　";
                if (st.Length > 0) GUILayout.Label(st, sText);
            }
            GUILayout.EndArea();
        }

        void DrawHelp()
        {
            float w = 330;
            var r = Panel(new Rect(W - w - 16, 16, w, 92));
            GUI.Label(new Rect(r.x + 12, r.y + 8, w - 24, 40),
                "左鍵：移動／調查／攻擊　Q／E：轉鏡頭\n滾輪：縮放　Tab：線索本" + (Gm.State == GameState.Combat ? "　空白鍵：結束回合" : ""), sSmall);
            if (Button(new Rect(r.x + 12, r.y + 52, 140, 30), "線索（" + Gm.Clues.Count + "）", showJournal)) showJournal = !showJournal;
            if (Gm.State == GameState.Explore && !Gm.Busy && Button(new Rect(r.x + 162, r.y + 52, 150, 30), "檢查自己"))
                FirstArea.InspectSelf(Gm);
        }

        void DrawLog()
        {
            var r = Panel(new Rect(16, H - 196, 560, 180));
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 8, r.width - 20, r.height - 16));
            int start = Mathf.Max(0, logs.Count - 7);
            for (int i = start; i < logs.Count; i++)
            {
                var style = i == logs.Count - 1 ? sLog : sSmall;
                GUILayout.Label(logs[i], style);
            }
            GUILayout.EndArea();
        }

        void DrawInventory()
        {
            var r = Panel(new Rect(W - 276, H - 196, 260, 180));
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 8, r.width - 24, r.height - 16));
            GUILayout.Label("<b>物品</b>", sText);
            if (Gm.Inventory.Count == 0) GUILayout.Label("（空手。四處找找看。）", sSmall);
            foreach (var kv in Gm.Inventory) GUILayout.Label("・" + kv.Key + (kv.Value > 1 ? " ×" + kv.Value : ""), sSmall);
            GUILayout.EndArea();
            if (Gm.ItemCount("乾糧") > 0 && Button(new Rect(r.x + r.width - 112, r.y + r.height - 40, 100, 30), "吃乾糧"))
                Gm.EatOutsideCombat();
        }

        void DrawInitiative()
        {
            float w = 90f * Gm.TurnOrder.Count + 20f;
            var r = Panel(new Rect(W / 2 - w / 2, 16, w, 50));
            float x = r.x + 10;
            foreach (var c in Gm.TurnOrder)
            {
                bool cur = c == Gm.Active;
                string name = c == Gm.Player ? "你" : "敵人";
                string text = c.IsAlive ? name + "\n<size=12>" + c.HP + " HP</size>" : "<color=#777>" + name + "\n倒下</color>";
                var style = new GUIStyle(sCenter) { fontSize = 14 };
                if (cur)
                {
                    var old = GUI.color;
                    GUI.color = new Color(0.83f, 0.53f, 0.23f, 0.5f);
                    GUI.DrawTexture(new Rect(x, r.y + 5, 86, 40), texWhite);
                    GUI.color = old;
                }
                GUI.Label(new Rect(x, r.y + 5, 86, 40), text, style);
                x += 90;
            }
            GUI.Label(new Rect(r.x, r.y + 50, r.width, 20), "第 " + Gm.Round + " 輪", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        }

        void DrawActionBar()
        {
            float bw = 112, bh = 52, gap = 6;
            int n = 8;
            float total = n * bw + (n - 1) * gap + 20;
            var r = Panel(new Rect(W / 2 - total / 2, H - bh - 36, total, bh + 20));
            if (!Gm.IsPlayerTurn)
            {
                GUI.Label(r, Gm.Active != null ? Gm.Active.DisplayName + " 行動中……" : "……", sCenter);
                return;
            }
            var p = Gm.Player;
            float x = r.x + 10, y = r.y + 10;
            ActionButton(ref x, y, bw, bh, gap, PlayerAction.Attack, "1 攻擊", "1 AP");
            ActionButton(ref x, y, bw, bh, gap, PlayerAction.Heavy, "2 猛力劈砍", "2 AP");
            ActionButton(ref x, y, bw, bh, gap, PlayerAction.Shove, "3 推擊", "1 AP");
            string ammo = Gm.ThrowAmmo();
            int ammoCount = ammo == null ? 0 : Gm.ItemCount(ammo);
            ActionButton(ref x, y, bw, bh, gap, PlayerAction.Throw, "4 投擲", ammo == null ? "沒有可丟的" : ammo + "×" + ammoCount);
            ActionButton(ref x, y, bw, bh, gap, PlayerAction.Dodge, "5 閃避", "2 AP");
            if (p.Prone) ActionButton(ref x, y, bw, bh, gap, PlayerAction.StandUp, "6 起身", "2 AP");
            else { GUI.enabled = false; Button(new Rect(x, y, bw, bh), "6 起身\n<size=12>沒倒地</size>"); GUI.enabled = true; x += bw + gap; }
            ActionButton(ref x, y, bw, bh, gap, PlayerAction.Eat, "7 乾糧", "1 AP ×" + Gm.ItemCount("乾糧"));
            if (Button(new Rect(x, y, bw, bh), "結束回合\n<size=12>空白鍵</size>")) Gm.EndPlayerTurn();
        }

        void ActionButton(ref float x, float y, float bw, float bh, float gap, PlayerAction a, string label, string sub)
        {
            bool needsTarget = a == PlayerAction.Attack || a == PlayerAction.Heavy || a == PlayerAction.Shove || a == PlayerAction.Throw;
            bool affordable = Gm.Player.AP >= GameManager.ActionCost(a) && !Gm.Busy;
            if (a == PlayerAction.Throw && Gm.ThrowAmmo() == null) affordable = false;
            if (a == PlayerAction.Eat && Gm.ItemCount("乾糧") == 0) affordable = false;
            GUI.enabled = affordable;
            bool on = needsTarget && Gm.Selected == a;
            if (Button(new Rect(x, y, bw, bh), label + "\n<size=12>" + sub + "</size>", on))
            {
                if (needsTarget) Gm.Selected = a;
                else Gm.DoInstantAction(a);
            }
            GUI.enabled = true;
            x += bw + gap;
        }

        void DrawJournal()
        {
            var r = Panel(new Rect(W - 436, 120, 420, 60 + Mathf.Max(1, Gm.Clues.Count) * 44), texDark);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("<b>線索本</b>　<size=12>（Tab 關閉）</size>", sText);
            if (Gm.Clues.Count == 0) GUILayout.Label("還沒有發現什麼不對勁的地方。", sSmall);
            foreach (var c in Gm.Clues) GUILayout.Label("・" + c, sSmall);
            GUILayout.EndArea();
        }

        void DrawHover()
        {
            Vector2 m = InputCompat.MousePosition;
            Vector2 pos = new Vector2(m.x / scale + 18, (Screen.height - m.y) / scale + 10);

            if (Gm.HoverInteract != null)
            {
                Tooltip(pos, "<b>" + Gm.HoverInteract.Title + "</b>\n<size=13>點擊調查</size>");
                return;
            }
            var e = Gm.HoverEnemy;
            if (e == null) return;
            string text = "<b>" + e.DisplayName + "</b>\nHP " + e.HP + " / " + e.MaxHP + "　AC " + e.AC;
            var brain = e.GetComponent<EnemyBrain>();
            if (Gm.State == GameState.Explore && brain != null && !brain.Aware)
                text += "\n<size=13>他還沒發現你。點擊：繞過去偷襲</size>";

            if (Gm.State == GameState.Combat && Gm.IsPlayerTurn)
            {
                var p = Gm.Player;
                var a = Gm.Selected;
                float chance;
                string what;
                var reasons = new List<string>();
                if (a == PlayerAction.Shove)
                {
                    int dc = 8 + Mathf.Max(Dice.Mod(e.STR), Dice.Mod(e.DEX));
                    chance = Dice.HitChance(Dice.Mod(p.STR), dc, RollMode.Normal);
                    what = "推擊成功率";
                    var dir = new Vector2Int(Math.Sign(e.Cell.x - p.Cell.x), Math.Sign(e.Cell.y - p.Cell.y));
                    if (GridMap.Adjacent(p.Cell, e.Cell) && Gm.Map.IsPit(e.Cell + dir) && !Gm.Map.IsRailed(e.Cell))
                        reasons.Add("<color=#d4883a>身後是深坑：成功就會墜落！</color>");
                }
                else
                {
                    var kind = a == PlayerAction.Throw ? AttackKind.Throw : a == PlayerAction.Heavy ? AttackKind.Heavy : AttackKind.Melee;
                    var mode = Gm.AttackMode(p, e, kind, reasons);
                    chance = Dice.HitChance(Gm.AttackBonusFor(p, kind), e.AC, mode);
                    what = "命中率" + (mode != RollMode.Normal ? "（" + Dice.ModeLabel(mode) + "）" : "");
                }
                string hex = ColorUtility.ToHtmlStringRGB(Palette.ForHitChance(chance));
                text += "\n" + what + "：<color=#" + hex + "><b>" + Mathf.RoundToInt(chance * 100) + "%</b></color>";
                if (reasons.Count > 0) text += "\n<size=13>" + string.Join("、", reasons.ToArray()) + "</size>";
                if (a != PlayerAction.Throw && !GridMap.Adjacent(p.Cell, e.Cell))
                    text += "\n<size=13>會先走過去（剩 " + p.MoveLeft.ToString("0.#") + " m）</size>";
                if (a == PlayerAction.Throw)
                    text += "\n<size=13>距離 " + GridMap.Distance(p.Cell, e.Cell).ToString("0.#") + " / 8 m</size>";
            }
            Tooltip(pos, text);
        }

        void Tooltip(Vector2 pos, string text)
        {
            var content = new GUIContent(text);
            float w = 260;
            float h = sText.CalcHeight(content, w - 20) + 16;
            if (pos.x + w > W) pos.x = W - w - 8;
            if (pos.y + h > H) pos.y = H - h - 8;
            var r = new Rect(pos.x, pos.y, w, h);
            GUI.DrawTexture(r, texDark);
            GUI.Label(new Rect(r.x + 10, r.y + 8, w - 20, h - 16), content, sText);
        }

        void DrawFloats()
        {
            var cam = Gm.Cam != null ? Gm.Cam.Cam : null;
            if (cam == null) return;
            foreach (var f in floats)
            {
                Vector3 sp = cam.WorldToScreenPoint(f.World + Vector3.up * f.T * 0.6f);
                if (sp.z < 0) continue;
                float a = 1f - Mathf.Clamp01((f.T - 0.9f) / 0.5f);
                var style = new GUIStyle(sCenter) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(f.Color.r, f.Color.g, f.Color.b, a) } };
                var shadow = new GUIStyle(style) { normal = { textColor = new Color(0f, 0f, 0f, a * 0.8f) } };
                var r = new Rect(sp.x / scale - 100, (Screen.height - sp.y) / scale - 15, 200, 30);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), f.Text, shadow);
                GUI.Label(r, f.Text, style);
            }
        }

        void DrawRoll()
        {
            if (!rollVisible) return;
            var r = new Rect(W - 300, H / 2 - 110, 260, 200);
            GUI.DrawTexture(r, texDark);
            GUI.Label(new Rect(r.x, r.y + 10, r.width, 24), rollLabel + (rollResult.mode != RollMode.Normal ? "（" + Dice.ModeLabel(rollResult.mode) + "）" : ""), sCenter);
            int face = rollRolling ? rollFace : rollResult.natural;
            var big = new GUIStyle(sBig);
            if (!rollRolling) big.normal.textColor = rollResult.IsCrit ? Palette.HitGreen : rollResult.IsFumble ? Palette.HitRed : big.normal.textColor;
            GUI.Label(new Rect(r.x, r.y + 36, r.width, 70), face.ToString(), big);
            if (!rollRolling)
            {
                string dice = rollResult.mode != RollMode.Normal ? "（" + rollResult.first + "、" + rollResult.second + "）" : "";
                GUI.Label(new Rect(r.x, r.y + 106, r.width, 24), "d20 " + rollResult.natural + dice + " " + Dice.Signed(rollResult.bonus) + " = " + rollResult.Total + "　vs " + rollTargetLabel + " " + rollTarget, sCenter);
                string verdict = rollResult.IsCrit ? "大成功！" : rollResult.IsFumble ? "大失敗" : rollSuccess ? "成功" : "失敗";
                var vs = new GUIStyle(sCenter) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = rollSuccess ? Palette.HitGreen : Palette.HitRed } };
                GUI.Label(new Rect(r.x, r.y + 138, r.width, 40), verdict, vs);
            }
        }

        void DrawBannerAndFlash()
        {
            if (bannerTimer > 0f)
            {
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(bannerTimer / 0.4f));
                GUI.Label(new Rect(0, H * 0.22f, W, 50), bannerText, sBanner);
                GUI.color = old;
            }
            if (flashTimer > 0f)
            {
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flashTimer / 0.4f));
                GUI.Label(new Rect(0, H * 0.22f + 50, W, 30), flashText, new GUIStyle(sCenter) { normal = { textColor = Palette.SkylightCold } });
                GUI.color = old;
            }
        }

        void DrawModal()
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, W, H), texWhite);
            GUI.color = old;

            float w = Mathf.Min(680, W - 40);
            var options = BuildOptionList();
            float bodyH = sText.CalcHeight(new GUIContent(modalBody), w - 48);
            float btnH = 0;
            foreach (var o in options) btnH += (string.IsNullOrEmpty(o.Note) ? 44 : 64) + 8;
            float h = Mathf.Min(H - 40, 80 + bodyH + 24 + btnH + 16);
            var r = new Rect(W / 2 - w / 2, H / 2 - h / 2, w, h);
            GUI.DrawTexture(r, texDark);

            GUI.Label(new Rect(r.x + 24, r.y + 18, w - 48, 36), modalTitle, sTitle);
            var bodyRect = new Rect(r.x + 24, r.y + 62, w - 48, Mathf.Min(bodyH, h - 80 - btnH - 24));
            modalScroll = GUI.BeginScrollView(bodyRect, modalScroll, new Rect(0, 0, w - 64, bodyH));
            GUI.Label(new Rect(0, 0, w - 64, bodyH), modalBody, sText);
            GUI.EndScrollView();

            float y = bodyRect.yMax + 24;
            ModalButton clicked = null;
            foreach (var o in options)
            {
                float bh = string.IsNullOrEmpty(o.Note) ? 44 : 64;
                string label = o.Label + (string.IsNullOrEmpty(o.Note) ? "" : "\n<size=12>" + o.Note + "</size>");
                if (GUI.Button(new Rect(r.x + 24, y, w - 48, bh), label, sButton)) clicked = o;
                y += bh + 8;
            }
            if (clicked != null)
            {
                CloseModal();
                if (clicked.OnClick != null) clicked.OnClick();
            }
        }

        /// <summary>把調查選項轉成按鈕，順便算出成功率。</summary>
        List<ModalButton> BuildOptionList()
        {
            if (modalInteract == null) return modalButtons ?? new List<ModalButton>();
            var it = modalInteract;
            var list = new List<ModalButton>();
            if (it.Options != null)
            {
                foreach (var opt in it.Options())
                {
                    if (opt.Available != null && !opt.Available()) continue;
                    var o = opt;
                    string note = o.Hint;
                    if (o.Check != Ability.None)
                    {
                        int dc = Mathf.Max(2, o.DC + Gm.Diff.CheckDCOffset);
                        int mod = Dice.Mod(GameManager.AbilityScore(Gm.Player, o.Check));
                        float p = Dice.HitChance(mod, dc, o.Advantage ? RollMode.Advantage : RollMode.Normal);
                        string check = InteractOption.AbilityName(o.Check) + "檢定 DC " + dc + "　成功率 " + Mathf.RoundToInt(p * 100) + "%";
                        note = string.IsNullOrEmpty(note) ? check : check + "　" + note;
                    }
                    list.Add(new ModalButton(o.Label, () => Gm.ChooseOption(it, o), note));
                }
            }
            list.Add(new ModalButton("離開", null));
            return list;
        }
    }
}
