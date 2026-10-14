using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    public enum Cell { Void, Floor, Wall, Pit, Gate, Exit }

    /// <summary>
    /// 地圖格子：1 格 = 1 公尺。格子 (x, y) 對應世界座標 (x, 0, y)。
    /// 負責可走判斷、尋路（A*）和視線檢查。
    /// </summary>
    public class GridMap
    {
        public readonly int Width;
        public readonly int Height;
        readonly Cell[,] cells;
        readonly bool[,] blocked;      // 物件擋路（石柱、稻草堆、木桶…）
        readonly bool[,] blocksSight;  // 擋視線（牆、木桶）
        readonly bool[,] puddle;
        readonly bool[,] railed;       // 這格旁邊的深坑有完好的石欄（推不下去）
        public bool GateOpen;

        public readonly List<Character> Characters = new List<Character>();

        public GridMap(string[] rows)
        {
            Height = rows.Length;
            Width = rows[0].Length;
            cells = new Cell[Width, Height];
            blocked = new bool[Width, Height];
            blocksSight = new bool[Width, Height];
            puddle = new bool[Width, Height];
            railed = new bool[Width, Height];

            for (int r = 0; r < Height; r++)
            {
                string row = rows[r];
                if (row.Length != Width)
                    Debug.LogError("[無盡牢獄] 地圖第 " + r + " 行長度是 " + row.Length + "，應該是 " + Width);
                int y = Height - 1 - r; // 文字最上面一行是地圖最北邊
                for (int x = 0; x < Width; x++)
                {
                    char ch = x < row.Length ? row[x] : '#';
                    Cell c;
                    switch (ch)
                    {
                        case '.': c = Cell.Floor; break;
                        case ',': c = Cell.Floor; puddle[x, y] = true; break;
                        case 'O': c = Cell.Pit; break;
                        case 'G': c = Cell.Gate; break;
                        case 'X': c = Cell.Exit; break;
                        case '#': c = Cell.Wall; break;
                        default: c = Cell.Void; break;
                    }
                    cells[x, y] = c;
                    if (c == Cell.Wall || c == Cell.Void) blocksSight[x, y] = true;
                }
            }
        }

        public static Vector3 ToWorld(Vector2Int c)
        {
            return new Vector3(c.x, 0f, c.y);
        }

        public static Vector2Int ToCell(Vector3 w)
        {
            return new Vector2Int(Mathf.RoundToInt(w.x), Mathf.RoundToInt(w.z));
        }

        public bool InBounds(Vector2Int c)
        {
            return c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;
        }

        public Cell Get(Vector2Int c)
        {
            return InBounds(c) ? cells[c.x, c.y] : Cell.Void;
        }

        public bool IsPit(Vector2Int c) { return Get(c) == Cell.Pit; }
        public bool IsPuddle(Vector2Int c) { return InBounds(c) && puddle[c.x, c.y]; }
        public bool IsRailed(Vector2Int c) { return InBounds(c) && railed[c.x, c.y]; }

        public void SetBlocked(Vector2Int c, bool value, bool sight)
        {
            if (!InBounds(c)) return;
            blocked[c.x, c.y] = value;
            if (sight) blocksSight[c.x, c.y] = value;
        }

        public void SetRailed(Vector2Int c, bool value)
        {
            if (InBounds(c)) railed[c.x, c.y] = value;
        }

        /// <summary>地形本身能不能站人（不考慮角色）。</summary>
        public bool IsStandable(Vector2Int c)
        {
            if (!InBounds(c) || blocked[c.x, c.y]) return false;
            Cell t = cells[c.x, c.y];
            return t == Cell.Floor || t == Cell.Exit || (t == Cell.Gate && GateOpen);
        }

        public Character CharacterAt(Vector2Int c)
        {
            foreach (var ch in Characters)
                if (ch != null && ch.IsAlive && ch.Cell == c) return ch;
            return null;
        }

        public bool IsWalkable(Vector2Int c, Character ignore)
        {
            if (!IsStandable(c)) return false;
            var ch = CharacterAt(c);
            return ch == null || ch == ignore;
        }

        public bool BlocksSight(Vector2Int c)
        {
            if (!InBounds(c)) return true;
            return blocksSight[c.x, c.y];
        }

        public static float Distance(Vector2Int a, Vector2Int b)
        {
            return Vector2.Distance(a, b);
        }

        /// <summary>相鄰（含斜角）＝ 近戰距離 1.5m 以內。</summary>
        public static bool Adjacent(Vector2Int a, Vector2Int b)
        {
            return a != b && Mathf.Abs(a.x - b.x) <= 1 && Mathf.Abs(a.y - b.y) <= 1;
        }

        static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        public IEnumerable<Vector2Int> Neighbours(Vector2Int c)
        {
            foreach (var d in Dirs) yield return c + d;
        }

        /// <summary>
        /// A* 尋路。回傳不含起點的路徑；走不到回傳 null。
        /// 斜走不能切牆角。其他角色當成障礙物。
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, Character mover)
        {
            if (start == goal) return new List<Vector2Int>();
            if (!IsWalkable(goal, mover)) return null;

            var open = new List<Vector2Int> { start };
            var came = new Dictionary<Vector2Int, Vector2Int>();
            var g = new Dictionary<Vector2Int, float> { { start, 0f } };
            var closed = new HashSet<Vector2Int>();
            int guard = 0;

            while (open.Count > 0 && guard++ < 4000)
            {
                int best = 0;
                float bestF = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    float f = g[open[i]] + Distance(open[i], goal);
                    if (f < bestF) { bestF = f; best = i; }
                }
                var cur = open[best];
                open.RemoveAt(best);
                if (cur == goal)
                {
                    var path = new List<Vector2Int>();
                    while (cur != start)
                    {
                        path.Add(cur);
                        cur = came[cur];
                    }
                    path.Reverse();
                    return path;
                }
                closed.Add(cur);

                foreach (var d in Dirs)
                {
                    var n = cur + d;
                    if (closed.Contains(n) || !IsWalkable(n, mover)) continue;
                    if (d.x != 0 && d.y != 0)
                    {
                        // 不能從兩個障礙中間斜擠過去
                        if (!IsStandable(new Vector2Int(cur.x + d.x, cur.y)) || !IsStandable(new Vector2Int(cur.x, cur.y + d.y)))
                            continue;
                    }
                    float cost = g[cur] + ((d.x != 0 && d.y != 0) ? 1.4142f : 1f);
                    float old;
                    if (g.TryGetValue(n, out old) && old <= cost) continue;
                    g[n] = cost;
                    came[n] = cur;
                    if (!open.Contains(n)) open.Add(n);
                }
            }
            return null;
        }

        public static float PathLength(Vector2Int start, List<Vector2Int> path)
        {
            if (path == null) return float.MaxValue;
            float len = 0f;
            var prev = start;
            foreach (var p in path)
            {
                len += Distance(prev, p);
                prev = p;
            }
            return len;
        }

        /// <summary>兩格之間有沒有視線（牆和木桶會擋）。</summary>
        public bool HasLineOfSight(Vector2Int a, Vector2Int b)
        {
            Vector2 from = a;
            Vector2 to = b;
            float dist = Vector2.Distance(from, to);
            int steps = Mathf.CeilToInt(dist / 0.2f);
            for (int i = 1; i < steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
                var c = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                if (c == a || c == b) continue;
                if (BlocksSight(c)) return false;
            }
            return true;
        }

        /// <summary>找目標旁邊、走得到且最近的一格。</summary>
        public Vector2Int? BestCellNear(Vector2Int target, Character mover, out List<Vector2Int> bestPath)
        {
            bestPath = null;
            Vector2Int? best = null;
            float bestLen = float.MaxValue;
            if (Adjacent(mover.Cell, target))
            {
                bestPath = new List<Vector2Int>();
                return mover.Cell;
            }
            foreach (var n in Neighbours(target))
            {
                if (!IsWalkable(n, mover)) continue;
                var path = FindPath(mover.Cell, n, mover);
                if (path == null) continue;
                float len = PathLength(mover.Cell, path);
                if (len < bestLen)
                {
                    bestLen = len;
                    best = n;
                    bestPath = path;
                }
            }
            return best;
        }
    }
}
