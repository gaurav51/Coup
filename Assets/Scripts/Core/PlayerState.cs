using System.Collections.Generic;

namespace Coup.Core
{
    /// <summary>One seat at the table. Influence is the face-down hand; revealed cards are public.</summary>
    public class PlayerState
    {
        public int Index { get; }
        public string Name { get; internal set; }
        public int Coins { get; internal set; }

        private readonly List<Role> _influence = new();
        private readonly List<Role> _revealed = new();

        /// <summary>Face-down cards still in play. Only this player may see them.</summary>
        public IReadOnlyList<Role> Influence => _influence;

        /// <summary>Cards lost to challenges, assassinations and coups. Public knowledge.</summary>
        public IReadOnlyList<Role> Revealed => _revealed;

        public bool IsAlive => _influence.Count > 0;
        public int InfluenceCount => _influence.Count;

        /// <summary>A player holding 10 or more coins must Coup on their turn.</summary>
        public bool MustCoup => Coins >= Rules.ForcedCoupThreshold;

        public PlayerState(int index, string name)
        {
            Index = index;
            Name = name;
        }

        public bool HasRole(Role role) => _influence.Contains(role);

        internal void GainCard(Role role) => _influence.Add(role);

        internal void ClearInfluence() => _influence.Clear();

        /// <summary>Replaces the hand and the public pile. Used when a server snapshot arrives.</summary>
        internal void Show(IReadOnlyList<Role> hidden, IReadOnlyList<Role> revealed)
        {
            _influence.Clear();
            _revealed.Clear();
            if (hidden != null)
                for (var i = 0; i < hidden.Count; i++) _influence.Add(hidden[i]);
            if (revealed != null)
                for (var i = 0; i < revealed.Count; i++) _revealed.Add(revealed[i]);
        }

        /// <summary>Moves a card from the hand to the revealed pile. Returns false if not held.</summary>
        internal bool RevealCard(Role role)
        {
            if (!_influence.Remove(role)) return false;
            _revealed.Add(role);
            return true;
        }

        /// <summary>Takes a specific card back out of the hand without revealing it (challenge redraw).</summary>
        internal bool TakeCard(Role role) => _influence.Remove(role);
    }
}
