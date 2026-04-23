using UnityEngine;

namespace IyanKim.UVMaskTool.Editor
{
    internal static class UVPaddingProcessor
    {
        public static Texture2D Apply(Texture2D tex, int padding)
        {
            return Apply(tex, padding, Color.clear, Color.white);
        }

        public static Texture2D Apply(Texture2D tex, int padding, Color backgroundColor, Color selectedColor)
        {
            if (tex == null)
            {
                return null;
            }

            if (padding == 0)
            {
                return tex;
            }

            padding = Mathf.Clamp(padding, -10, 10);
            var width = tex.width;
            var height = tex.height;
            var sourcePixels = tex.GetPixels32();
            var mask = BuildMask(sourcePixels, (Color32)selectedColor);
            var iterations = Mathf.Abs(padding);

            for (var i = 0; i < iterations; i++)
            {
                mask = padding > 0 ? Dilate(mask, width, height) : Erode(mask, width, height);
            }

            var output = new Color32[sourcePixels.Length];
            var bg = (Color32)backgroundColor;
            var fg = (Color32)selectedColor;
            for (var i = 0; i < output.Length; i++)
            {
                output[i] = mask[i] ? fg : bg;
            }

            tex.SetPixels32(output);
            tex.Apply(false, false);
            return tex;
        }

        private static bool[] BuildMask(Color32[] pixels, Color32 selectedColor)
        {
            var mask = new bool[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                mask[i] = ColorDistance(pixels[i], selectedColor) <= 3;
            }

            return mask;
        }

        private static bool[] Dilate(bool[] source, int width, int height)
        {
            var target = (bool[])source.Clone();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (source[index])
                    {
                        continue;
                    }

                    target[index] = HasNeighbor(source, width, height, x, y, true);
                }
            }

            return target;
        }

        private static bool[] Erode(bool[] source, int width, int height)
        {
            var target = (bool[])source.Clone();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (!source[index])
                    {
                        continue;
                    }

                    if (HasNeighbor(source, width, height, x, y, false))
                    {
                        target[index] = false;
                    }
                }
            }

            return target;
        }

        private static bool HasNeighbor(bool[] source, int width, int height, int x, int y, bool expectedValue)
        {
            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    var nx = x + offsetX;
                    var ny = y + offsetY;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    {
                        if (!expectedValue)
                        {
                            return true;
                        }

                        continue;
                    }

                    if (source[ny * width + nx] == expectedValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int ColorDistance(Color32 a, Color32 b)
        {
            return Mathf.Abs(a.r - b.r)
                + Mathf.Abs(a.g - b.g)
                + Mathf.Abs(a.b - b.b)
                + Mathf.Abs(a.a - b.a);
        }
    }
}
