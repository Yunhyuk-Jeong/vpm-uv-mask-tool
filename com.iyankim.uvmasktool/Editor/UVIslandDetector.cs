using System;
using System.Collections.Generic;
using UnityEngine;

namespace IyanKim.UVMaskTool.Editor
{
    internal sealed class UVIsland
    {
        public int id;
        public readonly List<int> triangleIndices = new List<int>();
        public Rect uvBounds;
    }

    internal static class UVIslandDetector
    {
        private const float AreaEpsilon = 0.00000001f;
        private const float UvQuantizeScale = 100000f;

        public static List<int> FindAvailableUvChannels(Mesh mesh)
        {
            return FindAvailableUvChannels(mesh, mesh != null ? mesh.triangles : null);
        }

        public static List<int> FindAvailableUvChannels(Mesh mesh, int[] triangles)
        {
            var channels = new List<int>();
            if (mesh == null || triangles == null || triangles.Length == 0)
            {
                return channels;
            }

            for (var channel = 0; channel <= 7; channel++)
            {
                if (TryReadUVs(mesh, channel, out var uvs) && HasUsableTriangles(triangles, uvs))
                {
                    channels.Add(channel);
                }
            }

            return channels;
        }

        public static int FindBestUvChannel(Mesh mesh, List<int> availableChannels)
        {
            return FindBestUvChannel(mesh, mesh != null ? mesh.triangles : null, availableChannels);
        }

        public static int FindBestUvChannel(Mesh mesh, int[] triangles, List<int> availableChannels)
        {
            if (mesh == null || triangles == null || triangles.Length == 0 || availableChannels == null || availableChannels.Count == 0)
            {
                return -1;
            }

            var bestChannel = availableChannels[0];
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < availableChannels.Count; i++)
            {
                var channel = availableChannels[i];
                if (!TryReadUVs(mesh, channel, out var uvs))
                {
                    continue;
                }

                var score = CalculateUvScore(triangles, uvs);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestChannel = channel;
                }
            }

