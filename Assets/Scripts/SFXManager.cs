using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public AudioClip card_hover;
    public AudioClip card_click;
    public AudioClip card_drop;
    public AudioClip ticket_tear;
    public AudioClip end_turn_click;
    public AudioClip tile_hover;

    private AudioSource audioSource;

    private void Awake() {

        audioSource = GetComponent<AudioSource>();
    }

    public void PlayHoverSound() {

        if (audioSource && card_hover)
            audioSource.PlayOneShot(card_hover);
    }

    public void PlayClickSound() {

        if (audioSource && card_click)
            audioSource.PlayOneShot(card_click);
    }

    public void PlayDropSound() {

        if (audioSource && card_drop)
            audioSource.PlayOneShot(card_drop);
    }

    public void PlayTicketTearSound() {

        if (audioSource && ticket_tear)
            audioSource.PlayOneShot(ticket_tear);
    }

    public void PlayEndTurnSound() {

        if (audioSource && end_turn_click)
            audioSource.PlayOneShot(end_turn_click);
    }

    public void PlayTileHoverSound() {

        if (audioSource && tile_hover)
            audioSource.PlayOneShot(tile_hover);
    }

    public float GetTicketTearSoundLength()
    {
        return ticket_tear.length;
    }

}
