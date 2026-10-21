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
            Cue.UiClick => 0.060f,
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
            Normalise();
            BuildLibrary();
        }

        /// <summary>
        /// Brings every cue clip to the level its moment deserves, by rewriting the
        /// audio rather than by turning a knob at play time. A gain alone cannot do
        /// it: Unity clamps a voice's volume at 1, so a quiet source can never be
        /// lifted to meet a loud one, and the mix collapses towards whatever the
        /// quietest file happened to be recorded at.
        /// </summary>
        private static void Normalise()
        {
            var written = 0;
            foreach (var path in ClipPaths(SfxDir).ToArray())
            {
                if (!TryCue(path, out var cue)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || clip.loadType == AudioClipLoadType.Streaming) continue;

                var samples = Read(clip);
                if (samples == null || samples.Length == 0) continue;

                var measured = WindowLoudness(samples, clip.frequency, clip.channels);
                if (measured < 0.0001f) continue;

                var gain = TargetLoudness(cue) / measured;
                var alreadyWav = Path.GetExtension(path) == ".wav";
                if (Mathf.Abs(gain - 1f) < 0.04f && alreadyWav) continue;

                // A clip already sitting at full scale cannot be made louder; the
                // soft knee would just absorb the gain. Boosting it again on every
                // run would soft-clip the same transients over and over, so a clip
                // that has nowhere left to go is left alone.
                var peak = 0f;
                foreach (var sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                if (gain > 1f && peak >= 0.95f && alreadyWav) continue;

                var shaped = new float[samples.Length];
                for (var i = 0; i < samples.Length; i++) shaped[i] = SoftClip(samples[i] * gain);

                var target = Path.ChangeExtension(path, ".wav");
                WriteWav(target, shaped, clip.channels, clip.frequency);
                if (target != path) AssetDatabase.DeleteAsset(path);
                written++;
            }

            Cache.Clear();
            if (written > 0) AssetDatabase.Refresh();
            Debug.Log($"[Coup] Normalised {written} cue clip(s) to their mix targets.");
        }

        /// <summary>
        /// Rounds off anything the gain pushed near full scale. Only the top of the
        /// range is touched, so a transient keeps its snap instead of being scaled
        /// back down — which would undo the levelling it was boosted for.
        /// </summary>
        private static float SoftClip(float x)
        {
            const float knee = 0.70f;
            var magnitude = Mathf.Abs(x);
            if (magnitude <= knee) return x;
            var over = (magnitude - knee) / (1f - knee);
            var shaped = knee + (1f - knee) * (float)System.Math.Tanh(over);
            return Mathf.Sign(x) * Mathf.Min(shaped, 0.995f);
        }

        private static void WriteWav(string assetPath, float[] samples, int channels, int rate)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var bytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + bytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(rate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(bytes);
            foreach (var sample in samples)
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 32767f));
            writer.Flush();

            var full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
            File.WriteAllBytes(full, stream.ToArray());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>A clip's file name decides which cue owns it: Coup.wav, CardDeal_1.wav.</summary>
        private static bool TryCue(string path, out Cue cue)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var cut = name.LastIndexOf('_');
            var stem = cut > 0 && int.TryParse(name[(cut + 1)..], out _) ? name[..cut] : name;
            return Enum.TryParse(stem, out cue) && cue != Cue.None;
        }

        private static void BuildLibrary()
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
                if (!TryCue(path, out var cue))
                {
                    unclaimed.Add(Path.GetFileNameWithoutExtension(path));
                    continue;
                }

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                if (!byCue.TryGetValue(cue, out var list)) byCue[cue] = list = new List<AudioClip>();
                list.Add(clip);
            }

            var entries = new List<CoupAudio.CueClip>();
            var report = new List<string>();

            foreach (var pair in byCue.OrderBy(pair => (int)pair.Key))
            {
                var takes = pair.Value.OrderBy(clip => clip.name).ToArray();
                var loudness = takes.Select(Loudness).Where(value => value > 0.0001f).ToArray();
                var measured = loudness.Length > 0 ? loudness.Average() : 0.1f;

                // The clips were levelled on disk, so this is a residual trim only.
                // It cannot exceed what Unity can apply: a voice's volume is
                // sfxVolume * gain, and that product is clamped at 1.
                var gain = Mathf.Clamp(TargetLoudness(pair.Key) / measured, 0.05f, 1f / library.sfxVolume);

                entries.Add(new CoupAudio.CueClip
                {
                    cue = pair.Key,
                    takes = takes,
                    gain = gain,
                    pitchSpread = PitchSpread(pair.Key)
                });
                report.Add($"{pair.Key} x{takes.Length}  measured {measured:F3} -> gain {gain:F2}");
            }

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
            return samples == null ? 0f : WindowLoudness(samples, clip.frequency, clip.channels);
        }

        private static float WindowLoudness(float[] samples, int frequency, int channels)
        {
            if (samples.Length == 0) return 0f;
            var window = Mathf.Min(samples.Length,
                Mathf.Max(1, (int)(LoudnessWindowSeconds * frequency) * channels));

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
