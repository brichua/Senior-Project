using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public AudioClip card_hover;
    public AudioClip card_click;
    public AudioClip card_drop;
    public AudioClip ticket_tear;
    public AudioClip end_turn_click;
    public AudioClip tile_hover;
    public AudioClip card_draw;
    public AudioClip card_destroy;
    public AudioClip fav_deck;
    public AudioClip unfav_deck;
    public AudioClip button_click_1;
    public AudioClip button_click_2;
    public AudioClip adjust_card_num_0;
    public AudioClip adjust_card_num_1;
    public AudioClip adjust_card_num_2;
    public AudioClip adjust_card_num_3;
    public AudioClip typing;
    public AudioClip class_select;
    

    private AudioSource audioSource;

    private void Awake() {

        audioSource = GetComponent<AudioSource>();
        if (audioSource)
        {
            var channel = GetComponent<VocaloidTCG.AudioCategorySource>();
            if (!channel) channel = gameObject.AddComponent<VocaloidTCG.AudioCategorySource>();
            channel.SetCategory(VocaloidTCG.AudioCategory.Effects);
        }
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

    public void PlayCardSound(AudioClip clip)
    {
        if (audioSource && clip)
            audioSource.PlayOneShot(clip);
    }

    public void PlayCardDrawSound()
    {
        if (audioSource && card_draw)
            audioSource.PlayOneShot(card_draw);
    }

    public void PlayDestroySound()
    {
        if (audioSource && card_destroy)
            audioSource.PlayOneShot(card_destroy);
    }

    public void PlayFavDeckSound()
    {
        if (audioSource && fav_deck)
            audioSource.PlayOneShot(fav_deck);
    }

    public void PlayUnfavDeckSound()
    {
        if (audioSource && unfav_deck)
            audioSource.PlayOneShot(unfav_deck);
    }

    public void PlayButtonClick1()
    {
        if (audioSource && button_click_1)
            audioSource.PlayOneShot(button_click_1);
    }

    public void PlayButtonClick2()
    {
        if (audioSource && button_click_2)
            audioSource.PlayOneShot(button_click_2);
    }

    public void PlayAdjustCardNumTo0()
    {
        if (audioSource && adjust_card_num_0)
            audioSource.PlayOneShot(adjust_card_num_0);
    }
    public void PlayAdjustCardNumTo1()
    {
        if (audioSource && adjust_card_num_1)
            audioSource.PlayOneShot(adjust_card_num_1);
    }
    public void PlayAdjustCardNumTo2()
    {
        if (audioSource && adjust_card_num_2)
            audioSource.PlayOneShot(adjust_card_num_2);
    }
    public void PlayAdjustCardNumTo3()
    {
        if (audioSource && adjust_card_num_3)
            audioSource.PlayOneShot(adjust_card_num_3);
    }
    public void PlayTypingSound()
    {
        if (audioSource && typing)
            audioSource.PlayOneShot(typing);
    }
    public void PlayClassSelectSound()
    {
        if (audioSource && class_select)
            audioSource.PlayOneShot(class_select);
    }
}
