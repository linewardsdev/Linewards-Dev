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
    /// It has two homes, chosen by whether the screen has room for a rail. On a tablet the seat
    /// list is permanently up beside the board. On a phone the margin was given back to the board
    /// and there is no rail, so it is a panel the lives readout opens — which is item 10's answer as
    /// well as item 5's, and the reason both are one component: a lives readout that expands into
    /// the full seat list is what that report asked for.
    ///
    /// The phone half matters more than it looks. Until it existed this component stood down
    /// completely without a rail, so item 5 was quietly tablet-only and a phone player could not see
    /// another seat's lives at all except by finishing the match.
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

        /// <summary>
        /// Whether the phone's tap-to-open seat panel is showing.
        /// </summary>
        /// <remarks>
        /// Static because the control that toggles it lives in <see cref="HudView"/> — the lives
        /// readout — and the two components have no reference to each other. Same shape as
        /// <c>RuntimeUiChrome.ModalScreenActive</c>, which exists for the same reason.
        ///
        /// Only consulted where there is no side rail. On a tablet the seat list is permanently in
        /// the rail, so a toggle would be a control that hides information the screen has room for.
        /// </remarks>
        public static bool PanelOpen { get; set; }

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

            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            // Two homes, chosen by whether the screen has room for a rail. On a tablet the seat
            // list is always up beside the board; on a phone it is a panel the lives readout opens,
            // because there is nowhere to put it permanently and a phone player had no way to see
            // the other seats at all before this.
            var hasRail = MobileViewportLayout.HasSideRails;
            if (!hasRail && !PanelOpen)
            {
                return;
            }

            var rail = hasRail
                ? MobileViewportLayout.SideRailRect(rightSide: true)
                : PhonePanelRect();
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

            // An opaque backing under the panel when it floats over the board. The rail version
            // sits on empty margin and can afford to be translucent; this one lands on a live lane,
            // and at PanelInk's alpha the creeps and flow arrows read straight through the seat
            // rows. Alpha composites in LINEAR here, so a value that looks opaque in sRGB terms
            // arrives noticeably lighter — the same trap the shell stylesheet documents.
            if (!hasRail)
            {
                Fill(panel, new Color(0.016f, 0.027f, 0.047f, 1f));
            }

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
        /// Where the seat panel sits on a screen with no rail.
        /// </summary>
        /// <remarks>
        /// Anchored under the HUD strip rather than centred, so it hangs from the control that
        /// opened it and leaves the bottom of the board — and the build and send launchers — clear.
        /// </remarks>
        private static Rect PhonePanelRect()
        {
            var frame = MobileViewportLayout.SafeScreenRect();
            var scale = MobileViewportLayout.UiScale();
            var width = Mathf.Min(frame.width - 24f * scale, 300f * scale);
            return new Rect(
                frame.center.x - width * 0.5f,
                frame.y + 76f * scale,
                width,
                Mathf.Min(frame.height * 0.62f, 460f * scale));
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
