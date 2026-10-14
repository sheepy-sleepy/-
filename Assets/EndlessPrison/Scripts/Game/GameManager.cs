using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndlessPrison
{
    public enum GameState { Title, Explore, Combat, Ended }
    public enum PlayerAction { Attack, Heavy, Shove, Throw, Dodge, StandUp, Eat }
    public enum AttackKind { Melee, Heavy, Throw }

    /// <summary>
    /// 遊戲主控：建場景、處理滑鼠點擊、探索、檢定、回合制戰鬥。
    /// 把這個元件掛在場景裡任何一個空物件上，按 Play 就會自動生成第一區。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public GridMap Map;
        public Character Player;
        public readonly List<Character> Enemies = new List<Character>();
        public Hud Hud;
        public CameraRig Cam;
        public Overlays Fx;
        public Difficulty Diff = Difficulty.Easy();

        public readonly Dictionary<string, int> Inventory = new Dictionary<string, int>();
        public readonly List<string> Clues = new List<string>();
        public GameState State;
        public bool Busy;
        public bool ReadEscapeLetter;
        public Action OnGateOpen;

        // 戰鬥
        public readonly List<Character> TurnOrder = new List<Character>();
        public Character Active;
        public bool IsPlayerTurn;
        public PlayerAction Selected = PlayerAction.Attack;
        public int Round;
        bool endTurnRequested;
        bool playerSurpriseAttack;
        bool pendingCombat;
        bool reachedExit;
        readonly HashSet<Character> surprised = new HashSet<Character>();

        public Character HoverEnemy;
        public Interactable HoverInteract;

        public const float ExploreSpeed = 2.4f;
        public const float CombatSpeed = 3f;
        public const float ThrowRange = 8f;

        void Awake()
        {
            I = this;
        }

        void Start()
        {
            Setup();
        }

        void Setup()
        {
            Hud = GetComponent<Hud>();
            if (Hud == null) Hud = gameObject.AddComponent<Hud>();
            Hud.Gm = this;
            Fx = Overlays.Create();
            FirstArea.Build(this);
            Cam = CameraRig.Create(Player.transform);
            State = GameState.Title;
            FirstArea.ShowTitle(this);
        }

        /// <summary>選好難度後開始。</summary>
        public void Begin(Difficulty diff)
        {
            Diff = diff;
            foreach (var e in Enemies) FirstArea.ApplyDifficulty(e, diff);
            FirstArea.ShowIntro(this, () =>
            {
                State = GameState.Explore;
                Hud.Log("點地板移動，點發亮的物件調查。Q／E 旋轉鏡頭，滾輪縮放。");
            });
        }

        public void Restart()
        {
            StopAllCoroutines();
            StartCoroutine(RestartRoutine());
        }

        IEnumerator RestartRoutine()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go == gameObject || (Cam != null && go == Cam.gameObject)) continue;
                Destroy(go);
            }
            Inventory.Clear();
            Clues.Clear();
            Enemies.Clear();
            TurnOrder.Clear();
            surprised.Clear();
            Busy = false;
            IsPlayerTurn = false;
            pendingCombat = false;
            reachedExit = false;
            playerSurpriseAttack = false;
            ReadEscapeLetter = false;
            OnGateOpen = null;
            Active = null;
            Round = 0;
            Hud.ResetAll();
            yield return null;
            Setup();
        }

        // ───────────────────────── 物品、線索 ─────────────────────────

        public void AddItem(string item, int count = 1)
        {
            int n;
            Inventory.TryGetValue(item, out n);
            Inventory[item] = n + count;
            Hud.Log("獲得「" + item + "」" + (count > 1 ? " ×" + count : "") + "。");
        }

        public int ItemCount(string item)
        {
            int n;
            return Inventory.TryGetValue(item, out n) ? n : 0;
        }

        public bool TakeItem(string item)
        {
            int n = ItemCount(item);
            if (n <= 0) return false;
            if (n == 1) Inventory.Remove(item);
            else Inventory[item] = n - 1;
            return true;
        }

        public void AddClue(string clue)
        {
            if (Clues.Contains(clue)) return;
            Clues.Add(clue);
            Hud.Log("【線索】" + clue);
            Hud.Flash("記下了一條線索");
        }

        public void OpenGate()
        {
            Map.GateOpen = true;
            if (OnGateOpen != null) OnGateOpen();
        }

        // ───────────────────────── 每幀 ─────────────────────────

        void Update()
        {
            if (Map == null || Player == null) return;

            if (State == GameState.Explore && !Hud.HasModal) UpdatePerception();
            if (State == GameState.Explore || State == GameState.Combat) HandleInput();

            // 地面輔助圖形
            bool showCombatFx = State == GameState.Combat && IsPlayerTurn && !Busy;
            if (showCombatFx) Fx.ShowMoveRange(Player); else Fx.HideMoveRange();
            if (State == GameState.Combat && Active != null) Fx.ShowActive(Active); else Fx.HideActive();
            foreach (var e in Enemies)
            {
                var brain = e.GetComponent<EnemyBrain>();
                Fx.SetVisionCone(e, State == GameState.Explore && e.IsAlive && brain != null && !brain.Aware,
                    Diff.EnemyVisionRange, 65f);
            }
        }

        void HandleInput()
        {
            HoverEnemy = null;
            HoverInteract = null;
            if (Hud.HasModal || Busy)
            {
                Fx.HidePath();
                return;
            }

            if (State == GameState.Combat && IsPlayerTurn)
            {
                if (InputCompat.KeyDown(KeyCode.Space)) { EndPlayerTurn(); return; }
                if (InputCompat.KeyDown(KeyCode.Alpha1)) Selected = PlayerAction.Attack;
                if (InputCompat.KeyDown(KeyCode.Alpha2)) Selected = PlayerAction.Heavy;
                if (InputCompat.KeyDown(KeyCode.Alpha3)) Selected = PlayerAction.Shove;
                if (InputCompat.KeyDown(KeyCode.Alpha4)) Selected = PlayerAction.Throw;
                if (InputCompat.KeyDown(KeyCode.Alpha5)) DoInstantAction(PlayerAction.Dodge);
                if (InputCompat.KeyDown(KeyCode.Alpha6)) DoInstantAction(PlayerAction.StandUp);
                if (InputCompat.KeyDown(KeyCode.Alpha7)) DoInstantAction(PlayerAction.Eat);
                if (InputCompat.KeyDown(KeyCode.Escape) || InputCompat.RightClickDown) Selected = PlayerAction.Attack;
            }

            Vector2 mouse = InputCompat.MousePosition;
            if (Hud.IsPointerOverUI(mouse) || Cam == null || Cam.Cam == null)
            {
                Fx.HidePath();
                return;
            }

            Ray ray = Cam.Cam.ScreenPointToRay(mouse);
            RaycastHit hit;
            Vector2Int? hoverCell = null;
            if (Physics.Raycast(ray, out hit, 200f))
            {
                var ch = hit.collider.GetComponentInParent<Character>();
                var it = hit.collider.GetComponentInParent<Interactable>();
                if (ch != null && ch.Team == Team.Enemy && ch.IsAlive) HoverEnemy = ch;
                else if (it != null && !it.Done) HoverInteract = it;
                else hoverCell = GridMap.ToCell(hit.point);
            }

            // 戰鬥中預覽移動路徑
            if (State == GameState.Combat && IsPlayerTurn && hoverCell.HasValue && !Player.Prone)
            {
                var path = Map.FindPath(Player.Cell, hoverCell.Value, Player);
                Fx.ShowPath(Player.Cell, path, path != null && GridMap.PathLength(Player.Cell, path) <= Player.MoveLeft + 0.01f);
            }
            else Fx.HidePath();

            if (!InputCompat.LeftClickDown) return;
            if (HoverEnemy != null) OnEnemyClicked(HoverEnemy);
            else if (HoverInteract != null) OnInteractClicked(HoverInteract);
            else if (hoverCell.HasValue) OnCellClicked(hoverCell.Value);
        }

        IEnumerator Run(IEnumerator routine)
        {
            Busy = true;
            yield return StartCoroutine(routine);
            Busy = false;
            AfterAction();
        }

        void StartRun(IEnumerator routine)
        {
            StartCoroutine(Run(routine));
        }

        void AfterAction()
        {
            if (reachedExit && State == GameState.Explore)
            {
                reachedExit = false;
                State = GameState.Ended;
                FirstArea.ShowVictory(this);
                return;
            }
            if (pendingCombat && State == GameState.Explore)
            {
                pendingCombat = false;
                StartCombat(false);
                return;
            }
            // 戰鬥中 AP 和移動都用完就自動結束回合
            if (State == GameState.Combat && IsPlayerTurn && Player.AP == 0 && (Player.MoveLeft < 1f || Player.Prone))
                EndPlayerTurn();
        }

        // ───────────────────────── 移動與感知 ─────────────────────────

        void OnCellClicked(Vector2Int cell)
        {
            if (State == GameState.Combat)
            {
                if (!IsPlayerTurn) return;
                if (Player.Prone) { Hud.Log("你倒在地上，要先起身（2 AP）才能移動。"); return; }
            }
            var path = Map.FindPath(Player.Cell, cell, Player);
            if (path == null || path.Count == 0)
            {
                if (path == null) Hud.Log("走不到那裡。");
                return;
            }
            if (State == GameState.Combat)
            {
                float len = GridMap.PathLength(Player.Cell, path);
                if (len > Player.MoveLeft + 0.01f)
                {
                    Hud.Log("太遠了。這回合還能移動 " + Player.MoveLeft.ToString("0.#") + " 公尺。");
                    return;
                }
                Player.MoveLeft -= len;
            }
            StartRun(MovePlayer(path));
        }

        IEnumerator MovePlayer(List<Vector2Int> path)
        {
            float speed = State == GameState.Combat ? CombatSpeed : ExploreSpeed;
            yield return StartCoroutine(Player.MoveAlong(path, speed, OnPlayerStep, () => pendingCombat || reachedExit));
        }

        void OnPlayerStep(Vector2Int cell)
        {
            bool puddle = Map.IsPuddle(cell);
            if (State == GameState.Explore)
            {
                foreach (var e in Enemies)
                {
                    var brain = e.GetComponent<EnemyBrain>();
                    if (brain == null || brain.Aware || !e.IsAlive) continue;
                    float radius = Diff.EnemyHearingRange * (puddle ? 1.5f : 1f);
                    if (GridMap.Distance(cell, e.Cell) <= radius) brain.HearNoise(cell);
                }
                if (puddle) Hud.Float(Player.transform.position + Vector3.up * 0.4f, "啪嚓", Palette.SkylightCold);
                if (Map.Get(cell) == Cell.Exit) reachedExit = true;
            }
        }

        void UpdatePerception()
        {
            foreach (var e in Enemies)
            {
                var brain = e.GetComponent<EnemyBrain>();
                if (brain == null || brain.Aware || !e.IsAlive) continue;
                if (brain.CanSee(Player, Diff.EnemyVisionRange))
                {
                    brain.Aware = true;
                    Hud.Float(e.transform.position + Vector3.up * 2.1f, "！", Palette.HitRed);
                    Hud.Log(e.DisplayName + "發現你了！");
                    if (Busy) pendingCombat = true;
                    else StartCombat(false);
                    return;
                }
            }
        }

        // ───────────────────────── 調查 ─────────────────────────

        void OnInteractClicked(Interactable it)
        {
            if (State == GameState.Combat)
            {
                Hud.Log("戰鬥中沒空做這個。");
                return;
            }
            StartRun(InteractRoutine(it));
        }

        IEnumerator InteractRoutine(Interactable it)
        {
            List<Vector2Int> path;
            var spot = Map.BestCellNear(it.Cell, Player, out path);
            if (spot == null)
            {
                Hud.Log("走不到「" + it.Title + "」旁邊。");
                yield break;
            }
            if (path.Count > 0) yield return StartCoroutine(MovePlayer(path));
            if (pendingCombat || reachedExit) yield break;
            Player.FaceTowards(it.Cell);
            yield return new WaitForSeconds(0.15f);
            it.MarkLooked();
            Hud.ShowInteract(it);
        }

        /// <summary>從調查視窗選了一個做法。</summary>
        public void ChooseOption(Interactable it, InteractOption opt)
        {
            StartRun(OptionRoutine(it, opt));
        }

        IEnumerator OptionRoutine(Interactable it, InteractOption opt)
        {
            Player.FaceTowards(it.Cell);
            yield return StartCoroutine(Player.Rig.Play(opt.Anim, null));

            bool success = true;
            if (opt.Check != Ability.None)
            {
                int dc = Mathf.Max(2, opt.DC + Diff.CheckDCOffset);
                int mod = Dice.Mod(AbilityScore(Player, opt.Check));
                var roll = Dice.D20(mod, opt.Advantage ? RollMode.Advantage : RollMode.Normal);
                success = !roll.IsFumble && (roll.IsCrit || roll.Total >= dc);
                yield return StartCoroutine(Hud.ShowRoll(InteractOption.AbilityName(opt.Check) + "檢定", roll, dc, "DC", success));
                Hud.Log(InteractOption.AbilityName(opt.Check) + "檢定：" + RollText(roll) + " vs DC " + dc + " → " + (success ? "成功" : "失敗"));
            }
            if (opt.OnResult != null) opt.OnResult(success);
        }

        public static int AbilityScore(Character c, Ability a)
        {
            switch (a)
            {
                case Ability.STR: return c.STR;
                case Ability.DEX: return c.DEX;
                case Ability.INT: return c.INT;
                case Ability.WIS: return c.WIS;
                default: return 10;
            }
        }

        public static string RollText(D20Result r)
        {
            string dice = r.mode == RollMode.Normal ? "d20(" + r.natural + ")" : "d20(" + r.first + "," + r.second + "→" + r.natural + ")";
            return dice + Dice.Signed(r.bonus) + "=" + r.Total;
        }

        // ───────────────────────── 戰鬥：開始與回合 ─────────────────────────

        public bool AnyEnemyAlive()
        {
            foreach (var e in Enemies) if (e.IsAlive) return true;
            return false;
        }

        public void StartCombat(bool playerSurprise)
        {
            if (State == GameState.Combat) return;
            State = GameState.Combat;
            Round = 1;
            TurnOrder.Clear();
            surprised.Clear();
            Fx.HidePath();

            var rolls = new Dictionary<Character, int>();
            var all = new List<Character> { Player };
            foreach (var e in Enemies) if (e.IsAlive) all.Add(e);
            foreach (var c in all)
            {
                int bonus = Dice.Mod(c.DEX) + (c == Player && ReadEscapeLetter ? 2 : 0);
                int r = Dice.Roll(20) + bonus;
                rolls[c] = r;
                TurnOrder.Add(c);
                c.Rig.CombatStance = true;
                var brain = c.GetComponent<EnemyBrain>();
                if (brain != null) brain.Aware = true;
            }
            TurnOrder.Sort((a, b) => rolls[b].CompareTo(rolls[a]));

            if (playerSurprise)
            {
                TurnOrder.Remove(Player);
                TurnOrder.Insert(0, Player);
                foreach (var e in Enemies) if (e.IsAlive) surprised.Add(e);
                playerSurpriseAttack = true;
                Hud.Log("你從他身後接近，他還沒發現你！第一擊有優勢，他這回合來不及反應。");
            }

            var sb = new System.Text.StringBuilder("先攻：");
            foreach (var c in TurnOrder) sb.Append(c.DisplayName).Append("(").Append(rolls[c]).Append(") ");
            Hud.Log(sb.ToString());
            if (ReadEscapeLetter) Hud.Log("遺書讓你有了準備（先攻 +2）。");
            Hud.Banner("戰鬥開始");
            StartCoroutine(CombatLoop());
        }

        IEnumerator CombatLoop()
        {
            yield return new WaitForSeconds(0.8f);
            while (Player.IsAlive && AnyEnemyAlive())
            {
                foreach (var c in TurnOrder.ToArray())
                {
                    if (!Player.IsAlive || !AnyEnemyAlive()) break;
                    if (!c.IsAlive) continue;
                    Active = c;
                    c.StartTurn();

                    if (surprised.Contains(c))
                    {
                        surprised.Remove(c);
                        Hud.Log(c.DisplayName + "措手不及，這回合無法行動。");
                        yield return new WaitForSeconds(0.9f);
                        continue;
                    }

                    if (c == Player)
                    {
                        Cam.SnapTo(Player.transform);
                        Hud.Banner("你的回合");
                        if (Player.Prone) Hud.Log("你倒在地上。起身需要 2 AP。");
                        endTurnRequested = false;
                        IsPlayerTurn = true;
                        while (!endTurnRequested && Player.IsAlive && AnyEnemyAlive()) yield return null;
                        while (Busy) yield return null;
                        IsPlayerTurn = false;
                        Selected = PlayerAction.Attack;
                    }
                    else
                    {
                        Cam.SnapTo(c.transform);
                        Hud.Banner(c.DisplayName + "的回合");
                        Busy = true;
                        var brain = c.GetComponent<EnemyBrain>();
                        if (brain != null) yield return StartCoroutine(brain.TakeTurn(this));
                        Busy = false;
                        Cam.SnapTo(Player.transform);
                    }
                }
                Round++;
            }
            Active = null;
            yield return new WaitForSeconds(0.6f);
            EndCombat();
        }

        void EndCombat()
        {
            Fx.HidePath();
            Fx.HideMoveRange();
            Fx.HideActive();
            if (!Player.IsAlive)
            {
                State = GameState.Ended;
                FirstArea.ShowGameOver(this);
                return;
            }
            State = GameState.Explore;
            Player.Rig.CombatStance = false;
            Player.SetProne(false);
            Hud.Banner("戰鬥結束");
            Hud.Log("四周安靜下來，只剩深淵裡的風聲。");
        }

        public void EndPlayerTurn()
        {
            if (!IsPlayerTurn || Busy) return;
            endTurnRequested = true;
        }

        // ───────────────────────── 玩家行動 ─────────────────────────

        public static int ActionCost(PlayerAction a)
        {
            switch (a)
            {
                case PlayerAction.Heavy: return 2;
                case PlayerAction.Dodge: return 2;
                case PlayerAction.StandUp: return 2;
                default: return 1;
            }
        }

        public string ThrowAmmo()
        {
            if (ItemCount("陶片") > 0) return "陶片";
            if (ItemCount("碎石") > 0) return "碎石";
            return null;
        }

        void OnEnemyClicked(Character enemy)
        {
            if (State == GameState.Explore)
            {
                var brain = enemy.GetComponent<EnemyBrain>();
                if (brain != null && !brain.Aware) StartRun(SneakUpRoutine(enemy));
                else StartCombat(false);
                return;
            }
            if (State != GameState.Combat || !IsPlayerTurn) return;
            TryPlayerAction(Selected, enemy);
        }

        IEnumerator SneakUpRoutine(Character enemy)
        {
            List<Vector2Int> path;
            var spot = Map.BestCellNear(enemy.Cell, Player, out path);
            if (spot == null)
            {
                Hud.Log("走不到他旁邊。");
                yield break;
            }
            if (path.Count > 0) yield return StartCoroutine(MovePlayer(path));
            var brain = enemy.GetComponent<EnemyBrain>();
            if (pendingCombat || (brain != null && brain.Aware)) yield break;
            Player.FaceTowards(enemy.Cell);
            StartCombat(true);
        }

        public void TryPlayerAction(PlayerAction action, Character target)
        {
            if (!IsPlayerTurn || Busy) return;
            int cost = ActionCost(action);
            if (Player.AP < cost)
            {
                Hud.Log("AP 不夠（需要 " + cost + "，剩 " + Player.AP + "）。");
                return;
            }

            if (action == PlayerAction.Throw)
            {
                string ammo = ThrowAmmo();
                if (ammo == null) { Hud.Log("沒有可以丟的東西。牢房裡的陶碗或環道的火盆旁也許有。"); return; }
                if (GridMap.Distance(Player.Cell, target.Cell) > ThrowRange) { Hud.Log("太遠了，丟不到（射程 8 公尺）。"); return; }
                if (!Map.HasLineOfSight(Player.Cell, target.Cell)) { Hud.Log("視線被擋住了。"); return; }
                StartRun(PlayerThrow(target, ammo));
                return;
            }

            if (action != PlayerAction.Attack && action != PlayerAction.Heavy && action != PlayerAction.Shove) return;

            if (!GridMap.Adjacent(Player.Cell, target.Cell))
            {
                if (Player.Prone) { Hud.Log("你倒在地上，先起身才能靠近。"); return; }
                List<Vector2Int> path;
                var spot = Map.BestCellNear(target.Cell, Player, out path);
                float len = GridMap.PathLength(Player.Cell, path);
                if (spot == null || len > Player.MoveLeft + 0.01f)
                {
                    Hud.Log("搆不到。這回合還能移動 " + Player.MoveLeft.ToString("0.#") + " 公尺。");
                    return;
                }
                Player.MoveLeft -= len;
                StartRun(ApproachThen(path, action, target));
                return;
            }
            StartRun(PlayerMelee(action, target));
        }

        IEnumerator ApproachThen(List<Vector2Int> path, PlayerAction action, Character target)
        {
            yield return StartCoroutine(MovePlayer(path));
            yield return StartCoroutine(PlayerMelee(action, target));
        }

        IEnumerator PlayerMelee(PlayerAction action, Character target)
        {
            Player.AP -= ActionCost(action);
            if (action == PlayerAction.Shove) yield return StartCoroutine(ShoveRoutine(Player, target));
            else yield return StartCoroutine(AttackRoutine(Player, target, action == PlayerAction.Heavy ? AttackKind.Heavy : AttackKind.Melee));
        }

        IEnumerator PlayerThrow(Character target, string ammo)
        {
            Player.AP -= 1;
            TakeItem(ammo);
            yield return StartCoroutine(AttackRoutine(Player, target, AttackKind.Throw));
        }

        /// <summary>不用選目標的行動：閃避、起身、吃乾糧。</summary>
        public void DoInstantAction(PlayerAction action)
        {
            if (!IsPlayerTurn || Busy) return;
            int cost = ActionCost(action);
            if (Player.AP < cost) { Hud.Log("AP 不夠（需要 " + cost + "，剩 " + Player.AP + "）。"); return; }
            switch (action)
            {
                case PlayerAction.Dodge:
                    if (Player.Dodging) { Hud.Log("你已經在專注閃避了。"); return; }
                    StartRun(DodgeRoutine(Player));
                    break;
                case PlayerAction.StandUp:
                    if (!Player.Prone) { Hud.Log("你沒有倒地。"); return; }
                    StartRun(StandUpRoutine(Player));
                    break;
                case PlayerAction.Eat:
                    if (ItemCount("乾糧") == 0) { Hud.Log("沒有乾糧。"); return; }
                    StartRun(EatRoutine());
                    break;
            }
        }

        public IEnumerator DodgeRoutine(Character c)
        {
            c.AP -= 2;
            c.Dodging = true;
            Hud.Log(c.DisplayName + "專注閃避：攻擊他的人都有劣勢，直到他的下個回合。");
            yield return StartCoroutine(c.Rig.Play(ActionAnim.Dodge, null));
        }

        public IEnumerator StandUpRoutine(Character c)
        {
            c.AP -= 2;
            c.SetProne(false);
            Hud.Log(c.DisplayName + "爬了起來（-2 AP）。");
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator EatRoutine()
        {
            Player.AP -= 1;
            TakeItem("乾糧");
            int heal = Dice.Roll(4) + 2;
            Player.Heal(heal);
            Hud.Float(Player.transform.position + Vector3.up * 2f, "+" + heal, Palette.HitGreen);
            Hud.Log("你啃了幾口乾硬的乾糧，回復 " + heal + " HP。");
            yield return StartCoroutine(Player.Rig.Play(ActionAnim.Interact, null));
        }

        /// <summary>在戰鬥外吃乾糧（從物品欄按的）。</summary>
        public void EatOutsideCombat()
        {
            if (State != GameState.Explore || Busy || ItemCount("乾糧") == 0) return;
            if (Player.HP >= Player.MaxHP) { Hud.Log("你現在不餓。"); return; }
            TakeItem("乾糧");
            int heal = Dice.Roll(4) + 2;
            Player.Heal(heal);
            Hud.Float(Player.transform.position + Vector3.up * 2f, "+" + heal, Palette.HitGreen);
            Hud.Log("你啃了幾口乾硬的乾糧，回復 " + heal + " HP。");
        }

        // ───────────────────────── 攻擊、推擊（玩家和敵人共用） ─────────────────────────

        public RollMode AttackMode(Character attacker, Character target, AttackKind kind, List<string> reasons)
        {
            bool adv = false, dis = false;
            if (target.Prone && kind != AttackKind.Throw) { adv = true; if (reasons != null) reasons.Add("目標倒地"); }
            if (!target.InFrontCone(attacker.Cell)) { adv = true; if (reasons != null) reasons.Add("從背後"); }
            if (attacker == Player && playerSurpriseAttack) { adv = true; if (reasons != null) reasons.Add("偷襲"); }
            if (target.Dodging) { dis = true; if (reasons != null) reasons.Add("目標閃避中"); }
            if (attacker.Prone) { dis = true; if (reasons != null) reasons.Add("自己倒地"); }
            if (kind == AttackKind.Throw)
            {
                if (target.Prone) { dis = true; if (reasons != null) reasons.Add("目標倒地（遠程）"); }
                foreach (var c in Map.Characters)
                    if (c.IsAlive && c.Team != attacker.Team && GridMap.Adjacent(c.Cell, attacker.Cell))
                    {
                        dis = true;
                        if (reasons != null) reasons.Add("被近身（遠程）");
                        break;
                    }
            }
            return Dice.Combine(adv, dis);
        }

        public int AttackBonusFor(Character attacker, AttackKind kind)
        {
            return kind == AttackKind.Throw ? attacker.ThrowBonus : attacker.AttackBonus;
        }

        public IEnumerator AttackRoutine(Character attacker, Character target, AttackKind kind)
        {
            attacker.FaceTowards(target.Cell);
            yield return new WaitForSeconds(0.15f);

            var reasons = new List<string>();
            RollMode mode = AttackMode(attacker, target, kind, reasons);
            int bonus = AttackBonusFor(attacker, kind);
            if (attacker == Player) playerSurpriseAttack = false;

            bool impact = false;
            ActionAnim anim = kind == AttackKind.Heavy ? ActionAnim.HeavyAttack : kind == AttackKind.Throw ? ActionAnim.Throw : ActionAnim.Attack;
            StartCoroutine(attacker.Rig.Play(anim, () => impact = true));
            while (!impact) yield return null;

            if (kind == AttackKind.Throw)
            {
                Vector3 from = attacker.Rig.RightHand.position;
                Vector3 to = target.transform.position + Vector3.up * 1.2f;
                Color color = attacker.Team == Team.Enemy ? Palette.SkylightCold : Palette.Bone;
                yield return StartCoroutine(Fx.Projectile(from, to, color, 0.28f));
            }

            var roll = Dice.D20(bonus, mode);
            bool hit = !roll.IsFumble && (roll.IsCrit || roll.Total >= target.AC);
            StartCoroutine(Hud.ShowRoll(attacker == Player ? "攻擊" : attacker.DisplayName + "攻擊", roll, target.AC, "AC", hit));

            string weaponName = kind == AttackKind.Throw ? (attacker.Team == Team.Enemy ? "飛刃" : "投擲") : attacker.Weapon.Name;
            string line = attacker.DisplayName + "用" + weaponName + (kind == AttackKind.Heavy ? "猛力劈砍" : "攻擊") + "：" + RollText(roll) + " vs AC " + target.AC;
            if (reasons.Count > 0) line += "（" + Dice.ModeLabel(mode) + "：" + string.Join("、", reasons.ToArray()) + "）";

            if (hit)
            {
                string extra;
                int dmg = RollDamage(attacker, target, kind, roll.IsCrit, mode, out extra);
                line += " → " + (roll.IsCrit ? "暴擊！" : "命中，") + "造成 " + dmg + " 點傷害" + extra;
                Hud.Float(target.transform.position + Vector3.up * 2f, (roll.IsCrit ? "暴擊 " : "") + "-" + dmg, Palette.HitRed);
                bool died = target.TakeDamage(dmg);
                Hud.Log(line);
                if (died) yield return StartCoroutine(KillRoutine(target, attacker));
                else StartCoroutine(target.Rig.Play(ActionAnim.Hit, null));
            }
            else
            {
                line += " → 未命中";
                Hud.Float(target.transform.position + Vector3.up * 2f, "未命中", Palette.SkylightCold);
                Hud.Log(line);
                if (!target.Prone) StartCoroutine(target.Rig.Play(ActionAnim.Dodge, null));
            }

            while (attacker.Rig.IsBusy) yield return null;
            yield return new WaitForSeconds(0.25f);
        }

        int RollDamage(Character attacker, Character target, AttackKind kind, bool crit, RollMode mode, out string extra)
        {
            extra = "";
            int dmg;
            int mult = crit ? 2 : 1;
            if (kind == AttackKind.Throw)
            {
                dmg = Dice.Roll(mult, 4) + Dice.Mod(attacker.DEX);
            }
            else
            {
                var w = attacker.Weapon;
                dmg = (w.DiceCount > 0 ? Dice.Roll(w.DiceCount * mult, w.DiceSides) : w.FlatBonus * mult) + attacker.WeaponMod;
                if (kind == AttackKind.Heavy)
                {
                    int bonus = Dice.Roll(mult, 6);
                    dmg += bonus;
                    extra += "（劈砍 +" + bonus + "）";
                }
            }

            // 偷襲：限靈巧或遠程武器，要有優勢，每回合一次
            bool finesseOrRanged = kind == AttackKind.Throw || attacker.Weapon.Finesse;
            if (attacker.Team == Team.Enemy && Diff.EnemySneakAttack && finesseOrRanged && mode == RollMode.Advantage && !attacker.UsedSneakThisTurn)
            {
                attacker.UsedSneakThisTurn = true;
                int s = Dice.Roll(mult, 6);
                dmg += s;
                extra += "（偷襲 +" + s + "）";
            }
            return Mathf.Max(1, dmg);
        }

        IEnumerator KillRoutine(Character target, Character killer)
        {
            target.HP = 0;
            target.Rig.SetDead();
            var col = target.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            if (target == Player)
            {
                Hud.Log("你倒下了。懷錶從手中滑落。");
            }
            else
            {
                Hud.Log(target.DisplayName + "倒下了。");
                FirstArea.OnEnemyKilled(this, target);
            }
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>推擊：DC = 8 + 目標 max(STR, DEX) 調整值；推的人擲 d20 + STR 調整值。</summary>
        public IEnumerator ShoveRoutine(Character attacker, Character target)
        {
            attacker.FaceTowards(target.Cell);
            yield return new WaitForSeconds(0.15f);
            bool impact = false;
            StartCoroutine(attacker.Rig.Play(ActionAnim.Shove, () => impact = true));
            while (!impact) yield return null;

            int dc = 8 + Mathf.Max(Dice.Mod(target.STR), Dice.Mod(target.DEX));
            var roll = Dice.D20(Dice.Mod(attacker.STR), RollMode.Normal);
            bool success = !roll.IsFumble && (roll.IsCrit || roll.Total >= dc);
            StartCoroutine(Hud.ShowRoll(attacker == Player ? "推擊" : attacker.DisplayName + "推擊", roll, dc, "DC", success));
            Hud.Log(attacker.DisplayName + "推擊" + target.DisplayName + "：" + RollText(roll) + " vs DC " + dc + " → " + (success ? "成功" : "失敗"));

            if (!success)
            {
                Hud.Float(target.transform.position + Vector3.up * 2f, "站穩了", Palette.SkylightCold);
                while (attacker.Rig.IsBusy) yield return null;
                yield break;
            }

            var dir = new Vector2Int(Math.Sign(target.Cell.x - attacker.Cell.x), Math.Sign(target.Cell.y - attacker.Cell.y));
            var dest = target.Cell + dir;

            if (Map.IsPit(dest) && !Map.IsRailed(target.Cell))
            {
                yield return StartCoroutine(FallRoutine(target, dest));
            }
            else if (Map.IsWalkable(dest, target))
            {
                // 被推退一格
                StartCoroutine(target.Rig.Play(ActionAnim.Hit, null));
                Vector3 from = target.transform.position;
                Vector3 to = GridMap.ToWorld(dest);
                target.Cell = dest;
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / 0.22f;
                    target.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t));
                    yield return null;
                }
                Hud.Log(target.DisplayName + "被推退一步。");
            }
            else
            {
                // 撞到牆、石欄或其他東西：倒地並受傷
                int dmg = Dice.Roll(4);
                bool rail = Map.IsPit(dest);
                Hud.Log(target.DisplayName + (rail ? "撞上石欄" : "撞上障礙") + "，摔倒在地（-" + dmg + " HP）。");
                Hud.Float(target.transform.position + Vector3.up * 2f, "-" + dmg, Palette.HitRed);
                target.SetProne(true);
                if (target.TakeDamage(dmg)) yield return StartCoroutine(KillRoutine(target, attacker));
            }
            while (attacker.Rig.IsBusy) yield return null;
            yield return new WaitForSeconds(0.2f);
        }

        IEnumerator FallRoutine(Character target, Vector2Int pitCell)
        {
            if (target == Player && !Diff.PlayerFallIsDeath)
            {
                int dmg = Dice.Roll(6);
                Hud.Log("你往後跌進深坑——最後一刻抓住了崖邊，狼狽地爬回來（-" + dmg + " HP，倒地）。");
                Hud.Float(target.transform.position + Vector3.up * 2f, "抓住崖邊！ -" + dmg, Palette.HitOrange);
                StartCoroutine(target.Rig.Play(ActionAnim.Hit, null));
                target.SetProne(true);
                if (target.TakeDamage(dmg)) yield return StartCoroutine(KillRoutine(target, null));
                yield break;
            }

            target.HP = 0;
            target.enabled = false;
            var col = target.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Hud.Float(target.transform.position + Vector3.up * 2f, "墜落！", Palette.HitRed);
            yield return StartCoroutine(target.Rig.FallInto(GridMap.ToWorld(pitCell)));
            if (target == Player)
            {
                Hud.Log("你墜入深淵。黑暗裡，好像有什麼東西在等你。");
            }
            else
            {
                Hud.Log(target.DisplayName + "墜入深淵。過了好一陣子，底下才傳來悶響。（地形殺）");
                FirstArea.OnEnemyKilled(this, target);
            }
        }
    }
}
