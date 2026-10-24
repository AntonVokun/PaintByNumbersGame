using System.Collections.Generic;
using UnityEngine;

// Scan inside each existing collider path; a PNG's rectangle centre can be transparent.
public static class ZoneFocusTargets
{
    public struct Target
    {
        public Vector2 localPoint;
        public float size;
        public float area;
    }

    public static List<Target> Build(PolygonCollider2D collider)
    {
        var targets = new List<Target>();
        var intersections = new List<float>();
        for (int path = 0; path < collider.pathCount; path++)
        {
            Vector2[] points = collider.GetPath(path);
            if (points.Length < 3) continue;
            Vector2 min = points[0], max = points[0];
            foreach (Vector2 p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            float bestWidth = 0f;
            Vector2 best = Vector2.zero;
            // Midpoints between edge intersections are interior candidates, including concave islands.
            for (int row = 1; row <= 5; row++)
            {
                float y = Mathf.Lerp(min.y, max.y, row / 6f);
                intersections.Clear();
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    Vector2 a = points[j], b = points[i];
                    if ((a.y > y) == (b.y > y)) continue;
                    intersections.Add(a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y));
                }
                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    float width = intersections[i + 1] - intersections[i];
                    if (width <= bestWidth) continue;
                    Vector2 point = new Vector2((intersections[i] + intersections[i + 1]) * .5f, y) + collider.offset;
                    if (!collider.OverlapPoint(collider.transform.TransformPoint(point))) continue;
                    bestWidth = width;
                    best = point;
                }
            }
            if (bestWidth > 0)
                targets.Add(new Target { localPoint = best, size = Mathf.Max(max.x - min.x, max.y - min.y), area = (max.x-min.x)*(max.y-min.y) });
        }
        targets.Sort((a,b) => b.area.CompareTo(a.area));
        return targets;
    }
}
