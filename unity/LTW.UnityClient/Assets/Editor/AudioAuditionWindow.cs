#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Listens to the whole cue set without playing a match: every WAV under
    /// Resources/Audio, one click each, or the full set in sequence.
    /// </summary>
    /// <remarks>
    /// Exists because the audio tuning loop was "provoke the event in a match, hope you
    /// were listening" — reviewing seventeen cues that way takes a full game and still
    /// misses the rare ones (elimination, defeat). This plays the files themselves in
    /// edit mode, so a pass over the set takes twenty seconds and the doc's tuning table
    /// can be worked through cue by cue.
    ///
    /// Edit-mode clip preview has no public API, so this goes through reflection into
    /// UnityEditor.AudioUtil, resolving methods across the names Unity has used
    /// (PlayPreviewClip / PlayClip). If an editor upgrade renames them again the window
    /// says so plainly rather than sitting silent — the failure mode is a visible label,
    /// not a mystery.
    ///
    /// Deliberately NOT the director path: no rate limiting, no jitter, no ducking, no
    /// variant rotation — every take is listed individually. This tool answers "what does
    /// this file sound like", the game answers "what does the mix feel like"; blending
    /// the two would answer neither.
    /// </remarks>
    public sealed class AudioAuditionWindow : EditorWindow
    {
        private static readonly string[] SearchFolders = { "Assets/Resources/Audio" };

        private AudioClip[] clipsCache = Array.Empty<AudioClip>();
        private Vector2 scroll;
        private int sequenceIndex = -1;
        private double sequenceNextAt;
        private string? reflectionFailure;

        [MenuItem("Line Wars/Review/Audition Audio")]
        public static void Open()
        {
            var window = GetWindow<AudioAuditionWindow>("Audition Audio");
            window.Refresh();
            window.Show();
        }

        private void Refresh()
        {
            clipsCache = AssetDatabase.FindAssets("t:AudioClip", SearchFolders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(clip => clip != null)
                .ToArray();
        }

        private void OnGUI()
        {
            if (reflectionFailure != null)
            {
                EditorGUILayout.HelpBox(
                    "Edit-mode preview is unavailable in this editor version: " + reflectionFailure +
                    "\nThe files still exist — audition them in Play Mode or an external player.",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh")) Refresh();
                if (GUILayout.Button("Play All In Sequence"))
                {
                    StopPreview();
                    sequenceIndex = 0;
                    sequenceNextAt = 0;
                    EditorApplication.update += PumpSequence;
                }

                if (GUILayout.Button("Stop"))
                {
                    EditorApplication.update -= PumpSequence;
                    sequenceIndex = -1;
                    StopPreview();
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var clip in clipsCache)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var isCurrent = sequenceIndex >= 0 && sequenceIndex < clipsCache.Length &&
                                    clipsCache[sequenceIndex] == clip;
                    GUILayout.Label(isCurrent ? "▶" : " ", GUILayout.Width(16));
                    GUILayout.Label($"{clip.name}  ({clip.length:0.00}s)", GUILayout.MinWidth(240));
                    if (GUILayout.Button("Play", GUILayout.Width(60)))
                    {
                        StopPreview();
                        PlayPreview(clip);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Steps the play-all sequence: each clip, then a beat of silence.</summary>
        private void PumpSequence()
        {
            if (sequenceIndex < 0 || sequenceIndex >= clipsCache.Length)
            {
                EditorApplication.update -= PumpSequence;
                sequenceIndex = -1;
                Repaint();
                return;
            }

            if (EditorApplication.timeSinceStartup < sequenceNextAt)
            {
                return;
            }

            var clip = clipsCache[sequenceIndex];
            // The bed is 48s; five seconds says what it is without stalling the review.
            var hold = Mathf.Min(clip.length, 5f) + 0.35f;
            StopPreview();
            PlayPreview(clip);
            sequenceNextAt = EditorApplication.timeSinceStartup + hold;
            sequenceIndex++;
            Repaint();
        }

        private void PlayPreview(AudioClip clip)
        {
            if (!InvokeAudioUtil(new[] { "PlayPreviewClip", "PlayClip" },
                    new object[] { clip, 0, false },
                    new[] { typeof(AudioClip), typeof(int), typeof(bool) }))
            {
                // Fall back to the two-argument and one-argument historical shapes.
                if (!InvokeAudioUtil(new[] { "PlayPreviewClip", "PlayClip" }, new object[] { clip }, new[] { typeof(AudioClip) }))
                {
                    reflectionFailure ??= "no PlayPreviewClip/PlayClip method matched";
                }
            }
        }

        private void StopPreview()
        {
            InvokeAudioUtil(new[] { "StopAllPreviewClips", "StopAllClips" }, Array.Empty<object>(), Type.EmptyTypes);
        }

        private bool InvokeAudioUtil(string[] names, object[] args, Type[] signature)
        {
            var util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (util == null)
            {
                reflectionFailure ??= "UnityEditor.AudioUtil not found";
                return false;
            }

            foreach (var name in names)
            {
                var method = util.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, signature, null);
                if (method != null)
                {
                    method.Invoke(null, args);
                    return true;
                }
            }

            return false;
        }
    }
}
