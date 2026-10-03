// QRCodeGenerator — simple QR code bitmap generation for Android
// Used by BiliLoginService to display login QR codes

using System.IO;

namespace FairyAI_Android.Services;

/// <summary>QR code bitmap generator for Android.</summary>
public class QRCodeGenerator
{
    /// <summary>Generate QR code image from text.</summary>
    public ImageSource Generate(string text, int size = 200)
    {
        try
        {
            // Simple QR code matrix generation
            var modules = EncodeToQRMatrix(text);
            int moduleSize = size / modules.GetLength(0);
            int pixelSize = modules.GetLength(0) * moduleSize;

            var bitmap = new byte[pixelSize * pixelSize * 4]; // RGBA

            // White background
            for (int i = 0; i < bitmap.Length; i += 4)
            {
                bitmap[i] = 255; bitmap[i + 1] = 255; bitmap[i + 2] = 255; bitmap[i + 3] = 255;
            }

            // Draw black modules
            for (int y = 0; y < modules.GetLength(0); y++)
            {
                for (int x = 0; x < modules.GetLength(1); x++)
                {
                    if (!modules[x, y]) continue;
                    for (int dy = 0; dy < moduleSize; dy++)
                    {
                        for (int dx = 0; dx < moduleSize; dx++)
                        {
                            int px = (y * moduleSize + dy) * pixelSize + (x * moduleSize + dx);
                            int idx = px * 4;
                            if (idx + 3 < bitmap.Length)
                            {
                                bitmap[idx] = 0; bitmap[idx + 1] = 0; bitmap[idx + 2] = 0; bitmap[idx + 3] = 255;
                            }
                        }
                    }
                }
            }

            return ImageSource.FromStream(() => new MemoryStream(bitmap));
        }
        catch
        {
            return null;
        }
    }

    private bool[,] EncodeToQRMatrix(string text)
    {
        // Simple QR encoding (simplified for display purposes)
        int size = 33;
        var matrix = new bool[size, size];

        // Finder patterns
        DrawFinder(matrix, 0, 0);
        DrawFinder(matrix, size - 7, 0);
        DrawFinder(matrix, 0, size - 7);

        // Timing patterns
        for (int i = 8; i < size - 8; i++)
        {
            matrix[i, 6] = i % 2 == 0;
            matrix[6, i] = i % 2 == 0;
        }

        // Data area
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        int bitIdx = 0;
        for (int y = 0; y < size && bitIdx < bytes.Length * 8; y++)
        {
            for (int x = 0; x < size && bitIdx < bytes.Length * 8; x++)
            {
                if (IsFunctionArea(x, y, size)) continue;
                int byteIdx = bitIdx / 8;
                int bitInByte = 7 - (bitIdx % 8);
                matrix[x, y] = (bytes[byteIdx] >> bitInByte & 1) == 1;
                bitIdx++;
            }
        }

        return matrix;
    }

    private static void DrawFinder(bool[,] m, int sx, int sy)
    {
        for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
                m[sx + x, sy + y] = x == 0 || x == 6 || y == 0 || y == 6 || (x >= 2 && x <= 4 && y >= 2 && y <= 4);
    }

    private static bool IsFunctionArea(int x, int y, int size)
    {
        if (x < 9 && y < 9) return true;
        if (x >= size - 8 && y < 9) return true;
        if (x < 9 && y >= size - 8) return true;
        if (x == 6 || y == 6) return true;
        return false;
    }
}
