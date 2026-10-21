using System;
using System.Linq;
using UnityEngine;

namespace Coup.Game
{
    /// <summary>
    /// Every clip in one asset, the way CoupArt holds every sprite. The director
    /// never hunts for files, and a cue can carry several takes so a sound that
    /// fires in bursts — coins, dealt cards — does not machine-gun one waveform.
    /// </summary>
    [CreateAssetMenu(fileName = "CoupAudio", menuName = "Coup/Audio Library")]
    public class CoupAudio : ScriptableObject
    {
        [Serializable]
        public class CueClip
        {
            public Cue cue;
            public AudioClip[] takes = Array.Empty<AudioClip>();

            /// <summary>Per-cue trim, so one loud sound is fixed here and not in every caller.</summary>
            [Range(0f, 2f)] public float gain = 1f;

            /// <summary>Random pitch spread, in semitones either side of centre.</summary>
            [Range(0f, 6f)] public float pitchSpread = 0.4f;
        }

        [Header("Cues")]
        public CueClip[] cues = Array.Empty<CueClip>();

        [Header("Music")]
        public AudioClip lobbyTheme;
        public AudioClip tableTheme;

        [Header("Mix")]
        [Range(0f, 1f)] public float sfxVolume = 0.85f;
        [Range(0f, 1f)] public float musicVolume = 0.34f;

        public CueClip For(Cue cue) => cues.FirstOrDefault(entry => entry.cue == cue);

        /// <summary>One take for the cue, chosen at random. Null when the cue has no art yet.</summary>
        public AudioClip Clip(Cue cue)
        {
            var entry = For(cue);
            if (entry == null || entry.takes == null || entry.takes.Length == 0) return null;
            if (entry.takes.Length == 1) return entry.takes[0];
            return entry.takes[UnityEngine.Random.Range(0, entry.takes.Length)];
        }
    }
}
