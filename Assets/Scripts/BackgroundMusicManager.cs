using UnityEngine;
using UnityEngine.SceneManagement;

public class BackgroundMusicManager : MonoBehaviour
{
    public static BackgroundMusicManager Instance { get; private set; }

    [System.Serializable]
    public class BGM
    {
        public string scene;
        public AudioClip clip;
    }

    public BGM[] bgmList;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float musicVolume = 0.5f;

    private AudioSource audioSource;
    private AudioClip currentClip;
    private VocaloidTCG.AudioCategorySource channel;
    public AudioSource PlaybackSource => audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.Stop();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = musicVolume;
        audioSource.spatialBlend = 0f;
        channel = GetComponent<VocaloidTCG.AudioCategorySource>();
        if (!channel) channel = gameObject.AddComponent<VocaloidTCG.AudioCategorySource>();
        channel.SetCategory(VocaloidTCG.AudioCategory.Music);
        channel.SetBaseVolume(musicVolume);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        PlaySceneMusic(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlaySceneMusic(scene.name);
    }

    private void PlaySceneMusic(string sceneName)
    {
        if (bgmList == null) return;
        foreach (BGM entry in bgmList)
        {
            if (entry == null || entry.scene != sceneName) continue;

            if (entry.clip == null) return;

            if (entry.clip == currentClip && audioSource.isPlaying) return;

            currentClip = entry.clip;
            audioSource.clip = currentClip;
            channel.SetBaseVolume(musicVolume);
            audioSource.Play();
            return;
        }
    }

    private void Update()
    {
        if (channel) channel.SetBaseVolume(musicVolume);
    }

    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);

        if (channel) channel.SetBaseVolume(musicVolume);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }
}
