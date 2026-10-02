using UnityEngine;
using UnityEngine.UI;

public class ScrollbarSFX : MonoBehaviour
{
    private Scrollbar scrollbar;
    private AudioSource audioSource;

    public float soundCooldown = 0.15f;
    private float nextSoundTime = 0f;

    private void Awake()
    {
        scrollbar = GetComponent<Scrollbar>();
        audioSource = GetComponent<AudioSource>();

        scrollbar.onValueChanged.AddListener(PlayScrollSound);
    }

    private void PlayScrollSound(float value)
    {
        if (Time.time < nextSoundTime)
            return;

        if (scrollbar.value > 1 || scrollbar.value < 0)
            return;

        audioSource.Play();
        nextSoundTime = Time.time + soundCooldown;
    }
}