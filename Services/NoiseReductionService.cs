using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Noise reduction for Android microphone input.
/// Spectral subtraction + adaptive gain + VAD for background speech recognition.
/// </summary>
public class NoiseReductionService
{
    private const int FrameSize = 256;
    private const int Overlap = 128;
    private const float NoiseFloorFactor = 0.3f;
    private const float SpectralFloor = 0.01f;
    private const float SensitivityGain = 1.8f;

    private float[]? _noiseProfile;
    private bool _noiseProfileReady;
    private int _calibrationFrames;
    private const int CalibrationFrameCount = 20;

    public float[] Process(float[] audio)
    {
        if (audio.Length < FrameSize) return audio;

        var output = new float[audio.Length];
        var hopSize = FrameSize - Overlap;
        var window = CreateHannWindow(FrameSize);

        for (int pos = 0; pos + FrameSize <= audio.Length; pos += hopSize)
        {
            var frame = new float[FrameSize];
            for (int i = 0; i < FrameSize; i++)
                frame[i] = audio[pos + i] * window[i];

            var spectrum = FFT(frame);
            var magnitude = new float[FrameSize / 2 + 1];
            var phase = new float[FrameSize / 2 + 1];
            for (int i = 0; i <= FrameSize / 2; i++)
            {
                magnitude[i] = (float)Math.Sqrt(spectrum[2 * i] * spectrum[2 * i] + spectrum[2 * i + 1] * spectrum[2 * i + 1]);
                phase[i] = (float)Math.Atan2(spectrum[2 * i + 1], spectrum[2 * i]);
            }

            if (!_noiseProfileReady)
            {
                _noiseProfile ??= new float[FrameSize / 2 + 1];
                for (int i = 0; i <= FrameSize / 2; i++)
                    _noiseProfile[i] = (_noiseProfile[i] * _calibrationFrames + magnitude[i]) / (_calibrationFrames + 1);
                _calibrationFrames++;
                if (_calibrationFrames >= CalibrationFrameCount)
                    _noiseProfileReady = true;
            }

            if (_noiseProfileReady && _noiseProfile != null)
            {
                for (int i = 0; i <= FrameSize / 2; i++)
                {
                    float noiseEst = _noiseProfile[i] * NoiseFloorFactor;
                    float enhanced = magnitude[i] - noiseEst;
                    if (enhanced < SpectralFloor * magnitude[i])
                        enhanced = SpectralFloor * magnitude[i];
                    magnitude[i] = Math.Max(0, enhanced);
                }
            }

            float frameEnergy = 0;
            for (int i = 0; i < magnitude.Length; i++)
                frameEnergy += magnitude[i] * magnitude[i];
            frameEnergy /= magnitude.Length;

            bool isSpeech = frameEnergy > 0.001f;
            float gain = isSpeech ? SensitivityGain : 0.5f;
            for (int i = 0; i <= FrameSize / 2; i++)
                magnitude[i] *= gain;

            for (int i = 0; i <= FrameSize / 2; i++)
            {
                spectrum[2 * i] = magnitude[i] * (float)Math.Cos(phase[i]);
                spectrum[2 * i + 1] = magnitude[i] * (float)Math.Sin(phase[i]);
            }
            for (int i = 1; i < FrameSize / 2; i++)
            {
                spectrum[2 * (FrameSize - i)] = spectrum[2 * i];
                spectrum[2 * (FrameSize - i) + 1] = -spectrum[2 * i + 1];
            }

            var processed = IFFT(spectrum);
            for (int i = 0; i < FrameSize; i++)
                output[pos + i] += processed[i] * window[i];
        }

        for (int i = 0; i < output.Length; i++)
            output[i] /= 2.0f;

        return output;
    }

    public byte[] ProcessPcm16(byte[] pcm)
    {
        var samples = new float[pcm.Length / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;
        var processed = Process(samples);
        var result = new byte[processed.Length * 2];
        for (int i = 0; i < processed.Length; i++)
        {
            short s = (short)(Math.Clamp(processed[i], -1f, 1f) * 32767f);
            BitConverter.GetBytes(s).CopyTo(result, i * 2);
        }
        return result;
    }

    public void ResetNoiseProfile()
    {
        _noiseProfile = null;
        _noiseProfileReady = false;
        _calibrationFrames = 0;
    }

    private static float[] CreateHannWindow(int size)
    {
        var w = new float[size];
        for (int i = 0; i < size; i++)
            w[i] = 0.5f * (1 - (float)Math.Cos(2 * Math.PI * i / (size - 1)));
        return w;
    }

    private static float[] FFT(float[] input)
    {
        int n = input.Length;
        var output = new float[2 * n];
        for (int i = 0; i < n; i++) { output[2 * i] = input[i]; output[2 * i + 1] = 0; }
        FFTInPlace(output, n);
        return output;
    }

    private static float[] IFFT(float[] spectrum)
    {
        int n = spectrum.Length / 2;
        var output = new float[n];
        for (int i = 0; i < n; i++) spectrum[2 * i + 1] = -spectrum[2 * i + 1];
        FFTInPlace(spectrum, n);
        for (int i = 0; i < n; i++) output[i] = spectrum[2 * i] / n;
        return output;
    }

    private static void FFTInPlace(float[] data, int n)
    {
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[2 * i], data[2 * j]) = (data[2 * j], data[2 * i]);
                (data[2 * i + 1], data[2 * j + 1]) = (data[2 * j + 1], data[2 * i + 1]);
            }
            int k = n / 2;
            while (k <= j) { j -= k; k /= 2; }
            j += k;
        }
        for (int step = 1; step < n; step *= 2)
        {
            float angle = (float)(-Math.PI / step);
            float wR = (float)Math.Cos(angle), wI = (float)Math.Sin(angle);
            for (int g = 0; g < n; g += 2 * step)
            {
                float cR = 1, cI = 0;
                for (int p = 0; p < step; p++)
                {
                    int a = g + p, b = a + step;
                    float tR = cR * data[2 * b] - cI * data[2 * b + 1];
                    float tI = cR * data[2 * b + 1] + cI * data[2 * b];
                    data[2 * b] = data[2 * a] - tR;
                    data[2 * b + 1] = data[2 * a + 1] - tI;
                    data[2 * a] += tR;
                    data[2 * a + 1] += tI;
                    float nR = cR * wR - cI * wI;
                    cI = cR * wI + cI * wR;
                    cR = nR;
                }
            }
        }
    }
}
