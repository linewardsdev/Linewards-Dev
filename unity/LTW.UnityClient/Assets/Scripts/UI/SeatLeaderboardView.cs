#nullable enable

using System.Linq;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    /// <summary>
    /// Every seat's lives and income, drawn in the side rail a wide screen leaves beside the board.
    /// </summary>
    /// <remarks>
    /// Two requests answered by one thing. The iPad bug-test list asks for a leaderboard showing
    /// other players' lives and income (item 5), and separately reports that the game does not fill
    /// a 13-inch iPad (item 11). The board cannot grow to close that gap — orthographicSize is
    /// vertical and the lane already fills it, so zooming in crops the player's own lane — which
    /// leaves a margin of 221 to 396 device pixels that has to become something or stay padding.
    /// This is what it becomes.
    ///
    /// It draws ONLY when <see cref="MobileViewportLayout.HasSideRails"/> is true, and that is the
    /// whole of its responsive behaviour. On a phone the margin is given back to the board and there
    /// is no rail to draw in, so this stands down completely rather than trying to squeeze an
    /// eight-row table into a strip too narrow to read — a phone still reaches the same data through
    /// the results screen.
    ///
    /// No new simulation data was needed: every seat's Lives, Gold, Income and IsEliminated are
    /// already on the snapshot, because an eight-seat match is the default and the results screen
    /// already scores all of them.
    /// </remarks>
    public sealed class SeatLeaderboardView : MonoBehaviour
    {
        private static readonly Color PanelInk = new(0.027f, 0.047f, 0.082f, 0.92f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color MutedCloud = new(0.62f, 0.72f, 0.88f, 1f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color WarningRose = new(1f, 0.384f, 0.455f, 1f);

        private UnitySimulationDriver simulationDriver = null!;
        private GUIStyle? titleStyle;
        private GUIStyle? seatStyle;
        private GUIStyle? valueStyle;
        private GUIStyle? labelStyle;

        public void Initialize(UnitySimulationDriver driver)
        {
            simulationDriver = driver;
        }

        private void OnGUI()
        {
            // Same modality rule the rest of the HUD follows: IMGUI dispatches in draw order, so a
            // shell screen's scrim can dim this but cannot stop it taking a click.
            if (RuntimeUiChrome.ModalScreenActive || simulationDriver == null)
            {
                return;
            }

            if (!MobileViewportLayout.HasSideRails)
            {
                return;
            }

            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            var rail = MobileViewportLayout.SideRailRect(rightSide: true);
            if (rail.width < 1f)
            {
                return;
            }

            EnsureStyles();
            var scale = MobileViewportLayout.UiScale();
            var seats = snapshot.Players.Players.OrderBy(seat => seat.PlayerId.Value).ToArray();
            if (seats.Length == 0)
            {
                return;
            }

            var margin = MobileViewportLayout.EdgeMargin(scale);
            var rowHeight = 46f * scale;
            var headerHeight = 30f * scale;
            var panelHeight = headerHeight + seats.Length * rowHeight + margin;
            var panel = new Rect(
                rail.x + margin * 0.5f,
                rail.y + MobileViewportLayout.TopMargin(scale),
                Mathf.Max(0f, rail.width - margin),
                Mathf.Min(panelHeight, rail.height - MobileViewportLayout.TopMargin(scale) - margin));

            RuntimeUiChrome.DrawPanel(panel, PanelInk, scale);

            titleStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            GUI.Label(new Rect(panel.x, panel.y + 6f * scale, panel.width, headerHeight), "SEATS", titleStyle);

            var localId = simulationDriver.LocalPlayerId;
            for (var index = 0; index < seats.Length; index++)
            {
                var seat = seats[index];
                var row = new Rect(
                    panel.x + 4f * scale,
                    panel.y + headerHeight + index * rowHeight,
                    panel.width - 8f * scale,
                    rowHeight);

                if (row.yMax > panel.yMax)
                {
                    break;
                }

                DrawSeatRow(row, seat, seat.PlayerId.Equals(localId), scale);
            }
        }

        /// <summary>
        /// One seat: who, lives, income — and whether it is you.
        /// </summary>
        /// <remarks>
        /// The local seat carries a filled left rail rather than a tinted row. A tinted band is the
        /// thing that went wrong twice on the results table: strong enough to see, it read as an
        /// opaque bar over the field; weak enough not to, it read as nothing. A solid rail is
        /// unambiguous at any alpha and keeps every row on the same baseline. An eliminated seat
        /// also says OUT in words, so colour is never the only carrier.
        /// </remarks>
        private void DrawSeatRow(Rect row, LTW.Simulation.Economy.PlayerEconomyState seat, bool isLocal, float scale)
        {
            if (isLocal)
            {
                Fill(new Rect(row.x, row.y + 2f * scale, 3f * scale, row.height - 4f * scale), ArcaneBlue);
            }

            var textX = row.x + 9f * scale;
            var accent = seat.IsEliminated ? MutedCloud : Cloud;

            seatStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            seatStyle.normal.textColor = accent;
            GUI.Label(new Rect(textX, row.y + 3f * scale, row.width * 0.5f, 16f * scale), $"P{seat.PlayerId.Value}", seatStyle);

            if (seat.IsEliminated)
            {
                labelStyle!.fontSize = Mathf.RoundToInt(10f * scale);
                labelStyle.normal.textColor = WarningRose;
                GUI.Label(new Rect(textX, row.y + 20f * scale, row.width - 12f * scale, 16f * scale), "OUT", labelStyle);
                return;
            }

            // Lives first and largest: it is the win condition, and the number a player scans for.
            valueStyle!.fontSize = Mathf.RoundToInt(15f * scale);
            valueStyle.normal.textColor = seat.Lives.Amount <= 5 ? WarningRose : SignalGold;
            GUI.Label(new Rect(row.xMax - row.width * 0.52f, row.y + 2f * scale, row.width * 0.5f, 18f * scale), $"{seat.Lives.Amount}", valueStyle);

            labelStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            labelStyle.normal.textColor = MutedCloud;
            GUI.Label(new Rect(textX, row.y + 21f * scale, row.width * 0.45f, 14f * scale), "LIVES", labelStyle);

            labelStyle.normal.textColor = MintSignal;
            GUI.Label(new Rect(row.xMax - row.width * 0.52f, row.y + 21f * scale, row.width * 0.5f, 14f * scale), $"+{seat.Income.Amount}", labelStyle);
        }

        private static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ArcaneBlue }
            };

            seatStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };

            valueStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                normal = { textColor = SignalGold }
            };

            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = MutedCloud }
            };
        }
    }
}
