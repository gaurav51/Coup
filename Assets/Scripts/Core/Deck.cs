using System;
using System.Collections.Generic;

namespace Coup.Core
{
    /// <summary>
    /// The draw pile. Seeded so games replay identically, which keeps the tests
    /// deterministic and lets us reproduce an AI's decision from a bug report.
    /// </summary>
    public class Deck
    {
        private readonly List<Role> _cards = new();
        private readonly Random _rng;

        public int Count => _cards.Count;

        public int CountOf(Role role)
        {
            var n = 0;
            for (var i = 0; i < _cards.Count; i++)
                if (_cards[i] == role) n++;
            return n;
        }

        public Deck(int seed)
        {
            _rng = new Random(seed);
            foreach (var role in Rules.AllRoles)
                for (var i = 0; i < Rules.CopiesPerRole; i++)
                    _cards.Add(role);
            Shuffle();
        }

        public void Shuffle()
        {
            for (var i = _cards.Count - 1; i > 0; i--)
            {
                var j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        public Role Draw()
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("Draw from an empty deck.");
            var last = _cards.Count - 1;
            var card = _cards[last];
            _cards.RemoveAt(last);
            return card;
        }

        /// <summary>Returns a card to the deck and reshuffles, as a successful challenge requires.</summary>
        public void ReturnAndShuffle(Role role)
        {
            _cards.Add(role);
            Shuffle();
        }
    }
}
