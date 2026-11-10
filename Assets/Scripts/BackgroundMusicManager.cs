using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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

    [Min(0f)]
    public float fadeOutDuration = 1f;
    [Min(0f)]
    public float fadeInDuration = 1f;
    public float musicVolume = 0.5f;

    private AudioSource audioSource;
    private AudioClip currentClip;
    private VocaloidTCG.AudioCategorySource channel;
    private Coroutine musicTransition;
    private float transitionVolume = 1f;
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
            if (musicTransition != null) StopCoroutine(musicTransition);
            musicTransition = StartCoroutine( ChangeMusic(entry.clip) );
            return;
        }
    }

    private IEnumerator ChangeMusic(AudioClip newClip)
    {
        float elapsed = 0f;
        float startingVolume = transitionVolume;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            transitionVolume = Mathf.Lerp(startingVolume, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        transitionVolume = 0f;

        audioSource.Stop();
        audioSource.clip = newClip;
        currentClip = newClip;

        transitionVolume = 1f;
        audioSource.Play();

        if (channel) channel.SetBaseVolume(musicVolume);

        musicTransition = null;
    }

    private void Update()
    {
        if (channel) channel.SetBaseVolume(musicVolume * transitionVolume);
    }

    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (channel) channel.SetBaseVolume(musicVolume * transitionVolume);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }
}
