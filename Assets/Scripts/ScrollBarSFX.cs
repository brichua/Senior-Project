using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ScrollbarSFX : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    private Scrollbar scrollbar;
    private AudioSource audioSource;

    public float soundCooldown = 0.15f;
    private float nextSoundTime = 0f;
    private bool isInteracting = false;

    private void Awake()
    {
        scrollbar = GetComponent<Scrollbar>();
        audioSource = GetComponent<AudioSource>();

        scrollbar.onValueChanged.AddListener(PlayScrollSound);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isInteracting = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isInteracting = false;
    }

    private void PlayScrollSound(float value)
    {
        if (!isInteracting)
            return;

        if (Time.time < nextSoundTime)
            return;

        if (scrollbar.value > 1 || scrollbar.value < 0)
            return;

        audioSource.Play();
        nextSoundTime = Time.time + soundCooldown;
    }

}