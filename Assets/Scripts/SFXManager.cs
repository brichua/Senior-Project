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
    public AudioClip enter_game;
    public AudioClip crowd_noise;
    public AudioClip hud_score;
    public AudioClip clock_tick;
    public AudioClip shimmer;
    public AudioClip tile_score;
    public AudioClip player_highlight;
    public AudioClip spotlight;
    public AudioClip tile_score_0;
    public AudioClip card_effect;
    public AudioClip story_start;

    [Header("Hover SFX Cooldown")]
    [Min(0f)] public float hoverSoundCooldown = 0.05f;
    private static float nextHoverSoundTime = 0f;
    [Min(0f)] public float tileHoverSoundCooldown = 0.05f;
    private static float nextTileHoverSoundTime = 0f;


    private AudioSource audioSource;
    private AudioSource cardAudioSource;
    public bool cardAudioSourceIsPlaying;

    private void Awake() {

        audioSource = GetComponent<AudioSource>();
        if (audioSource)
        {
            cardAudioSource = gameObject.AddComponent<AudioSource>();
            cardAudioSource.playOnAwake = false;
            cardAudioSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            cardAudioSource.spatialBlend = audioSource.spatialBlend;
            cardAudioSource.priority = audioSource.priority;
            var channel = GetComponent<VocaloidTCG.AudioCategorySource>();
            if (!channel) channel = gameObject.AddComponent<VocaloidTCG.AudioCategorySource>();
            channel.SetCategory(VocaloidTCG.AudioCategory.Effects);
        }
    }

    public void PlayHoverSound()
    {
        if (Time.unscaledTime < nextHoverSoundTime)
            return;

        nextHoverSoundTime = Time.unscaledTime + hoverSoundCooldown;

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

    public void PlayTileHoverSound()
    {
        if (Time.unscaledTime < nextTileHoverSoundTime)
            return;

        nextTileHoverSoundTime = Time.unscaledTime + tileHoverSoundCooldown;

        if (audioSource && tile_hover)
            audioSource.PlayOneShot(tile_hover);
    }

    public float GetTicketTearSoundLength()
    {
        return ticket_tear.length;
    }

    public void PlayCardSound(AudioClip clip)
    {
        if (!cardAudioSource) return;
        cardAudioSource.Stop();
        cardAudioSource.clip = clip;
        SyncCardAudioSettings();
        if (clip) { cardAudioSourceIsPlaying = true;  cardAudioSource.Play(); }
        cardAudioSourceIsPlaying = false;
    }

    private void LateUpdate() { SyncCardAudioSettings(); }

    private void SyncCardAudioSettings()
    {
        if (!audioSource || !cardAudioSource) return;
        cardAudioSource.volume = audioSource.volume;
        cardAudioSource.mute = audioSource.mute;
        cardAudioSource.pitch = audioSource.pitch;
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
    public void PlayEnterGameSound()
    {
        if (audioSource && enter_game)
            audioSource.PlayOneShot(enter_game);
    }

    public void PlayCrowdSound()
    {
        if (audioSource && crowd_noise)
            audioSource.PlayOneShot(crowd_noise);
    }

    public void PlayHUDScore()
    {
        if (audioSource && hud_score)
            audioSource.PlayOneShot(hud_score);
    }

    public void PlayClockTick()
    {
        if (audioSource && clock_tick)
            audioSource.PlayOneShot(clock_tick);
    }

    public void PlayShimmerSound()
    {
        if (audioSource && shimmer)
            audioSource.PlayOneShot(shimmer);
    }

    public void PlayTileScore()
    {
        if (audioSource && tile_score)
            audioSource.PlayOneShot(tile_score);
    }

    public void PlayTileScore0()
    {
        if (audioSource && tile_score_0)
            audioSource.PlayOneShot(tile_score_0);
    }

    public void PlayPlayerHighlight()
    {
        if (audioSource && player_highlight)
            audioSource.PlayOneShot(player_highlight);
    }

    public void PlaySpotlightSound()
    {
        if (audioSource && spotlight)
            audioSource.PlayOneShot(spotlight);
    }

    public void PlayCardEffect()
    {
        if (audioSource && card_effect)
            audioSource.PlayOneShot(card_effect);
    }

    public void PlayStoryStart()
    {
        if (audioSource && story_start)
            audioSource.PlayOneShot(story_start);
    }
}
