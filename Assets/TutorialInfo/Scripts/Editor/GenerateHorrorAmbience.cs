using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class GenerateHorrorAmbience
{
    private const string AudioDirectory = "Assets/Audio";
    private const string AudioFileName = "HorrorAmbience_GasStation.wav";
    public static string FullAudioPath => Path.Combine(AudioDirectory, AudioFileName);

    static GenerateHorrorAmbience()
    {
        // EditorApplication.delayCall += EnsureAmbienceExists;
    }

    [MenuItem("Tools/Gas Station/Generate Horror Ambience Audio Clip")]
    public static void EnsureAmbienceExists()
    {
        if (File.Exists(FullAudioPath))
        {
            return;
        }

        if (!Directory.Exists(AudioDirectory))
        {
            Directory.CreateDirectory(AudioDirectory);
        }

        GenerateWavFile(FullAudioPath);
        AssetDatabase.Refresh();
        Debug.Log("[HorrorAmbience] Successfully generated atmospheric horror ambience WAV: " + FullAudioPath);
    }

    private static void GenerateWavFile(string filePath)
    {
        int sampleRate = 44100;
        int channels = 2;
        int durationSeconds = 12; // 12-second seamless loop
        int totalSamples = sampleRate * durationSeconds;

        short[] pcmData = new short[totalSamples * channels];

        // Atmospheric horror synthesis parameters
        float humBaseFreq = 60f; // 60Hz North American electrical mains hum
        float subDroneFreq = 48f; // Low dark sub-bass
        float tensionFreq = 62.2f; // Slight dissonant beat frequency (quarter-tone tension)

        // Pink/Brown noise generator states
        float b0 = 0f, b1 = 0f, b2 = 0f;
        System.Random rng = new System.Random(1337);

        // Crossfade length for seamless looping (1.5 seconds)
        int crossfadeSamples = (int)(sampleRate * 1.5f);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;

            // 1. Electrical Fluorescent Hum (60Hz + 120Hz + 180Hz overtones with subtle micro-flutter)
            float humPhase = 2f * Mathf.PI * humBaseFreq * t;
            float humMod = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
            float humSample = (Mathf.Sin(humPhase) * 0.45f
                            + Mathf.Sin(humPhase * 2f) * 0.25f
                            + Mathf.Sin(humPhase * 3f) * 0.12f) * humMod * 0.22f;

            // 2. Sinister Sub-Bass & Dissonant Dread Drone (48Hz + 62.2Hz)
            float subPhase1 = 2f * Mathf.PI * subDroneFreq * t;
            float subPhase2 = 2f * Mathf.PI * tensionFreq * t;
            float subDrone = (Mathf.Sin(subPhase1) * 0.6f + Mathf.Sin(subPhase2) * 0.4f) * 0.25f;

            // 3. Low-Frequency Atmospheric Gas Station Wind (Filtered noise with slow wind gust LFO)
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            b0 = 0.99f * b0 + white * 0.05f;
            b1 = 0.98f * b1 + b0 * 0.04f;
            b2 = 0.96f * b2 + b1 * 0.03f;
            float windNoise = b2;

            float windGustLFO = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (t / durationSeconds) * 2f);
            float windSample = windNoise * (0.15f + 0.18f * windGustLFO);

            // 4. Industrial Coolers Compressor Hum (low mechanical rumble ~90Hz)
            float compPhase = 2f * Mathf.PI * 89.5f * t;
            float compressorSample = Mathf.Sin(compPhase) * 0.08f;

            // Sum layers
            float leftSample = (humSample * 0.95f) + (subDrone * 1.0f) + (windSample * 1.1f) + (compressorSample * 0.85f);
            float rightSample = (humSample * 1.05f) + (subDrone * 1.0f) + (windSample * 0.9f) + (compressorSample * 1.15f);

            // Seamless Cross-fade loop endpoints to avoid any clicks
            if (i < crossfadeSamples)
            {
                float fadeRatio = (float)i / crossfadeSamples;
                int endIdx = totalSamples - crossfadeSamples + i;
                float endT = (float)endIdx / sampleRate;

                float endHumPhase = 2f * Mathf.PI * humBaseFreq * endT;
                float endHum = Mathf.Sin(endHumPhase) * 0.22f;
                leftSample = Mathf.Lerp(endHum, leftSample, fadeRatio);
                rightSample = Mathf.Lerp(endHum, rightSample, fadeRatio);
            }

            // Soft saturation / limiter
            leftSample = Mathf.Clamp(leftSample * 0.85f, -0.95f, 0.95f);
            rightSample = Mathf.Clamp(rightSample * 0.85f, -0.95f, 0.95f);

            pcmData[i * channels] = (short)(leftSample * 32767f);
            pcmData[i * channels + 1] = (short)(rightSample * 32767f);
        }

        using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            // RIFF header
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            int subChunk2Size = totalSamples * channels * sizeof(short);
            bw.Write(36 + subChunk2Size);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt subchunk
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16); // Subchunk1Size for PCM
            bw.Write((short)1); // AudioFormat 1 = PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * sizeof(short)); // ByteRate
            bw.Write((short)(channels * sizeof(short))); // BlockAlign
            bw.Write((short)16); // BitsPerSample

            // data subchunk
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(subChunk2Size);

            // Write PCM audio samples
            byte[] byteBuffer = new byte[pcmData.Length * 2];
            Buffer.BlockCopy(pcmData, 0, byteBuffer, 0, byteBuffer.Length);
            bw.Write(byteBuffer);
        }
    }
}
