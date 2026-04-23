using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IyanKim.UVMaskTool.Editor
{
    internal static class UVPreviewRenderer
    {
        private static readonly Color Background = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color GridMajor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color GridMinor = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color IslandFill = new Color(0.55f, 0.58f, 0.62f, 0.28f);
        private static readonly Color HoverFill = new Color(0.72f, 0.86f, 1f, 0.55f);
        private static readonly Color SelectedFill = new Color(1f, 0.67f, 0.15f, 0.72f);
        private static readonly Color WireColor = new Color(0.88f, 0.88f, 0.88f, 0.58f);

        public readonly struct ViewTransform
        {
            private readonly Rect viewRect;
            private readonly Rect uvBounds;
            private readonly float scale;
            private readonly Vector2 pan;

            public ViewTransform(Rect viewRect, Rect uvBounds, float zoom, Vector2 pan)
            {
                this.viewRect = viewRect;
                this.uvBounds = uvBounds;
                this.pan = pan;

                var safeWidth = Mathf.Max(uvBounds.width, 0.0001f);
                var safeHeight = Mathf.Max(uvBounds.height, 0.0001f);
                var fitScale = Mathf.Min(viewRect.width / safeWidth, viewRect.height / safeHeight) * 0.88f;
                scale = Mathf.Max(1f, fitScale) * Mathf.Max(0.05f, zoom);
            }

            public Vector2 UvToScreen(Vector2 uv)
            {
                var uvCenter = uvBounds.center;
                var viewCenter = viewRect.center + pan;
                return new Vector2(
                    viewCenter.x + (uv.x - uvCenter.x) * scale,
                    viewCenter.y - (uv.y - uvCenter.y) * scale);
            }

            public Vector2 ScreenToUv(Vector2 point)
            {
                var uvCenter = uvBounds.center;
                var viewCenter = viewRect.center + pan;
                return new Vector2(
                    uvCenter.x + (point.x - viewCenter.x) / scale,
                    uvCenter.y - (point.y - viewCenter.y) / scale);
            }
        }

        public static ViewTransform CreateView(Rect localPreviewRect, List<Vector2> uvs, float zoom, Vector2 pan)
        {
            return new ViewTransform(localPreviewRect, CalculateUvBounds(uvs), zoom, pan);
        }

        public static ViewTransform CreateView(Rect localPreviewRect, List<Vector2> uvs, int[] meshTriangles, float zoom, Vector2 pan)
        {
            return new ViewTransform(localPreviewRect, CalculateUvBounds(uvs, meshTriangles), zoom, pan);
        }

        public static Rect CalculateUvBounds(List<Vector2> uvs)
        {
            if (uvs == null || uvs.Count == 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            var min = uvs[0];
            var max = uvs[0];
            for (var i = 1; i < uvs.Count; i++)
            {
                min = Vector2.Min(min, uvs[i]);
                max = Vector2.Max(max, uvs[i]);
            }

            if (Mathf.Abs(max.x - min.x) < 0.0001f)
            {
                min.x -= 0.5f;
                max.x += 0.5f;
            }

            if (Mathf.Abs(max.y - min.y) < 0.0001f)
            {
                min.y -= 0.5f;
                max.y += 0.5f;
            }

            var bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            var padding = Mathf.Max(bounds.width, bounds.height) * 0.08f;
            bounds.xMin -= padding;
            bounds.xMax += padding;
            bounds.yMin -= padding;
            bounds.yMax += padding;
            return bounds;
        }

        public static Rect CalculateUvBounds(List<Vector2> uvs, int[] meshTriangles)
        {
            if (uvs == null || meshTriangles == null || meshTriangles.Length == 0)
            {
                return CalculateUvBounds(uvs);
            }

            var hasBounds = false;
            var min = Vector2.zero;
            var max = Vector2.zero;
            for (var i = 0; i < meshTriangles.Length; i++)
            {
                var vertexIndex = meshTriangles[i];
                if (vertexIndex < 0 || vertexIndex >= uvs.Count)
                {
                    continue;
                }

                var uv = uvs[vertexIndex];
                if (!hasBounds)
                {
                    min = uv;
                    max = uv;
                    hasBounds = true;
                    continue;
                }

                min = Vector2.Min(min, uv);
                max = Vector2.Max(max, uv);
            }

            if (!hasBounds)
            {
                return CalculateUvBounds(uvs);
            }

            if (Mathf.Abs(max.x - min.x) < 0.0001f)
            {
                min.x -= 0.5f;
                max.x += 0.5f;
            }

            if (Mathf.Abs(max.y - min.y) < 0.0001f)
            {
                min.y -= 0.5f;
                max.y += 0.5f;
            }

            var bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            var padding = Mathf.Max(bounds.width, bounds.height) * 0.08f;
            bounds.xMin -= padding;
            bounds.xMax += padding;
            bounds.yMin -= padding;
            bounds.yMax += padding;
            return bounds;
        }

        public static void Draw(
            Rect previewRect,
            Mesh mesh,
            int[] meshTriangles,
            List<Vector2> uvs,
            List<UVIsland> islands,
            HashSet<int> selectedIslandIds,
            int hoverIslandId,
            ViewTransform transform)
        {
            EditorGUI.DrawRect(previewRect, Background);
            GUI.BeginClip(previewRect);
            Handles.BeginGUI();

            DrawGrid(new Rect(0f, 0f, previewRect.width, previewRect.height), transform);

            if (mesh != null && meshTriangles != null && uvs != null && islands != null)
            {
                DrawIslandFills(meshTriangles, uvs, islands, selectedIslandIds, hoverIslandId, transform);
                DrawWireframe(meshTriangles, uvs, transform);
            }

            Handles.EndGUI();
            GUI.EndClip();

            GUI.Box(previewRect, GUIContent.none, EditorStyles.helpBox);
        }

        private static void DrawGrid(Rect localRect, ViewTransform transform)
        {
            DrawUvLine(transform, new Vector2(0f, 0f), new Vector2(1f, 0f), GridMajor, 2f);
            DrawUvLine(transform, new Vector2(1f, 0f), new Vector2(1f, 1f), GridMajor, 2f);
            DrawUvLine(transform, new Vector2(1f, 1f), new Vector2(0f, 1f), GridMajor, 2f);
            DrawUvLine(transform, new Vector2(0f, 1f), new Vector2(0f, 0f), GridMajor, 2f);

            for (var i = -4; i <= 4; i++)
            {
                var value = i * 0.25f;
                DrawUvLine(transform, new Vector2(value, -4f), new Vector2(value, 4f), GridMinor, 1f);
                DrawUvLine(transform, new Vector2(-4f, value), new Vector2(4f, value), GridMinor, 1f);
            }

            EditorGUI.DrawRect(new Rect(0f, 0f, localRect.width, 1f), GridMajor);
            EditorGUI.DrawRect(new Rect(0f, localRect.height - 1f, localRect.width, 1f), GridMajor);
            EditorGUI.DrawRect(new Rect(0f, 0f, 1f, localRect.height), GridMajor);
            EditorGUI.DrawRect(new Rect(localRect.width - 1f, 0f, 1f, localRect.height), GridMajor);
        }

        private static void DrawIslandFills(
            int[] meshTriangles,
            List<Vector2> uvs,
            List<UVIsland> islands,
            HashSet<int> selectedIslandIds,
            int hoverIslandId,
            ViewTransform transform)
        {
            for (var i = 0; i < islands.Count; i++)
            {
                var island = islands[i];
                var color = IslandFill;
                if (selectedIslandIds != null && selectedIslandIds.Contains(island.id))
                {
                    color = SelectedFill;
                }
                else if (island.id == hoverIslandId)
                {
                    color = HoverFill;
                }

                Handles.color = color;
                for (var t = 0; t < island.triangleIndices.Count; t++)
                {
                    var triangleIndex = island.triangleIndices[t];
                    var baseIndex = triangleIndex * 3;
                    var a = transform.UvToScreen(uvs[meshTriangles[baseIndex]]);
                    var b = transform.UvToScreen(uvs[meshTriangles[baseIndex + 1]]);
                    var c = transform.UvToScreen(uvs[meshTriangles[baseIndex + 2]]);
                    Handles.DrawAAConvexPolygon(a, b, c);
                }
            }
        }

        private static void DrawWireframe(int[] meshTriangles, List<Vector2> uvs, ViewTransform transform)
        {
            Handles.color = WireColor;
            for (var i = 0; i < meshTriangles.Length; i += 3)
            {
                var a = transform.UvToScreen(uvs[meshTriangles[i]]);
                var b = transform.UvToScreen(uvs[meshTriangles[i + 1]]);
                var c = transform.UvToScreen(uvs[meshTriangles[i + 2]]);
                Handles.DrawAAPolyLine(1.4f, a, b, c, a);
            }
        }

        private static void DrawUvLine(ViewTransform transform, Vector2 fromUv, Vector2 toUv, Color color, float width)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(width, transform.UvToScreen(fromUv), transform.UvToScreen(toUv));
        }
    }
}
