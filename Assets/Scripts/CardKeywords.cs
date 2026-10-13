using System.Collections.Generic;
using System.Linq;
using VocaloidTCG.BoardUI;

namespace VocaloidTCG
{
    public static class CardKeywords
    {
        private static readonly KeywordDefinition[] standard = {
            new KeywordDefinition { keyword = "stage share", description = "This performer can share a tile with one allied performer. Each performer can have only one stage-share partner." },
            new KeywordDefinition { keyword = "encore", description = "If this performer wins its Center Stage tile, gain 1 additional fan score." },
            new KeywordDefinition { keyword = "soloist", description = "Soloist X: while this is your only allied performer in Center Stage, it has +X influence." },
            new KeywordDefinition { keyword = "harmonize", description = "Harmonize X: while at least 2 allied performers occupy the 8 surrounding tiles, this performer has +X influence." },
            new KeywordDefinition { keyword = "nimble", description = "This performer can move once during Preparation without using your turn's normal performer movement." },
            new KeywordDefinition { keyword = "cheer effect", description = "This stage effect can also be played during Performance." },
            new KeywordDefinition { keyword = "venue effect", description = "This stage effect stays active for the rest of the game after it is played." }
        };

        public static KeywordDefinition[] Descriptions(KeywordDefinition[] custom){
            var result = new List<KeywordDefinition>(standard);
            if(custom != null) result.AddRange(custom.Where(d => d != null && !string.IsNullOrWhiteSpace(d.keyword) &&
                !standard.Any(s => string.Equals(s.keyword, d.keyword, System.StringComparison.OrdinalIgnoreCase))));
            return result.ToArray();
        }
    }
}
