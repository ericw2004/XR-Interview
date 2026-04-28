using UnityEngine;
using System.IO;

public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        byte[] wavData = ConvertToWav(samples, clip.channels, clip.frequency);
        return wavData;
    }

    private static byte[] ConvertToWav(float[] samples, int channels, int sampleRate)
    {
        MemoryStream stream = new MemoryStream();
        int sampleCount = samples.Length;

        int byteRate = sampleRate * channels * 2;
        int subChunk2Size = sampleCount * 2;
        int chunkSize = 36 + subChunk2Size;

        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            // RIFF header
            writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(chunkSize);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

            // fmt subchunk
            writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2)); // Block align
            writer.Write((short)16); // Bits per sample

            // data subchunk
            writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            writer.Write(subChunk2Size);

            // PCM samples
            foreach (float sample in samples)
            {
                short intSample = (short)(Mathf.Clamp(sample, -1.0f, 1.0f) * short.MaxValue);
                writer.Write(intSample);
            }
             Debug.Log($"GBSCODE File Size (inside using): {stream.Length} bytes");
        }

        Debug.Log($"GBSCODE Samples: {samples.Length}, Channels: {channels}, SampleRate: {sampleRate}");
        //Debug.Log($"GBSCODE File Size: {stream.Length} bytes");

        return stream.ToArray();
    }
}