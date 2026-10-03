using System.IO;

namespace FairyAI_Android.Services;

/// <summary>FFmpeg audio converter and slicer.</summary>
public class FfmpegAudioConverter
{
    public bool IsAvailable => true;

    public async Task<byte[]> ConvertToWavAsync(byte[] audioData)
    {
        // Convert audio to WAV using ffmpeg
        var inputPath = Path.Combine(Path.GetTempPath(), $"input_{Guid.NewGuid():N}.tmp");
        var outputPath = Path.Combine(Path.GetTempPath(), $"output_{Guid.NewGuid():N}.wav");
        try
        {
            await File.WriteAllBytesAsync(inputPath, audioData);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{inputPath}\" -ar 24000 -ac 1 \"{outputPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null) await process.WaitForExitAsync();
            if (File.Exists(outputPath))
                return await File.ReadAllBytesAsync(outputPath);
            return audioData;
        }
        finally
        {
            try { File.Delete(inputPath); } catch { }
            try { File.Delete(outputPath); } catch { }
        }
    }

    public async Task<List<string>> SliceAsync(string inputPath, double chunkSeconds = 10.0)
    {
        var results = new List<string>();
        var outputDir = Path.Combine(Path.GetTempPath(), $"slices_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{inputPath}\" -f segment -segment_time {chunkSeconds:F1} -ar 24000 -ac 1 \"{outputDir}/slice_%03d.wav\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null) await process.WaitForExitAsync();
            results = Directory.GetFiles(outputDir, "*.wav").OrderBy(f => f).ToList();
        }
        catch { }
        return results;
    }
}

/// <summary>Audio slicer for voice clone preprocessing.</summary>
public class AudioSlicer
{
    public async Task<List<string>> SliceAsync(string inputPath, double chunkSeconds = 10.0, string outputDir = "")
    {
        var results = new List<string>();
        if (string.IsNullOrEmpty(outputDir))
            outputDir = Path.Combine(Path.GetTempPath(), $"slices_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{inputPath}\" -f segment -segment_time {chunkSeconds:F1} -ar 24000 -ac 1 \"{outputDir}/slice_%03d.wav\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null) await process.WaitForExitAsync();
            results = Directory.GetFiles(outputDir, "*.wav").OrderBy(f => f).ToList();
        }
        catch { }
        return results;
    }
}

