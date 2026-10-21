using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Coup.Game;

namespace Coup.EditorTools
{
    /// <summary>
    /// Builds the CoupAudio library from whatever is sitting in Assets/Audio. A clip
    /// is claimed by a cue through its file name — Coup.ogg, or CardDeal_1.ogg for one
    /// of several takes — so swapping a sound is a matter of dropping a file in and
    /// running this again. Nothing here authors audio; the clips are the source.
    ///
    /// It also sets each cue's gain, by measuring the clip rather than guessing. A
    /// library assembled from several packs arrives at wildly different levels, and
    /// that reads as a bad mix however good the individual sounds are.
    /// </summary>
    public static class CoupAudioLibraryBuilder
    {
        private const string SfxDir = "Assets/Audio/SFX";
        private const string MusicDir = "Assets/Audio/Music";
        private const string LibraryPath = "Assets/Resources/CoupAudio.asset";

        /// <summary>The window loudness is measured over. Shorter than most cues, so a
        /// long quiet tail does not make a sharp sound read as a soft one.</summary>
        private const float LoudnessWindowSeconds = 0.3f;

        /// <summary>
        /// How loud each cue should sit, as the RMS of its loudest moment. These are
        /// the mix: a bot's turn barely registers, a coup lands hard.
        /// </summary>
        private static float TargetLoudness(Cue cue) => cue switch
        {
            Cue.TurnOther => 0.030f,
            Cue.UiClick or Cue.UiBack => 0.060f,
            Cue.UiDenied => 0.070f,
            Cue.CardDeal or Cue.CardFlip or Cue.Shuffle => 0.085f,
            Cue.TurnYours => 0.090f,
            Cue.Income or Cue.ForeignAid or Cue.CoinGain or Cue.CoinPay => 0.100f,
            Cue.Tax or Cue.Steal or Cue.Exchange => 0.110f,
            Cue.CardSlam or Cue.Assassinate or Cue.Block => 0.135f,
            Cue.Challenge or Cue.ChallengeWon or Cue.ChallengeLost => 0.155f,
            Cue.InfluenceLost => 0.165f,
            Cue.Victory or Cue.Defeat => 0.180f,
            Cue.Coup or Cue.Eliminated => 0.200f,
            _ => 0.100f
        };

        /// <summary>
        /// Semitones of random detune. Only the cues that fire in bursts get any — a
        /// repeated card or coin is what gives away a sample library. Anything with a
        /// tune in it gets none, because detuning music just sounds broken.
        /// </summary>
        private static float PitchSpread(Cue cue) => cue switch
        {
            Cue.CardDeal or Cue.CardSlam => 1.1f,
            Cue.CardFlip or Cue.CoinGain or Cue.CoinPay => 0.9f,
            Cue.UiClick or Cue.Shuffle => 0.6f,
            Cue.Income or Cue.ForeignAid or Cue.Steal => 0.5f,
            _ => 0f
        };

