namespace DesiLudo3D.Core
{
    /// <summary>Character categories (owner amendment A2/A3): the player
    /// profile — NEVER the token color — determines voice and dialogue pools.
    /// Male -> male voice/pools, Female -> female, Child -> child-appropriate.</summary>
    public enum CharacterCategory
    {
        Male = 0,
        Female = 1,
        Child = 2,
    }

    /// <summary>Personality styles (spec §6.2): bias pool selection and add
    /// variant lines so characters don't all sound identical.</summary>
    public enum PersonalityStyle
    {
        Balanced = 0,
        Overconfident,      // "Mera raasta, meri marzi!"
        DramaKing,          // maximal drama on every event
        SweetButDeadly,     // polite words, ruthless captures
        Excited,            // child-friendly high energy
    }

    /// <summary>Pure identity data shared by presentation, the comedy
    /// director (M3) and the voice system (M4). Kept in Core (no engine refs)
    /// so the online host can replicate it (M6 profile sync).</summary>
    public sealed class PlayerIdentity
    {
        public string Name = "Player";
        public CharacterCategory Category = CharacterCategory.Male;
        public PersonalityStyle Personality = PersonalityStyle.Balanced;
        public string VoiceProfileId = "";      // A2: selected by profile; "" = category default
        public bool ReactionsEnabled = true;    // R1 clips + comedy for this profile

        public string CategoryId()
        {
            switch (Category)
            {
                case CharacterCategory.Female: return "female";
                case CharacterCategory.Child: return "child";
                default: return "male";
            }
        }

        public static CharacterCategory NextCategory(CharacterCategory current)
        {
            return (CharacterCategory)(((int)current + 1) % 3);
        }
    }
}
