using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coup.Game
{
    /// <summary>
    /// The one object that makes noise. It boots itself before the first scene, owns
    /// a small pool of voices and two music channels, and survives scene loads so the
    /// lobby theme can cross-fade into the table instead of cutting out.
    ///
    /// Callers go through <see cref="Sfx"/> and name a <see cref="Cue"/>; nothing in
    /// the game holds an AudioSource of its own.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class AudioDirector : MonoBehaviour
    {
        public const string MutedKey = "Coup.AudioMuted";     // set by the existing settings panel
        public const string MusicMutedKey = "Coup.MusicMuted";

        private const string LibraryResource = "CoupAudio";
        private const int Voices = 12;
        private const float CrossfadeSeconds = 1.1f;

        /// <summary>Two of the same cue closer together than this collapse into one.</summary>
        private const float RepeatGuardSeconds = 0.035f;

        private static AudioDirector _instance;
        private static bool _spawning;

        public CoupAudio library;

        private readonly AudioSource[] _voices = new AudioSource[Voices];
        private int _nextVoice;
        private readonly Dictionary<Cue, float> _lastPlayed = new();

        private AudioSource _musicA, _musicB;
        private bool _musicOnA;
        private AudioClip _musicWanted;
        private float _fade = 1f;

        private AudioListener _ownListener;

        /// <summary>The scene the mix is currently set up for. Empty until the first pass.</summary>
        private string _sceneSeen = "";

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (_instance != null) _instance.ApplyMix();
            }
        }

        public static bool MusicMuted
        {
            get => PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MusicMutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (_instance != null) _instance.ApplyMix();
            }
        }

        // ------------------------------------------------------------------ boot

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() => Ensure();

        public static AudioDirector Ensure()
        {
            if (_instance != null) return _instance;
            if (_spawning || !Application.isPlaying) return null;

            _spawning = true;
            var go = new GameObject("AudioDirector");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioDirector>();
            _spawning = false;
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            if (library == null) library = Resources.Load<CoupAudio>(LibraryResource);

            for (var i = 0; i < Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;      // a flat 2D interface; position means nothing
                source.bypassReverbZones = true;
                _voices[i] = source;
            }

            _musicA = NewMusicChannel();
            _musicB = NewMusicChannel();

            _ownListener = gameObject.AddComponent<AudioListener>();
            _ownListener.enabled = false;

            ApplyMix();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private AudioSource NewMusicChannel()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.bypassReverbZones = true;
            source.volume = 0f;
            return source;
        }

        /// <summary>
        /// Picks up a change of scene. This watches the active scene rather than
        /// subscribing to sceneLoaded, because the director boots before the first
        /// scene and so never receives that scene's load event — which left the
        /// authored lobby buttons silent.
        /// </summary>
        private void CheckScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.name == _sceneSeen) return;
            _sceneSeen = scene.name;

            AdoptListener();
            PlaySceneMusic(scene.name);
            BindClicks(null);
        }

        /// <summary>
        /// Keeps exactly one listener alive. A code-built scene may ship none, and a
        /// second one makes Unity drop the whole mix with a warning.
        /// </summary>
        private void AdoptListener()
        {
            var found = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            var other = false;
            foreach (var listener in found)
            {
                if (listener == _ownListener) continue;
                if (!listener.enabled) continue;
                if (other) listener.enabled = false;   // only ever keep the first
                else other = true;
            }
            if (_ownListener != null) _ownListener.enabled = !other;
        }

        // ------------------------------------------------------------------- mix

        private void ApplyMix()
        {
            AudioListener.volume = 1f;   // the old settings toggle drove this; cues own it now
            var sfx = Muted ? 0f : (library != null ? library.sfxVolume : 0.85f);
            foreach (var voice in _voices) if (voice != null) voice.volume = sfx;
        }

        private float MusicTarget =>
            Muted || MusicMuted ? 0f : (library != null ? library.musicVolume : 0.34f);

        // ----------------------------------------------------------------- cues

        public void PlayCue(Cue cue, float pitch, float volume)
        {
            if (cue == Cue.None || library == null || Muted) return;

            var now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(cue, out var last) && now - last < RepeatGuardSeconds) return;
            _lastPlayed[cue] = now;

            var entry = library.For(cue);
            var clip = library.Clip(cue);
            if (clip == null) return;

            var spread = entry != null ? entry.pitchSpread : 0f;
            var semitones = spread > 0f ? Random.Range(-spread, spread) : 0f;

            var voice = FreeVoice();
            voice.clip = clip;
            voice.pitch = pitch * Mathf.Pow(2f, semitones / 12f);
            voice.volume = library.sfxVolume * volume * (entry != null ? entry.gain : 1f);
            voice.Play();
        }

        /// <summary>The first idle voice, or the oldest one if every voice is busy.</summary>
        private AudioSource FreeVoice()
        {
            for (var i = 0; i < Voices; i++)
            {
                var voice = _voices[(_nextVoice + i) % Voices];
                if (voice != null && !voice.isPlaying)
                {
                    _nextVoice = (_nextVoice + i + 1) % Voices;
                    return voice;
                }
            }
            var fallback = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            return fallback;
        }

        // ---------------------------------------------------------------- music

        private void PlaySceneMusic(string sceneName)
        {
            if (library == null) return;
            var wanted = sceneName == "Lobby" ? library.lobbyTheme
                       : sceneName == "Game" ? library.tableTheme
                       : null;
            PlayMusic(wanted);
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == _musicWanted) return;
            _musicWanted = clip;

            var incoming = _musicOnA ? _musicB : _musicA;
            if (clip != null)
            {
                incoming.clip = clip;
                incoming.volume = 0f;
                incoming.Play();
            }
            _musicOnA = !_musicOnA;
            _fade = 0f;
        }

        private void Update()
        {
            CheckScene();

            var target = MusicTarget;
            var up = _musicOnA ? _musicA : _musicB;
            var down = _musicOnA ? _musicB : _musicA;

            if (_fade < 1f) _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / CrossfadeSeconds);

            if (up != null) up.volume = target * _fade;
            if (down != null)
            {
                down.volume = target * (1f - _fade);
                if (_fade >= 1f && down.isPlaying) { down.Stop(); down.clip = null; }
            }
        }

        // ---------------------------------------------------------------- clicks

        /// <summary>
        /// Gives every button under <paramref name="root"/> a click sound. The marker
        /// listens for the pointer itself rather than adding an onClick listener,
        /// because several panels rebuild their handlers with RemoveAllListeners and
        /// would quietly strip the sound again.
        /// </summary>
        public static void BindClicks(Transform root)
        {
            if (!Application.isPlaying) return;

            if (root != null)
            {
                Bind(root.GetComponentsInChildren<UnityEngine.UI.Selectable>(true));
                return;
            }

            Bind(FindObjectsByType<UnityEngine.UI.Selectable>(FindObjectsInactive.Include));
        }

        private static void Bind(IReadOnlyList<UnityEngine.UI.Selectable> targets)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null) continue;
                if (target.GetComponent<UiClickSound>() == null)
                    target.gameObject.AddComponent<UiClickSound>();
            }
        }
    }

    /// <summary>Short names for the call sites, so a cue reads as one word of staging.</summary>
    public static class Sfx
    {
        public static void Play(Cue cue, float pitch = 1f, float volume = 1f)
        {
            var director = AudioDirector.Ensure();
            if (director != null) director.PlayCue(cue, pitch, volume);
        }

        /// <summary>Plays the cue after a delay, for staging a beat that lands late.</summary>
        public static void PlayDelayed(Cue cue, float delay, float pitch = 1f, float volume = 1f)
        {
            var director = AudioDirector.Ensure();
            if (director == null) return;
            if (delay <= 0f) { director.PlayCue(cue, pitch, volume); return; }
            director.StartCoroutine(After(delay, cue, pitch, volume));
        }

        private static System.Collections.IEnumerator After(float delay, Cue cue, float pitch, float volume)
        {
            yield return new WaitForSecondsRealtime(delay);
            var director = AudioDirector.Ensure();
            if (director != null) director.PlayCue(cue, pitch, volume);
        }

        public static void BindClicks(Transform root) => AudioDirector.BindClicks(root);

        public static bool Muted
        {
            get => AudioDirector.Muted;
            set => AudioDirector.Muted = value;
        }

        public static bool MusicMuted
        {
            get => AudioDirector.MusicMuted;
            set => AudioDirector.MusicMuted = value;
        }
    }
}
