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
        if(!audioSource) return;
        audioSource.Stop();

        if (voiceline)
        {
            audioSource.PlayOneShot(voiceline);
        }
    }
    public AudioClip PlayVoiceline(CharacterClass character, VoicelineEvent reaction){
        return PlayVoiceline(character,reaction,out _);
    }
    public AudioClip PlayVoiceline(CharacterClass character, VoicelineEvent reaction,out int clipIndex){
        clipIndex=-1;
        if(!audioSource || !TryGetVoiceline(character,reaction,out var clip,out clipIndex)) return null;
        audioSource.Stop();
        audioSource.PlayOneShot(clip);
        return clip;
    }
    public bool TryGetVoiceline(CharacterClass character,VoicelineEvent reaction,out AudioClip clip,out int clipIndex){
        clip=null; clipIndex=-1;
        int characterIndex=GetCharacterIndex(character);
        if(characterIndex<0) return false;
        bool variant=reaction==VoicelineEvent.Idle || reaction==VoicelineEvent.Win_Tile || reaction==VoicelineEvent.Lose_Tile;
        var clips=variant ? GetCharacterArray(character,reaction) : GetVoicelines(reaction);
        if(clips==null || clips.Length==0) return false;
        if(variant){
            // Select once from populated slots; the same index drives the subtitle and duration.
            int count=0; foreach(var candidate in clips) if(candidate) count++;
            if(count==0) return false;
            int pick=UnityEngine.Random.Range(0,count);
            for(int i=0;i<clips.Length;i++) if(clips[i] && pick--==0){ clipIndex=i; break; }
        }else clipIndex=characterIndex;
        if(clipIndex<0 || clipIndex>=clips.Length || !clips[clipIndex]) return false;
        clip=clips[clipIndex]; return true;
    }

    public AudioClip GetVoiceline(CharacterClass character, VoicelineEvent reaction)
    {
        var variants=GetCharacterArray(character,reaction);
        if(variants!=null) return System.Array.Find(variants,c=>c);
        int index = GetCharacterIndex(character);
        var clips = GetVoicelines(reaction);
        return clips != null && index >= 0 && index < clips.Length ? clips[index] : null;
    }

    public void PlayStartVoicelines(CharacterClass player, CharacterClass opponent)
    {
        int playerIndex = GetCharacterIndex(player);
        int opponentIndex = GetCharacterIndex(opponent);

        if (!audioSource || playerIndex < 0 || opponentIndex < 0)
            return;

        int clipIndex = startClipTable[playerIndex, opponentIndex];

        if (start == null || clipIndex < 0 || clipIndex >= start.Length || !start[clipIndex])
            return;

        audioSource.PlayOneShot(start[clipIndex]);
    }


    public bool IsVoicelinePlaying()
    {
        return audioSource && audioSource.isPlaying;
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
