using UnityEditor;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Import policy for the generated audio, applied automatically on import.
    /// </summary>
    /// <remarks>
    /// A postprocessor rather than hand-edited importer settings so regenerating the WAVs —
    /// which tools/audio/synthesize_game_audio.py does as a plain file overwrite — cannot
    /// silently lose the configuration. The same reasoning as every generator in this repo:
    /// if a human has to remember to reapply a setting, it will eventually ship unapplied.
    ///
    /// SFX decompress on load: they are all under half a second and play dozens of times a
    /// minute; paying decode cost at play time is the wrong side of the trade. The music bed
    /// streams: it is 48 seconds and plays exactly once, looped — resident memory is the
    /// wrong side of ITS trade.
    /// </remarks>
    public sealed class AudioImportSettings : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;

            if (assetPath.Contains("Resources/Audio/SFX/"))
            {
                settings.loadType = UnityEngine.AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = UnityEngine.AudioCompressionFormat.ADPCM;
            }
            else if (assetPath.Contains("Resources/Audio/Music/"))
            {
                settings.loadType = UnityEngine.AudioClipLoadType.Streaming;
                settings.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
            }
            else
            {
                return;
            }

            importer.defaultSampleSettings = settings;
        }
    }
}
