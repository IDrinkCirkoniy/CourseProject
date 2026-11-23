using System.Collections.Generic;
using UnityEngine;

public class BpmDetector : MonoBehaviour
{
    public AudioClip audioClip;

    [ContextMenu("Analyze BPM")]
    public void Analyze()
    {
        if (audioClip == null)
        {
            Debug.LogError("Аудиоклип не назначен!");
            return;
        }

        float bpm = CalculateBPM(audioClip);
        Debug.Log($"Рассчитанный BPM для трека {audioClip.name}: {bpm:F1}");
    }

    public float CalculateBPM(AudioClip clip)
    {
        int sampleRate = clip.frequency;
        int channelCount = clip.channels;
        int sampleCount = clip.samples;

        float[] samples = new float[sampleCount];
        clip.GetData(samples, 0);

        float[] monoSamples = new float[sampleCount / channelCount];
        for (int i = 0, j = 0; i < monoSamples.Length; i++, j += channelCount)
        {
            float sum = 0;
            for (int c = 0; c < channelCount; c++)
            {
                sum += samples[j + c];
            }
            monoSamples[i] = sum / channelCount;
        }

        int windowSize = 1024;
        int numWindows = monoSamples.Length / windowSize;
        float[] energyBuffer = new float[numWindows];

        for (int i = 0; i < numWindows; i++)
        {
            float sum = 0;

            for (int j = 0; j < windowSize; j++)
            {
                float sample = monoSamples[i * windowSize + j];
                sum += sample * sample;
            }

            energyBuffer[i] = Mathf.Sqrt(sum / windowSize);
        }

        List<int> peakIndices = new List<int>();
        int historySize = 43;
        float thresholdMultiplier = 1.35f;

        for (int i = historySize; i < numWindows - historySize; i++)
        {
            float localSum = 0;

            for (int j = i - historySize; j <= i + historySize; j++)
            {
                localSum += energyBuffer[j];
            }

            float localAverage = localSum / (historySize * 2 + 1);

            if (energyBuffer[i] > localAverage * thresholdMultiplier)
            {
                peakIndices.Add(i);
                i += 10;
            }
        }

        Dictionary<int, int> intervalCounts = new Dictionary<int, int>();
        int minBpm = 60;
        int maxBpm = 180;

        float windowDuration = (float)windowSize / sampleRate;
        int minIntervalFrames = Mathf.RoundToInt((60f / maxBpm) / windowDuration);
        int maxIntervalFrames = Mathf.RoundToInt((60f / minBpm) / windowDuration);

        for (int i = 0; i < peakIndices.Count; i++)
        {
            for (int j = i + 1; j < peakIndices.Count; j++)
            {
                int interval = peakIndices[j] - peakIndices[i];
                if (interval >= minIntervalFrames && interval <= maxIntervalFrames)
                {
                    if (intervalCounts.ContainsKey(interval))
                    {
                        intervalCounts[interval]++;
                    }
                    else
                    {
                        intervalCounts[interval] = 1;
                    }
                        
                }
            }
        }

        int bestInterval = 0;
        int maxCount = 0;
        
        foreach (var kvp in intervalCounts)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                bestInterval = kvp.Key;
            }
        }

        if (bestInterval == 0)
        {
           return 120f; 
        } 

        float intervalSeconds = bestInterval * windowDuration;
        float calculatedBpm = 60f / intervalSeconds;

        while (calculatedBpm < 90)
        {
            calculatedBpm *= 2;
        } 
        
        while (calculatedBpm > 180)
        {
            calculatedBpm /= 2;
        } 

        return calculatedBpm;
    }
}