using UnityEngine;

public enum VoicelineEvent
{
    Intro,
    Victory,
    Defeat,
    Excited,
    Concerned,
    Win_Tile,
    Lose_Tile,
    Idle
}

public class VoicelinesManager : MonoBehaviour
{
    public AudioClip[] intro;
    public AudioClip[] victory;
    public AudioClip[] defeat;
    public AudioClip[] concerned;
    public AudioClip[] excited;
    public AudioClip[] win_tile;
    public AudioClip[] lose_tile;
    public AudioClip[] idle;
    public AudioSource audioSource;

    public void PlayVoiceline( int character_index, VoicelineEvent voiceline_event) {
        
        if (!audioSource || character_index < 0 || character_index >= 6)
            return;

        AudioClip[] voicelines = GetVoicelines(voiceline_event);

        if (voicelines == null || character_index >= voicelines.Length)
            return;

        AudioClip voiceline = voicelines[character_index];

        if (voiceline)
        {
            audioSource.PlayOneShot(voiceline);
        }

    }

    private AudioClip[] GetVoicelines(
            VoicelineEvent voiceline_event)
    {
        switch (voiceline_event)
        {
            case VoicelineEvent.Intro:
                return intro;

            case VoicelineEvent.Victory:
                return victory;

            case VoicelineEvent.Defeat:
                return defeat;

            case VoicelineEvent.Excited:
                return excited;

            case VoicelineEvent.Concerned:
                return concerned;

            case VoicelineEvent.Win_Tile:
                return win_tile;

            case VoicelineEvent.Lose_Tile:
                return lose_tile;

            case VoicelineEvent.Idle:
                return idle;

            default:
                return null;
        }
    }
}
