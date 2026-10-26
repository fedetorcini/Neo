/// <summary>Pixel density is independent of the one-unit gameplay grid.</summary>
public static class NeoVisualStandard
{
    public const int TilePixels = 32;
    public const int PixelsPerUnit = 32;
    public const int TallCharacterPixels = TilePixels * 3 / 2;
    public const int DoubleTilePixels = TilePixels * 2;
    public const int ReferenceWidth = 640;
    public const int ReferenceHeight = 360;
    public const float WorldPixel = 1f / PixelsPerUnit;
}
