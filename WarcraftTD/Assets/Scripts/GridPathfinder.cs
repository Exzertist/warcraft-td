using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarcraftTD
{
    // The returned route may cross a destructible tower. Its extra traversal
    // cost lets an enemy compare breaking it with taking a detour.
    public static class GridPathfinder
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        public static List<Vector2Int> Find(
            int width, int height, Vector2Int start, Vector2Int goal,
            Func<Vector2Int, float> enterCost)
        {
            var result = new List<Vector2Int>();
            if (!Inside(start, width, height) || !Inside(goal, width, height))
                return result;

            var distance = new float[width, height];
            var previous = new Vector2Int[width, height];
            var visited = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    distance[x, y] = float.PositiveInfinity;
            distance[start.x, start.y] = 0f;

            for (int n = 0; n < width * height; n++)
            {
                var current = new Vector2Int(-1, -1);
                float best = float.PositiveInfinity;
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        if (!visited[x, y] && distance[x, y] < best)
                        {
                            best = distance[x, y];
                            current = new Vector2Int(x, y);
                        }

                if (current.x < 0) break;
                if (current == goal)
                {
                    for (var cell = goal; cell != start; cell = previous[cell.x, cell.y])
                        result.Add(cell);
                    result.Add(start);
                    result.Reverse();
                    return result;
                }

                visited[current.x, current.y] = true;
                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (!Inside(next, width, height) || visited[next.x, next.y]) continue;
                    float cost = enterCost(next);
                    if (cost < 0f || float.IsInfinity(cost)) continue;
                    float candidate = best + Mathf.Max(0.001f, cost);
                    if (candidate >= distance[next.x, next.y]) continue;
                    distance[next.x, next.y] = candidate;
                    previous[next.x, next.y] = current;
                }
            }
            return result;
        }

        private static bool Inside(Vector2Int cell, int width, int height) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
    }
}
