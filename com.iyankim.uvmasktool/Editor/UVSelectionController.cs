using System.Collections.Generic;
using UnityEngine;

namespace IyanKim.UVMaskTool.Editor
{
    internal static class UVSelectionController
    {
        private const float BoundsPadding = 0.0001f;
        private const float BarycentricEpsilon = -0.00001f;

        public static int PickIsland(
            Vector2 localMousePosition,
            UVPreviewRenderer.ViewTransform transform,
            int[] meshTriangles,
            List<Vector2> uvs,
            List<UVIsland> islands)
        {
            if (meshTriangles == null || uvs == null || islands == null || islands.Count == 0)
            {
                return -1;
            }

            var uvPoint = transform.ScreenToUv(localMousePosition);
            var pickedId = -1;
            var pickedArea = float.MaxValue;

            for (var i = 0; i < islands.Count; i++)
            {
                var island = islands[i];
                if (!ContainsWithPadding(island.uvBounds, uvPoint))
                {
                    continue;
                }

                for (var t = 0; t < island.triangleIndices.Count; t++)
                {
                    var triangleIndex = island.triangleIndices[t];
                    var baseIndex = triangleIndex * 3;
                    var a = uvs[meshTriangles[baseIndex]];
                    var b = uvs[meshTriangles[baseIndex + 1]];
                    var c = uvs[meshTriangles[baseIndex + 2]];

                    if (!PointInTriangle(uvPoint, a, b, c))
                    {
                        continue;
                    }

                    var area = Mathf.Max(island.uvBounds.width * island.uvBounds.height, 0.0000001f);
                    if (area <= pickedArea)
                    {
                        pickedId = island.id;
                        pickedArea = area;
                    }
                    break;
                }
            }

            return pickedId;
        }

        public static void ApplyClick(HashSet<int> selectedIslandIds, int islandId, bool shift, bool control)
        {
            if (selectedIslandIds == null || islandId < 0)
            {
                return;
            }

            if (control)
            {
                selectedIslandIds.Remove(islandId);
                return;
            }

            if (shift)
            {
                selectedIslandIds.Add(islandId);
                return;
            }

            if (selectedIslandIds.Contains(islandId))
            {
                selectedIslandIds.Remove(islandId);
            }
            else
            {
                selectedIslandIds.Clear();
                selectedIslandIds.Add(islandId);
            }
        }

        private static bool ContainsWithPadding(Rect bounds, Vector2 point)
        {
            return point.x >= bounds.xMin - BoundsPadding
                && point.x <= bounds.xMax + BoundsPadding
                && point.y >= bounds.yMin - BoundsPadding
                && point.y <= bounds.yMax + BoundsPadding;
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Approximately(denominator, 0f))
            {
                return false;
            }

            var u = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denominator;
            var v = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denominator;
            var w = 1f - u - v;
            return u >= BarycentricEpsilon && v >= BarycentricEpsilon && w >= BarycentricEpsilon;
        }
    }
}
