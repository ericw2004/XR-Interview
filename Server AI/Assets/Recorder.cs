using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;
using System.Text; // Required for Encoding

public class Recorder : MonoBehaviour
{
    public string microphoneDevice;
    public int maxRecordingDuration = 10;
    public int sampleRate = 44100;

    private AudioClip recordedClip;
    private bool isRecording = false;

    private AudioSource audioSource;
    public int postId;

    public void Awake()
    {
        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
            Debug.Log("Using microphone: " + microphoneDevice);
        }
        else
        {
            Debug.LogWarning("No microphone found.");
        }

        // Setup audio source
        audioSource = gameObject.AddComponent<AudioSource>();
    }

    public IEnumerator RecordForSeconds(int seconds)
    {
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }

        recordedClip = Microphone.Start(microphoneDevice, false, seconds, sampleRate);

        if (recordedClip == null)
        {
            Debug.LogError("Failed to start recording!");
            yield break;
        }

        Debug.Log("Recording started...");

        float timer = 0f;

        while (timer < seconds)
        {
            if (!Microphone.IsRecording(microphoneDevice))
            {
                Debug.LogWarning("Microphone stopped unexpectedly.");
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Microphone.End(microphoneDevice);

        Debug.Log("Recording stopped.");
    }

    public void StartRecording()
    {
        if (microphoneDevice == null)
            return;

        Debug.Log("T_SHARP : Recording started.");
        recordedClip = Microphone.Start(microphoneDevice, false, maxRecordingDuration, sampleRate);
        isRecording = true;
    }

    public void StopRecording()
    {
        Microphone.End(microphoneDevice);
        isRecording = false;
        Debug.Log("T_SHARP : Recording stopped.");

        audioSource.clip = recordedClip;
    }

    public bool GetIsRecording()
    {
      return isRecording;
    }

    public AudioSource GetAudioSource()
    {
      return this.audioSource;
    }

    public AudioClip GetRecordedClip()
    {
      return this.recordedClip;
    }
    
    public void PlayRecording()
    {
      audioSource.Play();
    }

    public IEnumerator RecordUntilSilence(float silenceThreshold = 0.01f, float silenceDuration = 1.5f, float maxDuration = 10f)
    {
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }

        recordedClip = Microphone.Start(microphoneDevice, false, 30, sampleRate);

        if (recordedClip == null)
        {
            Debug.LogError("Failed to start recording!");
            yield break;
        }

        Debug.Log("Recording started...");

        float silenceTimer = 0f;
        float totalTimer = 0f;

        while (true)
        {
            float volume = GetCurrentVolume();
            Debug.Log("Live volume: " + volume);

            // If quiet → count silence
            if (volume < silenceThreshold)
            {
                silenceTimer += Time.deltaTime;
            }
            else
            {
                silenceTimer = 0f;
            }

            // Stop if silent long enough
            if (silenceTimer >= silenceDuration)
            {
                Debug.Log("Silence detected → stopping recording");
                break;
            }

            // Safety stop (prevents infinite recording)
            if (totalTimer >= maxDuration)
            {
                Debug.Log("Max recording time reached → stopping");
                break;
            }

            totalTimer += Time.deltaTime;
            yield return null;
        }

        Microphone.End(microphoneDevice);
        Debug.Log("Recording stopped.");
    }

    float GetCurrentVolume()
    {
        int micPosition = Microphone.GetPosition(microphoneDevice);

        if (micPosition < 128)
            return 0;

        float[] samples = new float[128];
        recordedClip.GetData(samples, micPosition - 128);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }

        return sum / samples.Length;
    }
}
