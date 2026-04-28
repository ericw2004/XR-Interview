using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;

public class ControlManager : MonoBehaviour
{
  [SerializeField]
  public Recorder recorder;
  public AICommunicator AiCommunicator;
  private AudioSource audioSource;

  // Awake is called when the script instance is being loaded
  void Awake()
  {
    audioSource = gameObject.AddComponent<AudioSource>();
  }

  // Update is called once per frame
  void Update()
  {
    // microphone recording AND ai communication
    if (OVRInput.GetDown(OVRInput.RawButton.A))
    {
      if (recorder.GetIsRecording())
      {
        recorder.StopRecording();

        StartCoroutine(TalkToAI());
      }
      else
      {
        recorder.StartRecording();
      }
    }
  }

  public IEnumerator TalkToAI()
  {
    if (recorder.GetRecordedClip() == null) {
      Debug.LogError("T_SHARP : Recording is empty! Exiting...");
      yield break;
    }

    Debug.Log("T_SHARP : Starting Coroutine VoiceChat2()");
    yield return StartCoroutine(AiCommunicator.VoiceChat2(recorder.GetRecordedClip(), "hello"));

    AudioClip AiAudioResponse = AiCommunicator.GetAIAudioResponse();
    string AiTextResponse = AiCommunicator.GetAITextResponse();

    audioSource.clip = AiAudioResponse;
    audioSource.Play();

    Debug.Log($"T_SHARP : Updating AI Tablet Text => {AiTextResponse}.");
  }
}