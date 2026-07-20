#nullable enable

using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class LocalSessionFlowOverlay : MonoBehaviour
    {
        private static readonly Color Cloud = new Color(0.957f, 0.969f, 1f, 1f);
        private static readonly Color MutedCloud = new Color(0.62f, 0.72f, 0.88f, 1f);
        private static readonly Color PanelInk = new Color(0.027f, 0.047f, 0.082f, 0.94f);
        private static readonly Color PanelTrim = new Color(0.176f, 0.376f, 0.612f, 0.88f);
        private static readonly Color MintSignal = new Color(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color SignalGold = new Color(1f, 0.784f, 0.29f, 1f);
        private static readonly Color ArcaneBlue = new Color(0.247f, 0.557f, 0.957f, 1f);
        private static readonly Color WarningRose = new Color(1f, 0.384f, 0.455f, 1f);

        private UnitySimulationDriver simulationDriver = null!;
        private LocalPlaytestRecorder? playtestRecorder;
        private GUIStyle? buttonStyle;
        private GUIStyle? titleStyle;
        private GUIStyle? subtitleStyle;
        private GUIStyle? bodyStyle;
        private GUIStyle? smallStyle;
        private Texture2D? whiteTexture;
        private bool showSettings;
        private PreMatchScreen preMatchScreen = PreMatchScreen.Title;

        private enum PreMatchScreen
        {
            Title,
            Ready,
            HowTo
        }

        public void Initialize(UnitySimulationDriver driver, LocalPlaytestRecorder recorder)
        {
            simulationDriver = driver;
            playtestRecorder = recorder;
        }

        private void OnGUI()
        {
            if (simulationDriver is null)
            {
                return;
            }

            EnsureStyle();

            var scale = MobileViewportLayout.UiScale();
            if (showSettings)
            {
                DrawSettingsPanel(scale);
                return;
            }

            if (simulationDriver.IsOpeningBuildCountdown)
            {
                DrawBuildCountdownPanel(scale);
                return;
            }

            if (simulationDriver.LatestMatchSummary is not null)
            {
                DrawResultsPanel(scale);
                return;
            }

            if (!simulationDriver.HasStarted)
            {
                if (preMatchScreen == PreMatchScreen.Title)
                {
                    DrawTitlePanel(scale);
                    return;
                }

                if (preMatchScreen == PreMatchScreen.HowTo)
                {
                    DrawHowToPanel(scale);
                    return;
                }

                DrawReadyPanel(scale);
                return;
            }

            if (simulationDriver.IsPaused)
            {
                DrawPausePanel(scale);
                return;
            }

            DrawLiveRail(scale);
        }

        private void DrawTitlePanel(float scale)
        {
            var panel = CenteredPanel(scale, 348f, 284f);
            DrawPanel(panel, scale);

            DrawLabel(panel.x + 22f * scale, panel.y + 20f * scale, panel.width - 44f * scale, 32f * scale, "LINE WARDS", titleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 22f * scale, panel.y + 54f * scale, panel.width - 44f * scale, 22f * scale, "EIGHT-LANE TOWER DUEL", subtitleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(
                panel.x + 30f * scale,
                panel.y + 88f * scale,
                panel.width - 60f * scale,
                48f * scale,
                "Build defenses, send pressure, and read the rotating lane war before it overruns you.",
                bodyStyle!,
                TextAnchor.MiddleCenter);

            var buttonWidth = 126f * scale;
            var buttonHeight = 30f * scale;
            var gap = 8f * scale;
            var leftX = panel.center.x - buttonWidth - gap * 0.5f;
            var rightX = panel.center.x + gap * 0.5f;
            var topY = panel.yMax - 108f * scale;

            if (DrawButton(new Rect(leftX, topY, buttonWidth, buttonHeight), "START GAME", MintSignal, scale))
            {
                ResetToReady();
                simulationDriver.BeginOpeningBuildCountdown();
            }

            if (DrawButton(new Rect(rightX, topY, buttonWidth, buttonHeight), "HOW TO PLAY", SignalGold, scale, 10f))
            {
                preMatchScreen = PreMatchScreen.HowTo;
            }

            if (DrawButton(new Rect(leftX, topY + buttonHeight + gap, buttonWidth, buttonHeight), "SETTINGS", ArcaneBlue, scale))
            {
                showSettings = true;
            }

            if (DrawButton(new Rect(rightX, topY + buttonHeight + gap, buttonWidth, buttonHeight), "QUIT", WarningRose, scale))
            {
                QuitGame();
            }

            DrawLabel(panel.x + 22f * scale, panel.yMax - 26f * scale, panel.width - 44f * scale, 16f * scale, "Prototype local vertical slice", smallStyle!, TextAnchor.MiddleCenter);
        }

        private void DrawHowToPanel(float scale)
        {
            var panel = CenteredPanel(scale, 348f, 292f);
            DrawPanel(panel, scale);

            DrawLabel(panel.x + 22f * scale, panel.y + 18f * scale, panel.width - 44f * scale, 28f * scale, "HOW TO PLAY", titleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 26f * scale, panel.y + 50f * scale, panel.width - 52f * scale, 24f * scale, "The current prototype is a fast eight-lane systems test.", bodyStyle!, TextAnchor.MiddleCenter);

            var textX = panel.x + 30f * scale;
            var textY = panel.y + 86f * scale;
            var textWidth = panel.width - 60f * scale;
            var lineHeight = 30f * scale;

            DrawLabel(textX, textY, textWidth, lineHeight, "1. Build towers on your lane platforms.", smallStyle!, TextAnchor.MiddleLeft);
            DrawLabel(textX, textY + lineHeight, textWidth, lineHeight, "2. Send creeps to pressure the next opponent.", smallStyle!, TextAnchor.MiddleLeft);
            DrawLabel(textX, textY + lineHeight * 2f, textWidth, lineHeight, "3. Survive leaks as pressure rotates across enemy lanes.", smallStyle!, TextAnchor.MiddleLeft);
            DrawLabel(textX, textY + lineHeight * 3f, textWidth, lineHeight, "4. Bots on lanes 2-8 should build and send from the start.", smallStyle!, TextAnchor.MiddleLeft);

            var buttonWidth = 118f * scale;
            var buttonHeight = 30f * scale;
            var gap = 8f * scale;
            var rowY = panel.yMax - 46f * scale;
            var startX = panel.center.x - buttonWidth - gap * 0.5f;

            if (DrawButton(new Rect(startX, rowY, buttonWidth, buttonHeight), "READY", MintSignal, scale))
            {
                ResetToReady();
            }

            if (DrawButton(new Rect(startX + buttonWidth + gap, rowY, buttonWidth, buttonHeight), "BACK", ArcaneBlue, scale))
            {
                preMatchScreen = PreMatchScreen.Title;
            }
        }

        private void DrawReadyPanel(float scale)
        {
            var panel = CenteredPanel(scale, 336f, 224f);
            DrawPanel(panel, scale);

            DrawLabel(panel.x + 22f * scale, panel.y + 20f * scale, panel.width - 44f * scale, 28f * scale, "LINE WARDS", titleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 22f * scale, panel.y + 50f * scale, panel.width - 44f * scale, 20f * scale, "LOCAL VERTICAL SLICE", subtitleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(
                panel.x + 28f * scale,
                panel.y + 82f * scale,
                panel.width - 56f * scale,
                44f * scale,
                $"Tap PLAY to begin an {Mathf.RoundToInt(simulationDriver.OpeningBuildCountdownDuration)} second build window.",
                bodyStyle!,
                TextAnchor.MiddleCenter);

            var buttonY = panel.yMax - 78f * scale;
            var buttonWidth = 112f * scale;
            var buttonHeight = 30f * scale;
            var gap = 8f * scale;
            var startX = panel.center.x - buttonWidth - gap * 0.5f;

            if (DrawButton(new Rect(startX, buttonY, buttonWidth, buttonHeight), "PLAY", MintSignal, scale))
            {
                showSettings = false;
                simulationDriver.BeginOpeningBuildCountdown();
            }

            if (DrawButton(new Rect(startX + buttonWidth + gap, buttonY, buttonWidth, buttonHeight), "SETTINGS", ArcaneBlue, scale))
            {
                showSettings = true;
            }

            var resetWidth = 84f * scale;
            if (DrawButton(new Rect(panel.center.x - resetWidth * 0.5f, buttonY + buttonHeight + gap, resetWidth, 24f * scale), "MENU", Cloud, scale, 10f))
            {
                ResetToTitle();
            }
        }

        private void DrawPausePanel(float scale)
        {
            var panel = CenteredPanel(scale, 316f, 224f);
            DrawPanel(panel, scale);

            DrawLabel(panel.x + 22f * scale, panel.y + 20f * scale, panel.width - 44f * scale, 28f * scale, "PAUSED", titleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 26f * scale, panel.y + 58f * scale, panel.width - 52f * scale, 36f * scale, "Take a breath. The lanes are holding.", bodyStyle!, TextAnchor.MiddleCenter);

            var buttonWidth = 112f * scale;
            var buttonHeight = 30f * scale;
            var gap = 8f * scale;
            var rowY = panel.yMax - 82f * scale;
            var startX = panel.center.x - buttonWidth - gap * 0.5f;

            if (DrawButton(new Rect(startX, rowY, buttonWidth, buttonHeight), "RESUME", MintSignal, scale))
            {
                simulationDriver.TogglePause();
            }

            if (DrawButton(new Rect(startX + buttonWidth + gap, rowY, buttonWidth, buttonHeight), "SETTINGS", ArcaneBlue, scale))
            {
                showSettings = true;
            }

            var lowerWidth = 92f * scale;
            var lowerY = rowY + buttonHeight + gap;
            if (DrawButton(new Rect(panel.center.x - lowerWidth - gap * 0.5f, lowerY, lowerWidth, 24f * scale), "RESET", WarningRose, scale, 10f))
            {
                ResetToReady();
            }

            if (DrawButton(new Rect(panel.center.x + gap * 0.5f, lowerY, lowerWidth, 24f * scale), "MENU", Cloud, scale, 10f))
            {
                ResetToTitle();
            }
        }

        private void DrawBuildCountdownPanel(float scale)
        {
            var frame = MobileViewportLayout.ScreenRect();
            var margin = MobileViewportLayout.EdgeMargin(scale);
            var width = Mathf.Min(318f * scale, frame.width - margin * 2f);
            var height = 112f * scale;
            var panel = new Rect(frame.center.x - width * 0.5f, frame.y + 132f * scale, width, height);
            DrawPanel(panel, scale);

            var seconds = Mathf.CeilToInt(simulationDriver.OpeningBuildCountdownRemaining);
            DrawLabel(panel.x + 18f * scale, panel.y + 10f * scale, panel.width - 36f * scale, 20f * scale, "BUILD PHASE", subtitleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 24f * scale, panel.y + 31f * scale, panel.width - 48f * scale, 32f * scale, seconds.ToString(), titleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 22f * scale, panel.y + 62f * scale, panel.width - 44f * scale, 18f * scale, "Place opening towers. Sends unlock when LIVE begins.", bodyStyle!, TextAnchor.MiddleCenter);

            var buttonWidth = 96f * scale;
            var buttonHeight = 24f * scale;
            var gap = 8f * scale;
            var rowY = panel.yMax - 30f * scale;
            var startX = panel.center.x - buttonWidth - gap * 0.5f;

            if (DrawButton(new Rect(startX, rowY, buttonWidth, buttonHeight), "START NOW", MintSignal, scale, 9f))
            {
                simulationDriver.StartMatch();
            }

            if (DrawButton(new Rect(startX + buttonWidth + gap, rowY, buttonWidth, buttonHeight), "MENU", Cloud, scale, 9f))
            {
                ResetToTitle();
            }
        }

        private void DrawResultsPanel(float scale)
        {
            var frame = MobileViewportLayout.ScreenRect();
            var margin = MobileViewportLayout.EdgeMargin(scale);
            var width = Mathf.Min(304f * scale, frame.width - margin * 2f);
            var height = 94f * scale;
            var panel = new Rect(frame.center.x - width * 0.5f, frame.yMax - height - margin, width, height);
            DrawPanel(panel, scale);

            DrawLabel(panel.x + 18f * scale, panel.y + 10f * scale, panel.width - 36f * scale, 22f * scale, "MATCH COMPLETE", subtitleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 22f * scale, panel.y + 30f * scale, panel.width - 44f * scale, 18f * scale, "Use the scoreboard above, then choose the next run.", bodyStyle!, TextAnchor.MiddleCenter);

            var buttonWidth = 84f * scale;
            var buttonHeight = 30f * scale;
            var gap = 6f * scale;
            var rowY = panel.yMax - 38f * scale;
            var startX = panel.center.x - buttonWidth * 1.5f - gap;

            if (DrawButton(new Rect(startX, rowY, buttonWidth, buttonHeight), "REMATCH", MintSignal, scale))
            {
                ResetToReady();
                simulationDriver.BeginOpeningBuildCountdown();
            }

            if (DrawButton(new Rect(startX + buttonWidth + gap, rowY, buttonWidth, buttonHeight), "SETTINGS", ArcaneBlue, scale))
            {
                showSettings = true;
            }

            if (DrawButton(new Rect(startX + (buttonWidth + gap) * 2f, rowY, buttonWidth, buttonHeight), "MENU", Cloud, scale))
            {
                ResetToTitle();
            }
        }

        private void DrawSettingsPanel(float scale)
        {
            var panel = CenteredPanel(scale, 336f, 244f);
            DrawPanel(panel, scale);

            DrawLabel(panel.x + 22f * scale, panel.y + 18f * scale, panel.width - 44f * scale, 28f * scale, "SETTINGS", titleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 22f * scale, panel.y + 50f * scale, panel.width - 44f * scale, 22f * scale, "Presentation toggles for quick playtest reads.", bodyStyle!, TextAnchor.MiddleCenter);

            var labelX = panel.x + 26f * scale;
            var rowX = panel.x + 146f * scale;
            var rowY = panel.y + 86f * scale;
            var rowHeight = 28f * scale;
            var buttonHeight = 24f * scale;
            var tinyWidth = 42f * scale;
            var wideWidth = 112f * scale;
            var gap = 6f * scale;

            DrawLabel(labelX, rowY, 110f * scale, rowHeight, "Audio", smallStyle!, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(rowX, rowY + 2f * scale, wideWidth, buttonHeight), PresentationPreferences.AudioMuted ? "MUTED" : "ON", PresentationPreferences.AudioMuted ? WarningRose : MintSignal, scale, 10f))
            {
                PresentationPreferences.ToggleAudioMuted();
            }

            rowY += rowHeight + gap;
            DrawLabel(labelX, rowY, 110f * scale, rowHeight, "Effects", smallStyle!, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(rowX, rowY + 2f * scale, wideWidth, buttonHeight), PresentationPreferences.ReducedEffects ? "REDUCED" : "FULL", PresentationPreferences.ReducedEffects ? SignalGold : MintSignal, scale, 10f))
            {
                PresentationPreferences.ReducedEffects = !PresentationPreferences.ReducedEffects;
            }

            rowY += rowHeight + gap;
            DrawLabel(labelX, rowY, 110f * scale, rowHeight, $"Text {PresentationPreferences.TextScale:0.0}x", smallStyle!, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(rowX, rowY + 2f * scale, tinyWidth, buttonHeight), "-", Cloud, scale, 12f))
            {
                PresentationPreferences.TextScale = Mathf.Clamp(PresentationPreferences.TextScale - 0.1f, 0.8f, 1.5f);
            }

            if (DrawButton(new Rect(rowX + tinyWidth + gap, rowY + 2f * scale, tinyWidth, buttonHeight), "+", Cloud, scale, 12f))
            {
                PresentationPreferences.TextScale = Mathf.Clamp(PresentationPreferences.TextScale + 0.1f, 0.8f, 1.5f);
            }

            rowY += rowHeight + gap;
            DrawLabel(labelX, rowY, 110f * scale, rowHeight, $"Volume {Mathf.RoundToInt(PresentationPreferences.FeedbackVolume * 100f)}%", smallStyle!, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(rowX, rowY + 2f * scale, tinyWidth, buttonHeight), "-", Cloud, scale, 12f))
            {
                PresentationPreferences.AdjustFeedbackVolume(-0.1f);
            }

            if (DrawButton(new Rect(rowX + tinyWidth + gap, rowY + 2f * scale, tinyWidth, buttonHeight), "+", Cloud, scale, 12f))
            {
                PresentationPreferences.AdjustFeedbackVolume(0.1f);
            }

            var backWidth = 96f * scale;
            if (DrawButton(new Rect(panel.center.x - backWidth * 0.5f, panel.yMax - 38f * scale, backWidth, 26f * scale), "BACK", ArcaneBlue, scale, 10f))
            {
                showSettings = false;
            }
        }

        private void DrawLiveRail(float scale)
        {
            var frame = MobileViewportLayout.ScreenRect();
            var buttonWidth = 62f * scale;
            var buttonHeight = 26f * scale;
            var resetWidth = 34f * scale;
            var resetHeight = 24f * scale;
            var gap = 4f * scale;
            var y = frame.y + 58f * scale;
            var x = frame.xMax - buttonWidth - MobileViewportLayout.EdgeMargin(scale);

            if (DrawButton(new Rect(x, y, buttonWidth, buttonHeight), "PAUSE", SignalGold, scale, 11f))
            {
                simulationDriver.TogglePause();
            }

            if (DrawButton(new Rect(x + buttonWidth - resetWidth, y + buttonHeight + gap, resetWidth, resetHeight), "R", Cloud, scale, 10f))
            {
                ResetToReady();
            }
        }

        private Rect CenteredPanel(float scale, float targetWidth, float targetHeight)
        {
            var frame = MobileViewportLayout.ScreenRect();
            var margin = MobileViewportLayout.EdgeMargin(scale);
            var width = Mathf.Min(targetWidth * scale, frame.width - margin * 2f);
            var height = Mathf.Min(targetHeight * scale, frame.height - margin * 2f);
            return new Rect(frame.center.x - width * 0.5f, frame.center.y - height * 0.5f, width, height);
        }

        private void DrawPanel(Rect rect, float scale)
        {
            var priorColor = GUI.color;

            GUI.color = PanelInk;
            GUI.DrawTexture(rect, WhiteTexture);

            var trim = Mathf.Max(1f, 2f * scale);
            GUI.color = PanelTrim;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, trim), WhiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - trim, rect.width, trim), WhiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, trim, rect.height), WhiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - trim, rect.y, trim, rect.height), WhiteTexture);

            GUI.color = priorColor;
        }

        private void DrawLabel(float x, float y, float width, float height, string text, GUIStyle style, TextAnchor alignment)
        {
            var priorAlignment = style.alignment;
            style.alignment = alignment;
            GUI.Label(new Rect(x, y, width, height), text, style);
            style.alignment = priorAlignment;
        }

        private bool DrawButton(Rect rect, string label, Color accent, float scale, float baseFontSize = 11f)
        {
            buttonStyle!.fontSize = Mathf.RoundToInt(baseFontSize * scale * PresentationPreferences.TextScale);
            return RuntimeUiChrome.DrawPanelButton(rect, label, accent, scale, buttonStyle);
        }

        private void ResetToReady()
        {
            showSettings = false;
            preMatchScreen = PreMatchScreen.Ready;
            simulationDriver.ResetMatch();
            playtestRecorder?.ResetRecorder();
        }

        private void ResetToTitle()
        {
            showSettings = false;
            preMatchScreen = PreMatchScreen.Title;
            simulationDriver.ResetMatch();
            playtestRecorder?.ResetRecorder();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private Texture2D WhiteTexture
        {
            get
            {
                if (whiteTexture is not null)
                {
                    return whiteTexture;
                }

                whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                whiteTexture.SetPixel(0, 0, Color.white);
                whiteTexture.Apply();
                return whiteTexture;
            }
        }

        private void EnsureStyle()
        {
            buttonStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };

            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };

            subtitleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = MintSignal }
            };

            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = MutedCloud }
            };

            smallStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };

            titleStyle.fontSize = Mathf.RoundToInt(20f * MobileViewportLayout.UiScale() * PresentationPreferences.TextScale);
            subtitleStyle.fontSize = Mathf.RoundToInt(11f * MobileViewportLayout.UiScale() * PresentationPreferences.TextScale);
            bodyStyle.fontSize = Mathf.RoundToInt(10f * MobileViewportLayout.UiScale() * PresentationPreferences.TextScale);
            smallStyle.fontSize = Mathf.RoundToInt(10f * MobileViewportLayout.UiScale() * PresentationPreferences.TextScale);
        }
    }
}
