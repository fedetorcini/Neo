using UnityEngine;

/// <summary>Shared crisp square sprite for runtime UI effects and the prototype course floor.</summary>
public static class DemoPixelArt
{
    private static Sprite square;
    private static Sprite circle;
    public static Sprite Square
    {
        get
        {
            if (square != null) return square;
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.name = "Neo demo pixel";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            square = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 16f);
            square.name = "Neo demo pixel";
            return square;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;
            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "Neo demo pixel circle";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = x + .5f - 16f;
                    float dy = y + .5f - 16f;
                    pixels[y * 32 + x] = dx * dx + dy * dy <= 16f * 16f ? Color.white : Color.clear;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            circle = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32f);
            circle.name = "Neo demo pixel circle";
            return circle;
        }
    }
}
