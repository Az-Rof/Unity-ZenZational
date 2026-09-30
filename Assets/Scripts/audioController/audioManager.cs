using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance; // Singleton instance AudioManager
    public Audio[] musicSounds, sfxSounds; // Array 
    public AudioSource musicSource, sfxSource; // AudioSource 
    private string currentSceneName;
    private bool isInitialized = false;

    public Slider musicSlider;
    public Slider sfxSlider;
    private List<AudioSource> activeLoopSFX = new List<AudioSource>();

    public void Awake()
    {
        musicSlider = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == "musicVolume")?.GetComponent<Slider>();
        sfxSlider = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == "sfxVolume")?.GetComponent<Slider>();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            currentSceneName = SceneManager.GetActiveScene().name;
            // PlayMusicForScene(currentSceneName);
            isInitialized = true;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void Start()
    {
        musicSlider = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == "musicVolume")?.GetComponent<Slider>();
        sfxSlider = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == "sfxVolume")?.GetComponent<Slider>();

        if (musicSlider != null)
            musicSlider.value = PlayerPrefs.GetFloat("musicVolume", musicSlider.value);
        if (sfxSlider != null)
            sfxSlider.value = PlayerPrefs.GetFloat("sfxVolume", sfxSlider.value);

        if (PlayerPrefs.HasKey("musicVolume"))
        {
            float musicVolume = PlayerPrefs.GetFloat("musicVolume");
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }
            if (musicSlider != null)
            {
                musicSlider.value = musicVolume;
            }
        }
        if (PlayerPrefs.HasKey("sfxVolume"))
        {
            float sfxVolume = PlayerPrefs.GetFloat("sfxVolume");
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }
            if (sfxSlider != null)
            {
                sfxSlider.value = sfxVolume;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentSceneName = scene.name;

        if (isInitialized)
        {
            CheckMusicForScene(currentSceneName);
        }
    }

    private void CheckMusicForScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        string requiredMusicName = null;

        switch (sceneName)
        {
            case "MainMenu":
                requiredMusicName = "PlayTheme";
                break;
            default:
                requiredMusicName = "PlayTheme";
                break;
        }

        if (!string.IsNullOrEmpty(requiredMusicName))
        {
            Audio targetSound = Array.Find(musicSounds, x => x.name == requiredMusicName);

            if (targetSound != null &&
                (musicSource.clip != targetSound.audioClip || !musicSource.isPlaying))
            {
                PlayMusic(requiredMusicName);
            }
            else if (targetSound != null)
            {
                Debug.Log($"Music {requiredMusicName} is already playing for scene {sceneName}.");
            }
        }
    }

    // private void PlayMusicForScene(string sceneName)
    // {
    //     if (string.IsNullOrEmpty(sceneName)) return;

    //     switch (sceneName)
    //     {
    //         case "MainMenu":
    //             PlayMusic("PlayTheme");
    //             break;
    //         default:
    //             PlayMusic("PlayTheme");
    //             break;
    //     }
    // }

    public void PlayMusic(string name)
    {
        if (musicSource == null)
        {
            Debug.LogWarning("MusicSource is missing or destroyed. Attempting to recreate.");
            musicSource = gameObject.AddComponent<AudioSource>();
        }
        Audio sound = Array.Find(musicSounds, x => x.name == name);

        if (sound == null)
        {
            Debug.LogError($"Music sound not found: {name}");
            return;
        }

        if (musicSource.clip == sound.audioClip && musicSource.isPlaying)
        {
            Debug.Log($"Music {name} is already playing.");
            return;
        }

        // Set the clip of the music source to the audio clip of the sound
        musicSource.clip = sound.audioClip;
        musicSource.loop = true;
        // Play the music
        musicSource.Play();

        Debug.Log($"Now playing: {name}");
    }

    public void PlaySFX(string name, float startTime = 0f, float finishTime = 0f)
    {

        Audio sound = Array.Find(sfxSounds, x => x.name == name);

        if (sound == null)
        {
            Debug.LogError($"SFX sound not found: {name}");
            return;
        }

        float clipLength = sound.audioClip.length;

        GameObject tempSFX = new GameObject($"SFX_{name}");
        AudioSource tempAudioSource = tempSFX.AddComponent<AudioSource>();

        tempAudioSource.clip = sound.audioClip;
        tempAudioSource.volume = sfxSource != null ? sfxSource.volume : 1.0f; // default volume if null
        tempAudioSource.time = Mathf.Clamp(startTime, 0f, clipLength);
        tempAudioSource.Play();

        float sfxDuration = clipLength - tempAudioSource.time;
        if (finishTime > tempAudioSource.time && finishTime < clipLength)
        {
            sfxDuration = finishTime - tempAudioSource.time;
        }

        Destroy(tempSFX, sfxDuration);
    }

    // public void PlaySFXLoop(string name, float startTime = 0f, float finishTime = 0f)
    // {
    //     Audio sound = Array.Find(sfxSounds, x => x.name == name);

    //     if (sound == null)
    //     {
    //         Debug.LogError($"SFX sound not found: {name}");
    //         return;
    //     }
    //     float clipLength = sound.audioClip.length;

    //     GameObject tempSFX = new GameObject($"SFX_{name}");
    //     AudioSource tempAudioSource = tempSFX.AddComponent<AudioSource>();

    //     tempAudioSource.clip = sound.audioClip;
    //     tempAudioSource.volume = sfxSource != null ? sfxSource.volume : 1.0f; // default volume if null
    //     tempAudioSource.time = Mathf.Clamp(startTime, 0f, clipLength);
    //     tempAudioSource.loop = true;
    //     tempAudioSource.Play();

    //     float sfxDuration = clipLength - tempAudioSource.time;
    //     if (finishTime > tempAudioSource.time && finishTime < clipLength)
    //     {
    //         sfxDuration = finishTime - tempAudioSource.time;
    //     }
    //     return;
    // }
    public AudioSource PlaySFXLoop(string name, float startTime, float finishTime)
    {
        Audio sound = Array.Find(sfxSounds, x => x.name == name);

        if (sound == null)
        {
            Debug.LogError($"SFX sound not found: {name}");
            return null;
        }

        AudioClip clip = sound.audioClip;
        float start = Mathf.Clamp(startTime, 0f, clip.length);
        float finish = Mathf.Clamp(finishTime, start + 0.001f, clip.length);

        GameObject tempSFX = new GameObject($"SFX_{name}_Loop");
        AudioSource tempAudioSource = tempSFX.AddComponent<AudioSource>();

        tempAudioSource.clip = clip;
        tempAudioSource.volume = sfxSource != null ? sfxSource.volume : 1.0f; // default volume if null
        tempAudioSource.loop = true;
        tempAudioSource.time = start;
        tempAudioSource.Play();

        activeLoopSFX.Add(tempAudioSource);

        StartCoroutine(LoopSegment(tempAudioSource, start, finish));

        return tempAudioSource;
    }
    
    IEnumerator LoopSegment(AudioSource source, float startTime, float finishTime)
    {
        while (source != null)
        {
            if (Time.timeScale == 0f)
            {
                if (source.isPlaying)
                    source.Pause();
            }
            else
            {
                if (!source.isPlaying)
                    source.UnPause();

                if (source.time >= finishTime)
                    source.time = startTime;
            }
            yield return null;
        }

        activeLoopSFX.Remove(source);
    }

    public void StopSFX(AudioSource source)
    {
        if (source == null) return;
        activeLoopSFX.Remove(source);
        Destroy(source.gameObject);
    }
    public void SetMusicVolume(float volume)
    {

        if (musicSource == null)
        {
            Debug.LogWarning("MusicSource is missing or destroyed. Cannot set volume.");
            return;
        }
        musicSource.volume = volume;
        PlayerPrefs.SetFloat("musicVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {

        if (sfxSource == null)
        {
            Debug.LogWarning("SFXSource is missing or destroyed. Cannot set volume.");
            return;
        }
        sfxSource.volume = volume;
        PlayerPrefs.SetFloat("sfxVolume", volume);
        PlayerPrefs.Save();
    }

    public void musicVolume()
    {
        if (musicSlider != null)
        {
            SetMusicVolume(musicSlider.value);
            PlayerPrefs.SetFloat("musicVolume", musicSlider.value);
        }
    }

    public void sfxVolume()
    {
        if (sfxSlider != null)
        {
            SetSFXVolume(sfxSlider.value);
            PlayerPrefs.SetFloat("sfxVolume", sfxSlider.value);
        }
    }

    void Update()
    {
        // --- Pause/Resume audio based on Time.timeScale ---
        bool isPaused = Time.timeScale == 0f;

        // Pause music source when game is paused
        if (musicSource != null)
        {
            if (isPaused && musicSource.isPlaying)
                musicSource.Pause();
            else if (!isPaused && !musicSource.isPlaying && musicSource.clip != null)
                musicSource.UnPause();
        }

        // Pause all loop SFX when game is paused
        // (LoopSegment coroutine also handles this, but this is a safety net)
        for (int i = activeLoopSFX.Count - 1; i >= 0; i--)
        {
            AudioSource src = activeLoopSFX[i];
            if (src == null)
            {
                activeLoopSFX.RemoveAt(i);
                continue;
            }

            if (isPaused && src.isPlaying)
                src.Pause();
            else if (!isPaused && !src.isPlaying)
                src.UnPause();
        }

        // --- Volume sync ---
        if (PlayerPrefs.HasKey("musicVolume") && musicSource != null)
        {
            float musicVolume = PlayerPrefs.GetFloat("musicVolume");
            musicSource.volume = musicVolume;
            if (musicSlider != null)
            {
                musicSlider.value = musicVolume;
            }
        }
        if (PlayerPrefs.HasKey("sfxVolume") && sfxSource != null)
        {
            float sfxVolume = PlayerPrefs.GetFloat("sfxVolume");
            sfxSource.volume = sfxVolume;
            if (sfxSlider != null)
            {
                sfxSlider.value = sfxVolume;
            }
        }
    }
}