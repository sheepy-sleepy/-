using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    /// <summary>
    /// 依照 GridMap 生成地板、牆、深坑、石欄和燈光。
    /// 物件（稻草堆、火盆…）由 FirstArea 另外擺。
    /// </summary>
    public static class LevelBuilder
    {
        public const float WallHeight = 2.6f;
        public const float PitDepth = 7f;

        public static Transform Build(GridMap map, HashSet<Vector2Int> brokenRail)
        {
            var root = new GameObject("Level").transform;
            var floorRoot = new GameObject("Floor").transform;
            floorRoot.SetParent(root, false);
            var wallRoot = new GameObject("Walls").transform;
            wallRoot.SetParent(root, false);

            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var c = new Vector2Int(x, y);
                    switch (map.Get(c))
                    {
                        case Cell.Floor:
                        case Cell.Gate:
                        case Cell.Exit:
                            BuildFloor(map, c, floorRoot);
                            break;
                        case Cell.Wall:
                            BuildWall(c, wallRoot);
                            break;
                    }
                }
            }

            BuildPit(map, root, brokenRail);
            BuildLighting(root);
            return root;
        }

        static void BuildFloor(GridMap map, Vector2Int c, Transform parent)
        {
            // 靠近深坑的地板做成很深的石柱，從側面看得到坑壁
            bool nearPit = false;
            foreach (var n in map.Neighbours(c))
                if (map.IsPit(n)) nearPit = true;
            float thickness = nearPit ? PitDepth : 0.3f;

            Color col = Palette.Jitter((c.x + c.y) % 2 == 0 ? Palette.Stone : Palette.StoneDark, 0.025f);
            if (map.IsPuddle(c)) col = Palette.Stone;
            var tile = MatLib.Prim(PrimitiveType.Cube, parent,
                new Vector3(c.x, -thickness / 2f + Random.Range(-0.015f, 0.015f), c.y),
                new Vector3(0.98f, thickness, 0.98f), col, false, "Floor " + c.x + "," + c.y);
            tile.isStatic = true;

            if (map.IsPuddle(c))
            {
                var water = MatLib.Prim(PrimitiveType.Cylinder, parent, new Vector3(c.x + Random.Range(-0.1f, 0.1f), 0.005f, c.y + Random.Range(-0.1f, 0.1f)),
                    new Vector3(Random.Range(0.75f, 0.95f), 0.01f, Random.Range(0.6f, 0.9f)), Palette.Water, true, "Puddle");
                water.GetComponent<Renderer>().sharedMaterial = MatLib.Solid(Palette.Water, 0.85f);
            }
            else if (Random.value < 0.18f)
            {
                // 零星碎石，讓地面不要太乾淨
                MatLib.Prim(PrimitiveType.Cube, parent, new Vector3(c.x + Random.Range(-0.35f, 0.35f), 0.03f, c.y + Random.Range(-0.35f, 0.35f)),
                    Vector3.one * Random.Range(0.05f, 0.12f), Palette.StoneDark, true, "Pebble").transform.rotation = Random.rotation;
            }
        }

        static void BuildWall(Vector2Int c, Transform parent)
        {
            float h = WallHeight + Random.Range(-0.15f, 0.15f);
            var wall = MatLib.Prim(PrimitiveType.Cube, parent, new Vector3(c.x, h / 2f - 0.3f, c.y), new Vector3(1f, h + 0.6f, 1f),
                Palette.Jitter(Palette.Wall, 0.03f), true, "Wall " + c.x + "," + c.y);
            wall.isStatic = true;
            // 石塊間的橫向灰縫
            MatLib.Prim(PrimitiveType.Cube, wall.transform, new Vector3(0f, Random.Range(-0.2f, 0.2f), 0f), new Vector3(1.01f, 0.03f, 1.01f), Palette.RawUmber, true, "Mortar");
        }

        static void BuildPit(GridMap map, Transform root, HashSet<Vector2Int> brokenRail)
        {
            var pitRoot = new GameObject("Abyss").transform;
            pitRoot.SetParent(root, false);

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if (map.IsPit(new Vector2Int(x, y)))
                    {
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
            if (minX == int.MaxValue) return;

            // 坑底：幾乎全黑的平面，加一點冷藍色的光，讓深淵看起來在呼吸
            var bottom = MatLib.Prim(PrimitiveType.Cube, pitRoot, new Vector3((minX + maxX) / 2f, -PitDepth, (minY + maxY) / 2f),
                new Vector3(maxX - minX + 1f, 0.1f, maxY - minY + 1f), Palette.Abyss, true, "AbyssFloor");
            bottom.isStatic = true;
            var glowGo = new GameObject("AbyssGlow");
            glowGo.transform.SetParent(pitRoot, false);
            glowGo.transform.position = new Vector3((minX + maxX) / 2f, -PitDepth + 1.5f, (minY + maxY) / 2f);
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = Palette.Hex("#3d5a73");
            glow.range = PitDepth + 2f;
            glow.intensity = 1.2f;
            var f = glowGo.AddComponent<Flicker>();
            f.Target = glow;
            f.BaseIntensity = 1.2f;
            f.Amount = 0.4f;

            // 石欄：在深坑邊緣的地板格上，靠坑的那一側。斷掉的地方可以把人推下去。
            var railRoot = new GameObject("Railing").transform;
            railRoot.SetParent(pitRoot, false);
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (!map.IsStandable(c) && map.Get(c) != Cell.Floor) continue;
                    if (map.Get(c) != Cell.Floor) continue;
                    bool edge = false;
                    foreach (var n in map.Neighbours(c))
                        if (map.IsPit(n)) edge = true;
                    if (!edge) continue;

                    bool broken = brokenRail.Contains(c);
                    map.SetRailed(c, !broken);

                    Vector2Int[] sides = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
                    foreach (var s in sides)
                    {
                        if (!map.IsPit(c + s)) continue;
                        Vector3 center = new Vector3(c.x + s.x * 0.45f, 0f, c.y + s.y * 0.45f);
                        bool alongX = s.y != 0;
                        if (broken)
                        {
                            // 斷掉的石欄：只剩一截殘樁和碎塊
                            MatLib.Prim(PrimitiveType.Cube, railRoot, center + new Vector3(0f, 0.15f, 0f), new Vector3(0.14f, 0.3f, 0.14f), Palette.StoneDark, true, "BrokenPost");
                            MatLib.Prim(PrimitiveType.Cube, railRoot, center + new Vector3(Random.Range(-0.3f, 0.3f), 0.05f, Random.Range(-0.2f, 0.2f)), new Vector3(0.25f, 0.1f, 0.15f), Palette.Stone, true, "Rubble")
                                .transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 180f), Random.Range(-15f, 15f));
                            continue;
                        }
                        MatLib.Prim(PrimitiveType.Cube, railRoot, center + new Vector3(0f, 0.35f, 0f), new Vector3(0.12f, 0.7f, 0.12f), Palette.Stone, true, "Post");
                        MatLib.Prim(PrimitiveType.Cube, railRoot, center + new Vector3(0f, 0.68f, 0f),
                            alongX ? new Vector3(1f, 0.1f, 0.12f) : new Vector3(0.12f, 0.1f, 1f), Palette.Stone, true, "Rail");
                    }
                }
            }
        }

        static void BuildLighting(Transform root)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Palette.Hex("#2a2629");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Palette.AsphaltBlue;
            RenderSettings.fogDensity = 0.03f;

            // 場景原本的燈先關掉，避免太亮
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.transform.parent == null) l.enabled = false;

            var sunGo = new GameObject("Skylight");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(62f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Palette.SkylightCold;
            sun.intensity = 0.35f;
            sun.shadows = LightShadows.Soft;
        }

        public static Light AddPointLight(Transform parent, Vector3 pos, Color color, float range, float intensity, bool flicker)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            if (flicker)
            {
                var f = go.AddComponent<Flicker>();
                f.Target = l;
                f.BaseIntensity = intensity;
            }
            return l;
        }

        /// <summary>天窗灑下來的冷光（聚光燈）。</summary>
        public static Light AddSkylightBeam(Transform parent, Vector3 floorPos)
        {
            var go = new GameObject("SkylightBeam");
            go.transform.SetParent(parent, false);
            go.transform.position = floorPos + new Vector3(0.6f, 6f, -0.4f);
            go.transform.LookAt(floorPos);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = Palette.SkylightCold;
            l.range = 10f;
            l.spotAngle = 38f;
            l.intensity = 3f;
            l.shadows = LightShadows.Soft;
            return l;
        }

        /// <summary>牆上的火把：木柄＋發光火焰＋閃爍的點光。</summary>
        public static void AddTorch(Transform parent, Vector3 wallFacePos, Vector3 outward)
        {
            var t = new GameObject("Torch").transform;
            t.SetParent(parent, false);
            t.position = wallFacePos;
            t.rotation = Quaternion.LookRotation(outward);
            MatLib.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, 0.12f), new Vector3(0.07f, 0.35f, 0.07f), Palette.Wood).transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            MatLib.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.2f, 0.22f), new Vector3(0.12f, 0.16f, 0.12f), Palette.CandleOrange)
                .GetComponent<Renderer>().sharedMaterial = MatLib.Glow(Palette.CandleOrange, 3f);
            AddPointLight(t, wallFacePos + outward * 0.6f + Vector3.up * 0.2f, Palette.CandleOrange, 7f, 1.6f, true);
        }
    }
}
