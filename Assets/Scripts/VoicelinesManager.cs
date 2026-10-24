using UnityEngine;
using VocaloidTCG;
using System;

public enum VoicelineEvent
{
    Start,
    Victory,
    Defeat,
    Excited,
    Concerned,
    Win_Tile,
    Lose_Tile,
    Idle,
    Multiplayer
}

public class VoicelinesManager : MonoBehaviour
{
    //public AudioClip[] start;
    public AudioClip[] victory;
    public AudioClip[] defeat;
    public AudioClip[] concerned;
    public AudioClip[] excited;

    public AudioClip[] kaito_win_tile;
    public AudioClip[] len_win_tile;
    public AudioClip[] luka_win_tile;
    public AudioClip[] meiko_win_tile;
    public AudioClip[] miku_win_tile;
    public AudioClip[] rin_win_tile;

    public AudioClip[] kaito_lose_tile;
    public AudioClip[] len_lose_tile;
    public AudioClip[] luka_lose_tile;
    public AudioClip[] meiko_lose_tile;
    public AudioClip[] miku_lose_tile;
    public AudioClip[] rin_lose_tile;

    public AudioClip[] kaito_idle;
    public AudioClip[] len_idle;
    public AudioClip[] luka_idle;
    public AudioClip[] meiko_idle;
    public AudioClip[] miku_idle;
    public AudioClip[] rin_idle;

    public AudioClip[] start;
    public AudioClip[] multiplayer;
    private AudioSource audioSource;

    private static readonly int[,] startClipTable =
    {
    // kaito, len, luka, meiko, miku, rin
    {  0,  1,  2,  3,  4,  5 }, // kaito
    {  1,  6,  7,  8,  9, 10 }, // len
    {  2,  7, 11, 12, 13, 14 }, // luka
    {  3,  8, 12, 15, 16, 17 }, // meiko
    {  4,  9, 13, 16, 18, 19 }, // miku
    {  5, 10, 14, 17, 19, 20 }  // rin
    };

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

    public void PlayStoryReaction(AudioClip voiceline)
    {
        audioSource.Stop();

        if (voiceline)
        {
            audioSource.PlayOneShot(voiceline);
        }
    }
    public void PlayVoiceline( CharacterClass character, VoicelineEvent voiceline_event) {

        AudioClip voiceline;
        int character_index = GetCharacterIndex(character);
        
        if (!audioSource || character_index < 0 || character_index >= 6)
            return;

        if (voiceline_event == VoicelineEvent.Win_Tile || voiceline_event == VoicelineEvent.Lose_Tile)
        {
            AudioClip[] voicelines = GetCharacterArray(character, voiceline_event);
            int random_index = UnityEngine.Random.Range(0, 3);
            voiceline = voicelines[random_index];
        }
        else
        {
            AudioClip[] voicelines = GetVoicelines(voiceline_event);
            if (voicelines == null || character_index >= voicelines.Length)
                return;
            voiceline = voicelines[character_index];
        }

        if (voiceline)
        {
            audioSource.PlayOneShot(voiceline);
        }

    }

    public AudioClip GetVoiceline(CharacterClass character, VoicelineEvent reaction)
    {
        int index = GetCharacterIndex(character);
        var clips = GetVoicelines(reaction);
        return clips != null && index >= 0 && index < clips.Length ? clips[index] : null;
    }

    public void PlayStartVoicelines(CharacterClass player, CharacterClass opponent)
    {
        int playerIndex = GetCharacterIndex(player);
        int opponentIndex = GetCharacterIndex(opponent);

        if (playerIndex < 0 || opponentIndex < 0)
            return;

        int clipIndex = startClipTable[playerIndex, opponentIndex];

        if (clipIndex < 0)
            return;

        audioSource.PlayOneShot(start[clipIndex]);
    }


    public bool IsVoicelinePlaying()
    {
        return audioSource.isPlaying;
    }

    private AudioClip[] GetVoicelines(VoicelineEvent voiceline_event)
    {
        switch (voiceline_event)
        {
            case VoicelineEvent.Start: return start;
            case VoicelineEvent.Victory: return victory;
            case VoicelineEvent.Defeat: return defeat;
            case VoicelineEvent.Excited: return excited;
            case VoicelineEvent.Concerned: return concerned;
            case VoicelineEvent.Multiplayer: return multiplayer;
            default: return null;
        }
    }

    // characters in alpha order by first name
    // 0 = kaito
    // 1 = len
    // 2 = luka
    // 3 = meiko
    // 4 = miku
    // 5 = rin

    private int GetCharacterIndex(CharacterClass character)
    {
        switch (character)
        {
            case CharacterClass.KAITO: return 0;
            case CharacterClass.KagamineLen: return 1;
            case CharacterClass.MegurineLuka: return 2;
            case CharacterClass.MEIKO: return 3;
            case CharacterClass.HatsuneMiku: return 4;
            case CharacterClass.KagamineRin: return 5;
            default: return -1;
        }
    }

    private AudioClip[] GetCharacterArray(CharacterClass character, VoicelineEvent voiceline_event)
    {
        if (voiceline_event == VoicelineEvent.Win_Tile)
        {
            switch (character)
            {
                case CharacterClass.KAITO: return kaito_win_tile;
                case CharacterClass.KagamineLen: return len_win_tile;
                case CharacterClass.MegurineLuka: return luka_win_tile;
                case CharacterClass.MEIKO: return meiko_win_tile;
                case CharacterClass.HatsuneMiku: return miku_win_tile;
                case CharacterClass.KagamineRin: return rin_win_tile;
                default:  return null;
            }
        }
        else if (voiceline_event == VoicelineEvent.Lose_Tile)
        {
            switch (character)
            {
                case CharacterClass.KAITO: return kaito_lose_tile;
                case CharacterClass.KagamineLen: return len_lose_tile;
                case CharacterClass.MegurineLuka: return luka_lose_tile;
                case CharacterClass.MEIKO: return meiko_lose_tile;
                case CharacterClass.HatsuneMiku: return miku_lose_tile;
                case CharacterClass.KagamineRin: return rin_lose_tile;
                default: return null;
            }
        }
        else if (voiceline_event == VoicelineEvent.Idle)
        {
            switch (character)
            {
                case CharacterClass.KAITO: return kaito_idle;
                case CharacterClass.KagamineLen: return len_idle;
                case CharacterClass.MegurineLuka: return luka_idle;
                case CharacterClass.MEIKO: return meiko_idle;
                case CharacterClass.HatsuneMiku: return miku_idle;
                case CharacterClass.KagamineRin: return rin_idle;
                default: return null;
            }
        }
        else
            return null;
    }
}
