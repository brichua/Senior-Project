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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = musicVolume;
        audioSource.spatialBlend = 0f;

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
        foreach (BGM entry in bgmList)
        {
            if (entry.scene != sceneName) continue;

            if (entry.clip == null) return;

            if (entry.clip == currentClip && audioSource.isPlaying) return;

            currentClip = entry.clip;
            audioSource.clip = currentClip;
            audioSource.volume = musicVolume;
            audioSource.Play();
            return;
        }
    }

    private void Update()
    {
        if (audioSource != null) audioSource.volume = musicVolume;
    }

    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);

        if (audioSource != null) audioSource.volume = musicVolume;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }
}