        [MenuItem("Coup/Rebuild Audio Library", priority = 30)]
        public static void Rebuild()
        {
            var library = AssetDatabase.LoadAssetAtPath<CoupAudio>(LibraryPath);
            if (library == null)
            {
                Directory.CreateDirectory("Assets/Resources");
                library = ScriptableObject.CreateInstance<CoupAudio>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var byCue = new Dictionary<Cue, List<AudioClip>>();
            var unclaimed = new List<string>();

            foreach (var path in ClipPaths(SfxDir))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var cut = name.LastIndexOf('_');
                var stem = cut > 0 && int.TryParse(name[(cut + 1)..], out _) ? name[..cut] : name;

                if (!Enum.TryParse<Cue>(stem, out var cue) || cue == Cue.None)
                {
                    unclaimed.Add(name);
                    continue;
                }

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                if (!byCue.TryGetValue(cue, out var list)) byCue[cue] = list = new List<AudioClip>();
                list.Add(clip);
            }

            var entries = new List<CoupAudio.CueClip>();
            var peaks = new List<float>();
            var report = new List<string>();

            foreach (var pair in byCue.OrderBy(pair => (int)pair.Key))
            {
                var takes = pair.Value.OrderBy(clip => clip.name).ToArray();
                var loudness = takes.Select(Loudness).Where(value => value > 0.0001f).ToArray();
                var measured = loudness.Length > 0 ? loudness.Average() : 0.1f;

                var gain = Mathf.Clamp(TargetLoudness(pair.Key) / measured, 0.05f, 4f);
                peaks.Add(takes.Select(Peak).DefaultIfEmpty(1f).Max());

                entries.Add(new CoupAudio.CueClip
                {
                    cue = pair.Key,
                    takes = takes,
                    gain = gain,
                    pitchSpread = PitchSpread(pair.Key)
                });
                report.Add($"{pair.Key} x{takes.Length}  measured {measured:F3}");
            }

            // Trim the whole set by one factor rather than clamping each cue on its
            // own. Clamping per cue silently flattens the mix — the loud sounds hit
            // the ceiling first and end up no louder than the quiet ones, which is
            // the thing these targets exist to prevent.
            var hottest = 0f;
            for (var i = 0; i < entries.Count; i++)
                hottest = Mathf.Max(hottest, entries[i].gain * peaks[i]);
            var trim = hottest > 1f ? 1f / hottest : 1f;
            for (var i = 0; i < entries.Count; i++)
            {
                entries[i].gain *= trim;
                report[i] += $" -> gain {entries[i].gain:F2}";
            }
            report.Add($"set trimmed by x{trim:F2} so the hottest cue peaks at 1.0");

            library.cues = entries.ToArray();
            library.lobbyTheme = FirstClip(MusicDir, "LobbyTheme");
            library.tableTheme = FirstClip(MusicDir, "TableTheme");

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            var missing = Enum.GetValues(typeof(Cue)).Cast<Cue>()
                .Where(cue => cue != Cue.None && !byCue.ContainsKey(cue)).ToArray();

            Debug.Log($"[Coup] Audio library: {entries.Count} cues, "
                    + $"lobby={(library.lobbyTheme != null ? library.lobbyTheme.name : "none")}, "
                    + $"table={(library.tableTheme != null ? library.tableTheme.name : "none")}\n"
                    + string.Join("\n", report));

            if (missing.Length > 0)
                Debug.LogWarning($"[Coup] Cues with no clip: {string.Join(", ", missing)}");
            if (unclaimed.Count > 0)
                Debug.LogWarning($"[Coup] Files in {SfxDir} that match no cue name: {string.Join(", ", unclaimed)}");
        }

        private static IEnumerable<string> ClipPaths(string dir) =>
            AssetDatabase.FindAssets("t:AudioClip", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path);

        private static AudioClip FirstClip(string dir, string stem) =>
            ClipPaths(dir)
                .Where(path => Path.GetFileNameWithoutExtension(path) == stem)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .FirstOrDefault();

        /// <summary>RMS of the clip's loudest window — what the ear actually judges.</summary>
        private static float Loudness(AudioClip clip)
        {
            var samples = Read(clip);
            if (samples == null || samples.Length == 0) return 0f;

            var window = Mathf.Min(samples.Length, Mathf.Max(1, (int)(LoudnessWindowSeconds * clip.frequency) * clip.channels));
            var sum = 0.0;
            for (var i = 0; i < window; i++) sum += samples[i] * (double)samples[i];

            var best = sum;
            for (var i = window; i < samples.Length; i++)
            {
                sum += samples[i] * (double)samples[i] - samples[i - window] * (double)samples[i - window];
                if (sum > best) best = sum;
            }
            return Mathf.Sqrt((float)(best / window));
        }

        private static float Peak(AudioClip clip)
        {
            var samples = Read(clip);
            if (samples == null) return 1f;
            var peak = 0f;
            foreach (var sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            return peak;
        }

        private static readonly Dictionary<AudioClip, float[]> Cache = new();

        private static float[] Read(AudioClip clip)
        {
            if (Cache.TryGetValue(clip, out var cached)) return cached;
            float[] samples = null;
            if (clip.loadType != AudioClipLoadType.Streaming)
            {
                clip.LoadAudioData();
                samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0)) samples = null;
            }
            Cache[clip] = samples;
            return samples;
        }
    }
}
