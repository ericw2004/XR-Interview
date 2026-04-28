using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;
using System.Text;
using System.ComponentModel; // Required for Encoding


public class AudioManager : MonoBehaviour
{

    public static AudioManager instance;
    
    [SerializeField]
    public AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        instance = this;
    }
    void Update()
    {
        if (!this.audioSource.isPlaying && this.audioSource != null)
        {
            //Actions.DonePlaying();
            Actions.DonePlaying?.Invoke();
        }
    }


    public void PlayAudio(AudioClip toPlay)
    {
        if (this.audioSource.isPlaying)
        {
            Debug.LogError("t# : something else is playing!");
            return;
        }
        Debug.Log("Trying to play audio clip: ");
        
        this.audioSource.clip = toPlay;
        audioSource.Play();
      Debug.Log("Successfully played audio clip: ");
      
    }


    public void PauseAudio()
    {
        if (this.audioSource.isPlaying)
        {
            audioSource.Pause();

        }
        else
        {
            Debug.LogWarning("t# : nothing is playing");
            return;
        }
    }


    public void ReplayAudio()
    {
        if (this.audioSource.clip == null)
        {
            Debug.LogError("t# : No recorded clip to play back");
            return;
        }

        this.audioSource.Stop();
        this.audioSource.time = 0f;
        this.PlayAudio(this.audioSource.clip);
    }


    public void PauseOrPlay(AudioClip toPlay)
    {
        if (this.audioSource.isPlaying)
        {
            this.PauseAudio();
        }
        else
        {
            this.PlayAudio(toPlay);
        }
    }

    public void RemoveClipAndStop()
    {
        this.audioSource.Stop();
        this.audioSource.clip = null;
    }

    public bool GetIsPlaying()
    {
        return this.audioSource.isPlaying;
    }
}