            return bestChannel;
        }

        public static List<UVIsland> GenerateIslands(Mesh mesh, int uvChannel)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            var uvs = ReadUVs(mesh, uvChannel);
            return GenerateIslands(mesh, uvs);
        }

        public static List<UVIsland> GenerateIslands(Mesh mesh, List<Vector2> uvs)
        {
            return GenerateIslands(mesh, uvs, mesh != null ? mesh.triangles : null);
        }

        public static List<UVIsland> GenerateIslands(Mesh mesh, List<Vector2> uvs, int[] triangles)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            if (uvs == null || uvs.Count != mesh.vertexCount)
            {
                throw new InvalidOperationException("The selected UV channel does not contain UV data for this mesh.");
            }

            if (triangles == null || triangles.Length == 0)
            {
                return new List<UVIsland>();
            }

            var vertices = mesh.vertices;
            var triangleCount = triangles.Length / 3;
            var adjacency = new List<int>[triangleCount];
            var edgeMap = new Dictionary<SurfaceUvEdgeKey, List<int>>(triangleCount * 3);
            var validTriangle = new bool[triangleCount];

            for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                adjacency[triangleIndex] = new List<int>(3);

                var baseIndex = triangleIndex * 3;
                var a = triangles[baseIndex];
                var b = triangles[baseIndex + 1];
                var c = triangles[baseIndex + 2];

                if (!IsValidVertexIndex(a, uvs.Count) || !IsValidVertexIndex(b, uvs.Count) || !IsValidVertexIndex(c, uvs.Count)
                    || !IsValidVertexIndex(a, vertices.Length) || !IsValidVertexIndex(b, vertices.Length) || !IsValidVertexIndex(c, vertices.Length))
                {
                    continue;
                }

                var uvA = uvs[a];
                var uvB = uvs[b];
                var uvC = uvs[c];
                if (Mathf.Abs(SignedArea(uvA, uvB, uvC)) <= AreaEpsilon)
                {
                    continue;
                }

                validTriangle[triangleIndex] = true;
                RegisterEdge(edgeMap, new SurfaceUvEdgeKey(vertices[a], uvA, vertices[b], uvB), triangleIndex);
                RegisterEdge(edgeMap, new SurfaceUvEdgeKey(vertices[b], uvB, vertices[c], uvC), triangleIndex);
                RegisterEdge(edgeMap, new SurfaceUvEdgeKey(vertices[c], uvC, vertices[a], uvA), triangleIndex);
            }

            foreach (var connectedTriangles in edgeMap.Values)
            {
                if (connectedTriangles.Count < 2)
                {
                    continue;
                }

                for (var i = 0; i < connectedTriangles.Count; i++)
                {
                    var from = connectedTriangles[i];
                    for (var j = i + 1; j < connectedTriangles.Count; j++)
                    {
                        var to = connectedTriangles[j];
                        adjacency[from].Add(to);
                        adjacency[to].Add(from);
                    }
                }
            }

            return BuildIslands(triangles, uvs, adjacency, validTriangle);
        }

        public static List<Vector2> ReadUVs(Mesh mesh, int uvChannel)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            if (uvChannel < 0 || uvChannel > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(uvChannel), "UV channel must be between 0 and 7.");
            }

            if (!TryReadUVs(mesh, uvChannel, out var uvs))
            {
                throw new InvalidOperationException($"UV{uvChannel} is empty or incomplete.");
            }

            return uvs;
        }

        private static bool TryReadUVs(Mesh mesh, int uvChannel, out List<Vector2> uvs)
        {
            uvs = new List<Vector2>(mesh != null ? mesh.vertexCount : 0);
            if (mesh == null || uvChannel < 0 || uvChannel > 7)
            {
                return false;
            }

            var uvArray = GetUvArray(mesh, uvChannel);
            if (uvArray != null && uvArray.Length == mesh.vertexCount)
            {
                uvs.AddRange(uvArray);
                return true;
            }

            mesh.GetUVs(uvChannel, uvs);
            if (uvs.Count == mesh.vertexCount)
            {
                return true;
            }

            var uv4s = new List<Vector4>(mesh.vertexCount);
            mesh.GetUVs(uvChannel, uv4s);
            if (uv4s.Count != mesh.vertexCount)
            {
                uvs.Clear();
                return false;
            }

            uvs.Clear();
            for (var i = 0; i < uv4s.Count; i++)
            {
                uvs.Add(new Vector2(uv4s[i].x, uv4s[i].y));
            }

            return true;
        }

        private static Vector2[] GetUvArray(Mesh mesh, int uvChannel)
        {
            switch (uvChannel)
            {
                case 0: return mesh.uv;
                case 1: return mesh.uv2;
                case 2: return mesh.uv3;
                case 3: return mesh.uv4;
                case 4: return mesh.uv5;
                case 5: return mesh.uv6;
                case 6: return mesh.uv7;
                case 7: return mesh.uv8;
                default: return null;
            }
        }

        private static bool HasUsableTriangles(int[] triangles, List<Vector2> uvs)
        {
            if (triangles == null || uvs == null || triangles.Length < 3)
            {
                return false;
            }

            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                if (!IsValidVertexIndex(a, uvs.Count) || !IsValidVertexIndex(b, uvs.Count) || !IsValidVertexIndex(c, uvs.Count))
                {
                    continue;
                }

                if (Mathf.Abs(SignedArea(uvs[a], uvs[b], uvs[c])) > AreaEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static float CalculateUvScore(int[] triangles, List<Vector2> uvs)
        {
            if (triangles == null || uvs == null || triangles.Length < 3)
            {
                return 0f;
            }

            var validTriangles = 0;
            var hasBounds = false;
            var min = Vector2.zero;
            var max = Vector2.zero;

            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                if (!IsValidVertexIndex(a, uvs.Count) || !IsValidVertexIndex(b, uvs.Count) || !IsValidVertexIndex(c, uvs.Count))
                {
                    continue;
                }

                var uvA = uvs[a];
                var uvB = uvs[b];
                var uvC = uvs[c];
                if (Mathf.Abs(SignedArea(uvA, uvB, uvC)) <= AreaEpsilon)
                {
                    continue;
                }

                validTriangles++;
                ExpandBounds(ref hasBounds, ref min, ref max, uvA);
                ExpandBounds(ref hasBounds, ref min, ref max, uvB);
                ExpandBounds(ref hasBounds, ref min, ref max, uvC);
            }

            if (!hasBounds)
            {
                return 0f;
            }

            var boundsArea = Mathf.Max((max.x - min.x) * (max.y - min.y), 0f);
            return validTriangles + boundsArea * 1000f;
        }

        private static void ExpandBounds(ref bool hasBounds, ref Vector2 min, ref Vector2 max, Vector2 uv)
        {
            if (!hasBounds)
            {
                min = uv;
                max = uv;
                hasBounds = true;
                return;
            }

            min = Vector2.Min(min, uv);
            max = Vector2.Max(max, uv);
        }

        private static List<UVIsland> BuildIslands(
            int[] meshTriangles,
            List<Vector2> uvs,
            List<int>[] adjacency,
            bool[] validTriangle)
        {
            var islands = new List<UVIsland>();
            var visited = new bool[adjacency.Length];
            var queue = new Queue<int>();

            for (var start = 0; start < adjacency.Length; start++)
            {
                if (visited[start] || !validTriangle[start])
                {
                    continue;
                }

                var island = new UVIsland { id = islands.Count };
                var hasBounds = false;
                var min = Vector2.zero;
                var max = Vector2.zero;

                visited[start] = true;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    var triangleIndex = queue.Dequeue();
                    island.triangleIndices.Add(triangleIndex);

                    ExpandBounds(meshTriangles, uvs, triangleIndex, ref hasBounds, ref min, ref max);

                    var neighbors = adjacency[triangleIndex];
                    for (var i = 0; i < neighbors.Count; i++)
                    {
                        var neighbor = neighbors[i];
                        if (visited[neighbor] || !validTriangle[neighbor])
                        {
                            continue;
                        }

                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }

                island.uvBounds = hasBounds
                    ? Rect.MinMaxRect(min.x, min.y, max.x, max.y)
                    : new Rect(0f, 0f, 0f, 0f);
                islands.Add(island);
            }

            return islands;
        }

        private static void ExpandBounds(
            int[] meshTriangles,
            List<Vector2> uvs,
            int triangleIndex,
            ref bool hasBounds,
            ref Vector2 min,
            ref Vector2 max)
        {
            var baseIndex = triangleIndex * 3;
            for (var i = 0; i < 3; i++)
            {
                var uv = uvs[meshTriangles[baseIndex + i]];
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
        }

        private static void RegisterEdge(Dictionary<SurfaceUvEdgeKey, List<int>> edgeMap, SurfaceUvEdgeKey edge, int triangleIndex)
        {
            if (!edgeMap.TryGetValue(edge, out var triangles))
            {
                triangles = new List<int>(2);
                edgeMap.Add(edge, triangles);
            }

            triangles.Add(triangleIndex);
        }

        private static bool IsValidVertexIndex(int index, int vertexCount)
        {
            return index >= 0 && index < vertexCount;
        }

        private static float SignedArea(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private readonly struct QuantizedPosition : IEquatable<QuantizedPosition>, IComparable<QuantizedPosition>
        {
            private readonly int x;
            private readonly int y;
            private readonly int z;

            public QuantizedPosition(Vector3 position)
            {
                x = Mathf.RoundToInt(position.x * UvQuantizeScale);
                y = Mathf.RoundToInt(position.y * UvQuantizeScale);
                z = Mathf.RoundToInt(position.z * UvQuantizeScale);
            }

            public bool Equals(QuantizedPosition other)
            {
                return x == other.x && y == other.y && z == other.z;
            }

            public override bool Equals(object obj)
            {
                return obj is QuantizedPosition other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = x;
                    hash = (hash * 397) ^ y;
                    hash = (hash * 397) ^ z;
                    return hash;
                }
            }

            public int CompareTo(QuantizedPosition other)
            {
                var xComparison = x.CompareTo(other.x);
                if (xComparison != 0)
                {
                    return xComparison;
                }

                var yComparison = y.CompareTo(other.y);
                return yComparison != 0 ? yComparison : z.CompareTo(other.z);
            }
        }

        private readonly struct QuantizedUv : IEquatable<QuantizedUv>, IComparable<QuantizedUv>
        {
            private readonly int x;
            private readonly int y;

            public QuantizedUv(Vector2 uv)
            {
                x = Mathf.RoundToInt(uv.x * UvQuantizeScale);
                y = Mathf.RoundToInt(uv.y * UvQuantizeScale);
            }

            public bool Equals(QuantizedUv other)
            {
                return x == other.x && y == other.y;
            }

            public override bool Equals(object obj)
            {
                return obj is QuantizedUv other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (x * 397) ^ y;
                }
            }

            public int CompareTo(QuantizedUv other)
            {
                var xComparison = x.CompareTo(other.x);
                return xComparison != 0 ? xComparison : y.CompareTo(other.y);
            }
        }

        private readonly struct SurfaceUvPointKey : IEquatable<SurfaceUvPointKey>, IComparable<SurfaceUvPointKey>
        {
            private readonly QuantizedPosition position;
            private readonly QuantizedUv uv;

            public SurfaceUvPointKey(Vector3 position, Vector2 uv)
            {
                this.position = new QuantizedPosition(position);
                this.uv = new QuantizedUv(uv);
            }

            public bool Equals(SurfaceUvPointKey other)
            {
                return position.Equals(other.position) && uv.Equals(other.uv);
            }

            public override bool Equals(object obj)
            {
                return obj is SurfaceUvPointKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (position.GetHashCode() * 397) ^ uv.GetHashCode();
                }
            }

            public int CompareTo(SurfaceUvPointKey other)
            {
                var positionComparison = position.CompareTo(other.position);
                return positionComparison != 0 ? positionComparison : uv.CompareTo(other.uv);
            }
        }

        private readonly struct SurfaceUvEdgeKey : IEquatable<SurfaceUvEdgeKey>
        {
            private readonly SurfaceUvPointKey a;
            private readonly SurfaceUvPointKey b;

            public SurfaceUvEdgeKey(Vector3 firstPosition, Vector2 firstUv, Vector3 secondPosition, Vector2 secondUv)
            {
                var first = new SurfaceUvPointKey(firstPosition, firstUv);
                var second = new SurfaceUvPointKey(secondPosition, secondUv);
                if (first.CompareTo(second) <= 0)
                {
                    a = first;
                    b = second;
                }
                else
                {
                    a = second;
                    b = first;
                }
            }

            public bool Equals(SurfaceUvEdgeKey other)
            {
                return a.Equals(other.a) && b.Equals(other.b);
            }

            public override bool Equals(object obj)
            {
                return obj is SurfaceUvEdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (a.GetHashCode() * 397) ^ b.GetHashCode();
                }
            }
        }
    }
}
