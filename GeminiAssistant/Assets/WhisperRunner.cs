using UnityEngine;
using System;
using System.Diagnostics;
using System.Collections;

public class WhisperRunner : MonoBehaviour
{
    public string pythonPath = @"C:\Users\curyt\AppData\Local\Python\bin\python.exe";
    public string scriptPath = @"C:\Users\curyt\Documents\transcribe.py";

    // Event to send cleaned transcription to ChatUI
    public event Action<string> OnTranscription;

    public string wakeWord = "assistant"; // optional
    private bool isListening = false;

    // Start continuous listening (called from button)
    public void StartListening()
    {
        if (!isListening)
            StartCoroutine(ListenLoop());
    }

    public void StopListening()
    {
        isListening = false;
    }

    IEnumerator ListenLoop()
    {
        isListening = true;

        while (isListening)
        {
            yield return StartCoroutine(RunWhisperCoroutine());
            yield return new WaitForSeconds(0.5f); // small delay to avoid spam
        }
    }

    IEnumerator RunWhisperCoroutine()
    {
        UnityEngine.Debug.Log("WhisperRunner: Recording & transcribing...");

        var psi = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = "\"" + scriptPath + "\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = psi })
        {
            process.Start();

            // wait until process exits
            while (!process.HasExited)
                yield return null;

            string fullOutput = process.StandardOutput.ReadToEnd().Trim();
            UnityEngine.Debug.Log("Whisper Output RAW:\n" + fullOutput);

            string[] lines = fullOutput.Split('\n');
            string output = "";

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();

                if (!string.IsNullOrEmpty(line))
                {
                    output = line;
                    break;
                }
            }

            UnityEngine.Debug.Log("Extracted speech: [" + output + "]");

            string lower = output.ToLower();

            if (string.IsNullOrEmpty(output) ||
                lower.Contains("recording") ||
                lower.Contains("transcribing"))
            {
                UnityEngine.Debug.LogWarning("No valid speech detected.");

                string fallback = "I didn't catch that. Could you repeat?";
                UnityEngine.Debug.Log("Firing fallback transcription: " + fallback);

                OnTranscription?.Invoke(fallback);
            }

            else if (lower.Contains(wakeWord))
            {
                UnityEngine.Debug.Log("Wake word detected!");

                string cleaned = lower.Replace(wakeWord, "").Trim();

                UnityEngine.Debug.Log("Firing OnTranscription with: " + cleaned);
                OnTranscription?.Invoke(cleaned);
            }

            else
            {
                UnityEngine.Debug.Log("Sending transcription without wake word filter: " + output);
                OnTranscription?.Invoke(output);
            }
        }
    }
}

           