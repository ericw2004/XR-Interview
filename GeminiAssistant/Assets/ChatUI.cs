using UnityEngine;
using TMPro;
using Meta.WitAi.TTS.Utilities;
public class ChatUI : MonoBehaviour
{
    public TMP_Text responseText;
    public GeminiManager gemini;
    public WhisperRunner whisper;
    public TTSSpeaker speaker;

    void Start()
    {
        if (gemini == null) Debug.LogError("Gemini reference is NULL!");
        if (whisper == null) Debug.LogError("Whisper reference is NULL!");
        if (responseText == null) Debug.LogError("ResponseText is NULL!");

        // Subscribe to Gemini responses
        gemini.OnResponse += UpdateText;

        // Subscribe to Whisper transcriptions
        if (whisper != null)
        {
            whisper.OnTranscription += SendVoiceMessage;
            Debug.Log("Subscribed to Whisper OnTranscription!");
        }
    }

    // Call this from your Speak button
    public void StartListening()
    {
        if (responseText != null) responseText.text = "Listening...";
        if (whisper != null) whisper.StartListening();
    }

    // Called when Whisper transcribes something
    void SendVoiceMessage(string spokenText)
    {
        Debug.Log("SendVoiceMessage called! Text = " + spokenText);

        // If it's the fallback message, don't send to Gemini
        if (spokenText == "I didn't catch that. Could you repeat?")
        {
            responseText.text = spokenText;

            if (speaker != null)
            {
                speaker.Stop();
                speaker.Speak(spokenText);
            }
        
            return;
        }

        responseText.text = "Thinking...";
        gemini.SendPrompt(spokenText);
    }

    void UpdateText(string text)
    {
        Debug.Log("Raw Gemini text: " + text);

        string cleaned = CleanText(text);

        Debug.Log("Cleaned text: " + cleaned);

        if (responseText != null) responseText.text = cleaned;

        if (speaker != null)
        {
            Debug.Log("Speaking: " + cleaned);
            speaker.Stop(); // prevents overlap
            speaker.Speak(cleaned);
        }
    }

    string CleanText(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Remove escape characters
        string cleaned = input.Replace("\\n", " ")
                              .Replace("\n", " ")
                              .Replace("\r", " ")
                              .Replace("\t", " ");

        // Remove markdown symbols
        cleaned = cleaned.Replace("*", "")
                         .Replace("_", "")
                         .Replace("#", "")
                         .Replace("`", "");

        // Optional: remove extra spaces
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");

        return cleaned.Trim();
    }
}

