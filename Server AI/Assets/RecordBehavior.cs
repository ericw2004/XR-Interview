using UnityEngine;
using System.Collections;
using TMPro;

public class RecordBehaviour : MonoBehaviour
{

    [Header("Managers")]
    public Recorder recorder;
    public AICommunicator AiCommunicator;
    public AudioManager audioManager;

    string wakeWord = "assistant";

    private bool isProcessing = false;

    // SETTINGS (tweak these if needed)
    float minVolume = 0.0005f;
    float silenceDuration = 1.5f;

    void Start()
    {
        StartCoroutine(ContinuousListening());
    }

    IEnumerator ContinuousListening()
    {
        while (true)
        {
            Debug.Log("Listening...");

            // Small delay so mic resets cleanly
            yield return new WaitForSeconds(0.3f);

            yield return StartCoroutine(recorder.RecordUntilSilence());

            AudioClip finalClip = recorder.GetRecordedClip();

            if (finalClip == null)
            {
                Debug.LogWarning("No audio recorded.");
                continue;
            }

            // CHECK IF THERE WAS ACTUAL SPEECH
            float finalVolume = GetVolume(finalClip);
            Debug.Log("Final volume: " + finalVolume);

            if (finalVolume < minVolume)
            {
                Debug.Log("Too quiet — skipping");
                continue;
            }

            // PREVENT SPAM REQUESTS
            if (isProcessing)
            {
                Debug.Log("Still processing previous request — skipping");
                continue;
            }

            isProcessing = true;

            if (AiCommunicator.GetAITextResponse() == null)
            {
                Debug.LogWarning("AI failed — resetting state");
                isProcessing = false;
                continue;
            }

            // SEND TO AI
            Debug.Log("Sending to AI...");
            yield return StartCoroutine(
                AiCommunicator.VoiceChat2(finalClip, "hello")
            );

            isProcessing = false;

            string userSpeech = AiCommunicator.LastUserQuery;

            if (string.IsNullOrEmpty(userSpeech))
            {
                Debug.Log("No transcription received.");
                continue;
            }

            Debug.Log("USER SAID: " + userSpeech);

            string cleanedSpeech = NormalizeText(userSpeech);
            string cleanedWakeWord = NormalizeText(wakeWord);

            Debug.Log("CLEANED SPEECH: " + cleanedSpeech);

            if (!cleanedSpeech.Contains(cleanedWakeWord))
            {
                Debug.Log("Wake word NOT detected — ignoring response");
                continue;
            }

            Debug.Log("Wake word detected!");

            AudioClip aiAudio = AiCommunicator.GetAIAudioResponse();
            string aiText = AiCommunicator.GetAITextResponse();

            Debug.Log("AI: " + aiText);

            // PLAY RESPONSE
            if (aiAudio != null)
            {
                audioManager.RemoveClipAndStop();
                audioManager.PlayAudio(aiAudio);
            }
            else
            {
                Debug.LogWarning("No AI audio received.");
            }

            // WAIT UNTIL AUDIO FINISHES
            while (audioManager.GetIsPlaying())
                yield return null;

            yield return new WaitForSeconds(0.5f);
        }
    }

    float GetVolume(AudioClip clip)
    {
        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }

        return sum / samples.Length;
    }

    string NormalizeText(string input)
    {
        input = input.ToLower();

        // remove punctuation
        input = input.Replace(",", "")
                     .Replace(".", "")
                     .Replace("?", "")
                     .Replace("!", "");

        // remove extra spaces
        input = System.Text.RegularExpressions.Regex.Replace(input, @"\s+", " ").Trim();

        return input;
    }
}