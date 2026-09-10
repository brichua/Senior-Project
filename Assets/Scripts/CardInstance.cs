using System;

namespace VocaloidTCG
{
    [Serializable]
    public sealed class CardInstance
    {
        public CardData Data { get; }
        public Side Owner { get; }
        public int EffectModifier { get; set; }
        public int CurrentInfluence => Math.Max(0, Data.influence + EffectModifier);

        public CardInstance(CardData data, Side owner)
        {
            Data = data;
            Owner = owner;
        }
    }

    public enum Side
    {
        Player,
        Enemy
    }
}
