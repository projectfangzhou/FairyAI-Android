using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Voiceprint Recognition — speaker verification using spectral features.
/// </summary>
public class VoiceprintService
{
    private readonly string _embeddingPath;
    private readonly float _threshold;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public bool IsEnrolled => File.Exists(_embeddingPath);

    public VoiceprintService()
    {
        _embeddingPath = Path.Combine(FileSystem.AppDataDirectory, "voiceprint.dat");
        _threshold = 0.55f;
    }

    public async Task<(bool Success, float Quality)> EnrollAsync(byte[] audioData)
    {
        if (audioData.Length < 100) return (false, 0f);
        var features = ExtractFeatures(audioData);
        if (features.Length == 0) return (false, 0f);

        var json = System.Text.Json.JsonSerializer.Serialize(features);
        await File.WriteAllTextAsync(_embeddingPath, json);
        return (true, 1f);
    }

    public (bool IsMatch, float Confidence) Verify(byte[] audioData)
    {
        if (!IsEnrolled) return (true, 1f);

        try
        {
            var stored = System.Text.Json.JsonSerializer.Deserialize<float[]>(
                File.ReadAllText(_embeddingPath));
            var current = ExtractFeatures(audioData);
            if (stored == null || stored.Length == 0 || current.Length == 0)
                return (true, 1f);

            int len = Math.Min(stored.Length, current.Length);
            float dist = 0;
            for (int i = 0; i < len; i++)
            {
                float d = stored[i] - current[i];
                dist += d * d;
            }
            float confidence = 1f / (1f + (float)Math.Sqrt(dist));
            return (confidence >= _threshold, confidence);
        }
        catch
        {
            return (true, 0.5f);
        }
    }

    private static float[] ExtractFeatures(byte[] audioData)
    {
        int headerSize = 44;
        if (audioData.Length < headerSize + 400) return Array.Empty<float>();

        int sampleCount = (audioData.Length - headerSize) / 2;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            samples[i] = BitConverter.ToInt16(audioData, headerSize + i * 2) / 32768f;

        var features = new float[12];
        features[0] = (float)Math.Sqrt(samples.Sum(s => s * s) / sampleCount);
        features[1] = samples.Max(Math.Abs);

        int zcr = 0;
        for (int i = 1; i < sampleCount; i++)
            if ((samples[i] >= 0) != (samples[i - 1] >= 0)) zcr++;
        features[2] = (float)zcr / sampleCount;

        int fftSize = 128;
        int frames = Math.Min(sampleCount / fftSize, 6);
        for (int b = 0; b < 8; b++)
        {
            float bandSum = 0;
            for (int f = 0; f < frames; f++)
            {
                int start = f * fftSize;
                for (int k = b * 8; k < (b + 1) * 8 && k < fftSize / 2; k++)
                {
                    float re = 0, im = 0;
                    float freq = (float)k / fftSize;
                    for (int n = 0; n < fftSize; n += 4)
                    {
                        float angle = 2f * MathF.PI * freq * n;
                        re += samples[start + n] * MathF.Cos(angle);
                        im -= samples[start + n] * MathF.Sin(angle);
                    }
                    bandSum += re * re + im * im;
                }
            }
            features[3 + b] = bandSum / (frames + 1);
        }

        return features;
    }
}
