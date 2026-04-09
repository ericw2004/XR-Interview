using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System;

public class GeminiManager : MonoBehaviour
{
    public string apiKey = "AIzaSyAuc_XX8n-CRrz6-0xfiDbcyp3B2E80jYI";

    public Action<string> OnResponse;

    private bool isRequesting = false;

    public void SendPrompt(string prompt)
    {
        if (isRequesting)
        {
            Debug.LogWarning("Request already in progress — blocking duplicate call.");
            return;
        }

        StartCoroutine(SendRequest(prompt));
    }

    IEnumerator SendRequest(string prompt)
    {
        isRequesting = true;

        string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent";

        string jsonBody = @"
        {
          ""contents"": [
            {
              ""parts"": [
                {
                  ""text"": """ + EscapeJson(prompt) + @"""
                }
              ]
            }
          ]
        }";

        Debug.Log("Sending request...");
        Debug.Log("Prompt: " + prompt);

        int retries = 0;
        int maxRetries = 3;

        while (retries < maxRetries)
        {
            UnityWebRequest request = new UnityWebRequest(url, "POST");

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", apiKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string fullResponse = request.downloadHandler.text;
                Debug.Log("FULL RESPONSE: " + fullResponse);

                string responseText = ExtractText(fullResponse);
                OnResponse?.Invoke(responseText);

                isRequesting = false;
                yield break;
            }
            else
            {
                Debug.LogError("Error: " + request.error);
                Debug.LogError("Response: " + request.downloadHandler.text);

                // Handle 429 retry
                if (request.responseCode == 429)
                {
                    retries++;
                    float waitTime = Mathf.Pow(2, retries); // 2s, 4s, 8s
                    Debug.LogWarning($"429 hit. Retrying in {waitTime} seconds...");
                    yield return new WaitForSeconds(waitTime);
                }
                else
                {
                    break;
                }
            }
        }

        Debug.LogError("Request failed after retries.");
        isRequesting = false;
    }

    string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }

    string ExtractText(string json)
    {
        try
        {
            string marker = "\"text\": \"";
            int start = json.IndexOf(marker);

            if (start == -1)
                return "No text found in response";

            start += marker.Length;
            int end = json.IndexOf("\"", start);

            if (end == -1)
                return "Parse error";

            return json.Substring(start, end - start);
        }
        catch (Exception e)
        {
            Debug.LogError("Parse error: " + e.Message);
            return "Error parsing response";
        }
    }
}
