using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FrostUI.Media;

/// <summary>Shared, generated material textures used by the glass surfaces.</summary>
public static class FrostMaterials
{
    private const int NoiseSize = 128;
    private static ImageBrush? _noiseBrush;

    /// <summary>
    /// A seamless, deterministic grain texture. It is generated once and frozen;
    /// overlay it at 2–4% opacity for the characteristic frosted-glass tooth.
    /// </summary>
    public static ImageBrush NoiseBrush => _noiseBrush ??= CreateNoiseBrush();

    private static ImageBrush CreateNoiseBrush()
    {
        var stride = NoiseSize * 4;
        var pixels = new byte[stride * NoiseSize];
        var random = new Random(20261026);

        for (var i = 0; i < NoiseSize * NoiseSize; i++)
        {
            var offset = i * 4;
            var luminance = (byte)random.Next(190, 256);
            pixels[offset] = luminance;
            pixels[offset + 1] = luminance;
            pixels[offset + 2] = luminance;
            pixels[offset + 3] = (byte)random.Next(0, 26);
        }

        var bitmap = BitmapSource.Create(
            NoiseSize,
            NoiseSize,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, NoiseSize, NoiseSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }
}
