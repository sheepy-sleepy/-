using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessPrison
{
    /// <summary>
    /// 地面上的輔助圖形：移動路徑、移動範圍圈、敵人視錐、目前行動角色的光圈、投擲物。
    /// </summary>
    public class Overlays : MonoBehaviour
    {
        LineRenderer pathLine;
        LineRenderer rangeRing;
        LineRenderer activeRing;
        readonly Dictionary<Character, GameObject> cones = new Dictionary<Character, GameObject>();

        static LineRenderer MakeLine(Transform parent, string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = MatLib.Overlay(Color.white);
            lr.startColor = color;
            lr.endColor = color;
            lr.widthMultiplier = width;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            lr.numCornerVertices = 2;
            return lr;
        }

        public static Overlays Create()
        {
            var o = new GameObject("Overlays").AddComponent<Overlays>();
            o.pathLine = MakeLine(o.transform, "Path", new Color(0.66f, 0.72f, 0.78f, 0.8f), 0.06f);
            o.rangeRing = MakeLine(o.transform, "MoveRange", new Color(0.66f, 0.72f, 0.78f, 0.35f), 0.04f);
            o.rangeRing.loop = true;
            o.activeRing = MakeLine(o.transform, "ActiveRing", new Color(0.83f, 0.53f, 0.23f, 0.9f), 0.05f);
            o.activeRing.loop = true;
            return o;
        }

        public void ShowPath(Vector2Int start, List<Vector2Int> path, bool reachable)
        {
            if (path == null || path.Count == 0)
            {
                pathLine.positionCount = 0;
                return;
            }
            Color c = reachable ? new Color(0.66f, 0.72f, 0.78f, 0.85f) : new Color(0.71f, 0.28f, 0.23f, 0.85f);
            pathLine.startColor = c;
            pathLine.endColor = c;
            pathLine.positionCount = path.Count + 1;
            pathLine.SetPosition(0, GridMap.ToWorld(start) + Vector3.up * 0.04f);
            for (int i = 0; i < path.Count; i++) pathLine.SetPosition(i + 1, GridMap.ToWorld(path[i]) + Vector3.up * 0.04f);
        }

        public void HidePath() { pathLine.positionCount = 0; }

        static void Circle(LineRenderer lr, Vector3 center, float radius)
        {
            const int n = 48;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.03f, Mathf.Sin(a) * radius));
            }
        }

        public void ShowMoveRange(Character c)
        {
            if (c == null || c.MoveLeft < 0.5f || c.Prone) { rangeRing.positionCount = 0; return; }
            Circle(rangeRing, c.transform.position, c.MoveLeft);
        }

        public void HideMoveRange() { rangeRing.positionCount = 0; }

        public void ShowActive(Character c)
        {
            if (c == null || !c.IsAlive) { activeRing.positionCount = 0; return; }
            Circle(activeRing, c.transform.position, 0.45f);
        }

        public void HideActive() { activeRing.positionCount = 0; }

        /// <summary>敵人的 130° 視錐，貼在地上。</summary>
        public void SetVisionCone(Character c, bool visible, float range, float halfAngle)
        {
            GameObject cone;
            if (!cones.TryGetValue(c, out cone))
            {
                cone = new GameObject("VisionCone");
                cone.transform.SetParent(c.transform, false);
                cone.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                var mf = cone.AddComponent<MeshFilter>();
                var mr = cone.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MatLib.Overlay(new Color(0.71f, 0.28f, 0.23f, 0.16f));
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mf.mesh = FanMesh(range, halfAngle);
                cones[c] = cone;
            }
            if (cone.activeSelf != visible) cone.SetActive(visible);
        }

        static Mesh FanMesh(float range, float halfAngle)
        {
            const int seg = 24;
            var verts = new Vector3[seg + 2];
            var tris = new int[seg * 3];
            var cols = new Color[seg + 2];
            verts[0] = Vector3.zero;
            cols[0] = new Color(1f, 1f, 1f, 1f);
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)seg) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Sin(a) * range, 0f, Mathf.Cos(a) * range);
                cols[i + 1] = new Color(1f, 1f, 1f, 0.15f);
            }
            for (int i = 0; i < seg; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            var m = new Mesh { vertices = verts, triangles = tris, colors = cols };
            m.RecalculateNormals();
            return m;
        }

        /// <summary>丟出去的東西（陶片、碎石、飛刃），飛到目標位置。</summary>
        public IEnumerator Projectile(Vector3 from, Vector3 to, Color color, float duration)
        {
            var go = MatLib.Prim(PrimitiveType.Cube, null, from, new Vector3(0.12f, 0.06f, 0.18f), color);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                Vector3 p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
                go.transform.position = p;
                go.transform.Rotate(720f * Time.deltaTime, 360f * Time.deltaTime, 0f);
                yield return null;
            }
            Destroy(go);
        }
    }
}
