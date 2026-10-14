using UnityEngine;
using VocaloidTCG;

public enum VoicelineEvent
{
    Start,
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
    public AudioClip[] start;
    public AudioClip[] victory;
    public AudioClip[] defeat;
    public AudioClip[] concerned;
    public AudioClip[] excited;
    public AudioClip[] win_tile;
    public AudioClip[] lose_tile;
    public AudioClip[] idle;
    private AudioSource audioSource;

    private void Awake()
    {

        audioSource = GetComponent<AudioSource>();
        if (audioSource)
        {
            var channel = GetComponent<VocaloidTCG.AudioCategorySource>();
            if (!channel) channel = gameObject.AddComponent<VocaloidTCG.AudioCategorySource>();
            channel.SetCategory(VocaloidTCG.AudioCategory.Voices);
        }
    }

    public void PlayVoiceline( CharacterClass character, VoicelineEvent voiceline_event) {

        int character_index = GetCharacterIndex(character);
        
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

    public AudioClip GetVoiceline(CharacterClass character, VoicelineEvent reaction){
        int index = GetCharacterIndex(character);
        var clips = GetVoicelines(reaction);
        return clips != null && index >= 0 && index < clips.Length ? clips[index] : null;
    }

    private AudioClip[] GetVoicelines(
            VoicelineEvent voiceline_event)
    {
        switch (voiceline_event)
        {
            case VoicelineEvent.Start:
                return start;

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

    // characters in alpha order by first name
    // 0 = kaito
    // 1 = len
    // 2 = luka
    // 3 = meiko
    // 4 = miku
    // 5 = rin

    private int GetCharacterIndex(
            CharacterClass character)
    {
        switch (character)
        {
            case CharacterClass.KAITO:
                return 0;

            case CharacterClass.KagamineLen:
                return 1;

            case CharacterClass.MegurineLuka:
                return 2;

            case CharacterClass.MEIKO:
                return 3;

            case CharacterClass.HatsuneMiku:
                return 4;

            case CharacterClass.KagamineRin:
                return 5;

            default:
                return -1;
        }
    }
}
