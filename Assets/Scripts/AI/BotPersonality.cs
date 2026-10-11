namespace Coup.AI
{
    /// <summary>
    /// Dials that give each seat a recognisable table presence. A bot that never
    /// bluffs and never challenges makes Coup boring within two rounds, so the
    /// presets deliberately spread across both axes.
    /// </summary>
    public class BotPersonality
    {
        public string Name { get; set; } = "Bot";

        /// <summary>How readily it spends coins on Coup and picks on the leader. 0..1</summary>
        public float Aggression { get; set; } = 0.5f;

        /// <summary>How often it claims a role it does not hold. 0..1</summary>
        public float BluffChance { get; set; } = 0.3f;

        /// <summary>Shifts the probability threshold at which it calls a bluff. 0..1</summary>
        public float Suspicion { get; set; } = 0.5f;

        /// <summary>How willing it is to claim a blocking role it does not hold. 0..1</summary>
        public float BlockBluffChance { get; set; } = 0.25f;

        public static BotPersonality Cautious => new BotPersonality
        {
            Name = "Cautious",
            Aggression = 0.3f,
            BluffChance = 0.12f,
            Suspicion = 0.35f,
            BlockBluffChance = 0.15f
        };

        public static BotPersonality Opportunist => new BotPersonality
        {
            Name = "Opportunist",
            Aggression = 0.55f,
            BluffChance = 0.35f,
            Suspicion = 0.55f,
            BlockBluffChance = 0.3f
        };

        public static BotPersonality Ruthless => new BotPersonality
        {
            Name = "Ruthless",
            Aggression = 0.85f,
            BluffChance = 0.6f,
            Suspicion = 0.75f,
            BlockBluffChance = 0.45f
        };

        public static BotPersonality[] Presets => new[] { Cautious, Opportunist, Ruthless };
    }
}
