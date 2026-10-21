using UnityEditor;
using UnityEngine;

namespace Coup.EditorTools
{
    /// <summary>
    /// Everything under Assets/Audio is either a short interface or table cue or one
    /// of the two music loops, and the two want opposite import settings. Running as a
    /// postprocessor matches the sprite rule: a clip dropped into the folder later is
    /// configured on arrival instead of by hand, and no .meta needs editing.
    /// </summary>
    public class CoupAudioImportSettings : AssetPostprocessor
    {
        private const string AudioRoot = "Assets/Audio/";
        private const string MusicFolder = "/Music/";

        private static readonly string[] MobilePlatforms = { "Android", "iPhone" };

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioRoot)) return;

            var importer = (AudioImporter)assetImporter;
            var isMusic = assetPath.Contains(MusicFolder);

            // Cues fire from a 2D interface, so a stereo copy would only double the
            // memory. The themes keep their width.
            importer.forceToMono = !isMusic;
            importer.ambisonic = false;

            var settings = importer.defaultSampleSettings;
            settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = isMusic ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.quality = isMusic ? 0.5f : 1f;
            settings.sampleRateSetting = isMusic
                ? AudioSampleRateSetting.PreserveSampleRate
                : AudioSampleRateSetting.OptimizeSampleRate;
            settings.preloadAudioData = !isMusic;
            importer.defaultSampleSettings = settings;

            importer.loadInBackground = isMusic;

            // Without an explicit override mobile falls back to the default block.
            // Vorbis on the themes keeps the download down; ADPCM on the cues keeps
            // them cheap to decode when several fire in the same frame.
            foreach (var platform in MobilePlatforms)
            {
                var mobile = importer.GetOverrideSampleSettings(platform);
                mobile.preloadAudioData = !isMusic;
                mobile.loadType = settings.loadType;
                mobile.compressionFormat = isMusic ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                mobile.quality = isMusic ? 0.45f : 1f;
                mobile.sampleRateSetting = settings.sampleRateSetting;
                importer.SetOverrideSampleSettings(platform, mobile);
            }
        }
    }
}
