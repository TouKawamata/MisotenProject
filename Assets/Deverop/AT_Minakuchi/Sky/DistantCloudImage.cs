using UnityEngine;

namespace Minakuchi.Sky
{
    // Bake a tileable cloud silhouette once; the shader applies the surrounding cloud palette.
    internal static class DistantCloudImage
    {
        private const int Resolution = 1024;
        private const int Period = 8;

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263);
                value = (value ^ (value >> 13)) * 1274126177u;
                return (value ^ (value >> 16)) / (float)uint.MaxValue;
            }
        }

        private static float Noise(float x, float y, int period)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int ax = ix % period, ay = iy % period;
            int bx = (ax + 1) % period, by = (ay + 1) % period;
            return Mathf.Lerp(Mathf.Lerp(Hash(ax, ay), Hash(bx, ay), fx),
                Mathf.Lerp(Hash(ax, by), Hash(bx, by), fx), fy);
        }

        private static float Shape(float x, float y)
        {
            float value = 0, weight = 0.55f;
            int period = Period;
            for (int octave = 0; octave < 5; octave++)
            {
                value += Noise(x, y, period) * weight;
                x *= 2; y *= 2; period *= 2; weight *= 0.5f;
            }
            return value;
        }

        public static Texture2D Create()
        {
            var pixels = new Color32[Resolution * Resolution];
            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float px = (x + 0.5f) * Period / Resolution;
                float py = (y + 0.5f) * Period / Resolution;
                float shape = Shape(px, py);
                pixels[y * Resolution + x] = new Color(shape, 1, 1, 1);
            }
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, true, true)
            {
                name = "遠景の静止雲イラスト", wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear, anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
