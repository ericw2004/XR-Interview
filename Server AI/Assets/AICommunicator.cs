using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;
using System.Text; // Required for Encoding


public class AICommunicator : MonoBehaviour
{

    [SerializeField]
    const string API_URI = "http://172.172.161.211:8000/";      //POST URI
    public AudioClip AIAudioResponse;
    public string AITextResponse;
    public string LastUserQuery;

    public AudioClip GetAIAudioResponse()
    {
        return this.AIAudioResponse;
    }

    public string GetAITextResponse()
    {
        return this.AITextResponse;
    }
    //API

    public IEnumerator VoiceChat2(AudioClip recordedClip, string currentQuestion)
    {
        this.AIAudioResponse = null;
        this.AITextResponse = null;

        // First request to get the prompt and text response
        byte[] wavData = WavUtility.FromAudioClip(recordedClip);

        if (wavData == null)
        {
            Debug.LogError("T_SHARP: WAV object or stream is not ready/empty. Cannot send STT request.");
            yield break; // Exit coroutine if no data to send
        }

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "audio.wav", "audio/wav");
        form.AddField("current_question", currentQuestion);

        Debug.Log("T_SHARP : Preparing to make API call.");
        Debug.Log("T_SHARP : API URL IS " + API_URI + "voicechataiagent");

        //Set up the UnityWebRequest
        UnityWebRequest FirstRequest = UnityWebRequest.Post(API_URI + "voicechataiagent", form);
        FirstRequest.certificateHandler = new AcceptAllCertificates(); // Accept all SSL certificates
        // Send the request and decompress the multimedia response
        yield return FirstRequest.SendWebRequest();
        Debug.Log("T_SHARP : Made API call.");

        if (FirstRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("SERVER ERROR:");
            Debug.LogError(FirstRequest.downloadHandler.text);
            yield break;
        }

        VoiceChat2Response FirstResponse = JsonUtility.FromJson<VoiceChat2Response>(FirstRequest.downloadHandler.text);

        Debug.Log("USER SAID: " + FirstResponse.user_query);
        this.LastUserQuery = FirstResponse.user_query;
        Debug.Log("T_SHARP : Success on first call to API");
        Debug.Log($"T_SHARP : Text response => {FirstResponse.response}");
        Debug.Log($"T_SHARP : Audio location => {FirstResponse.audio_url}");
        //Debug.Log($"T_SHARP : Current question received by API => {FirstResponse.current_question_received}"); // Verify it was received

        this.AITextResponse = FirstResponse.response; 

        // Create and send the second UnityWebRequest (removed because the first request already has the audio URL)

        // Logging and sending the second request to API /static/
        Debug.Log($"T_SHARP : Making request to second API @ {API_URI + FirstResponse.audio_url.Substring(1)}"); // Substring(1) to remove leading '/' for formatting
        UnityWebRequest AudioRequest = UnityWebRequest.Get(API_URI + FirstResponse.audio_url.Substring(1)); // Corrected URL formatting
        AudioRequest.downloadHandler = new DownloadHandlerAudioClip(API_URI + FirstResponse.audio_url.Substring(1), AudioType.MPEG);

        Debug.Log("T_SHARP : Sending second API request for audio");
        yield return AudioRequest.SendWebRequest();
        Debug.Log($"T_SHARP : Audio API request status {AudioRequest.result}");

        if (AudioRequest.result == UnityWebRequest.Result.Success)
        {
            AudioClip AiAudioClip = DownloadHandlerAudioClip.GetContent(AudioRequest);
            this.AIAudioResponse = AiAudioClip;
            Debug.Log("T_SHARP : Process complete, Audio available at AiAudioResponse.");
        }
        else
        {
            Debug.LogError($"T_SHARP : Failed to make call to second API for audio! Error: {AudioRequest.error}"); // Log the actual error
            yield break;
        }
    }


    // Class to deserialize the FastAPI response from /voicechat2
    [Serializable]
    public class VoiceChat2Response
    {
        public string user_query; // text representation of user's voice recording question
        public string response; // Gemini response text
        public string audio_url; // Gemini response audio URL
        //public string current_question_received; // for debugging
    }
}