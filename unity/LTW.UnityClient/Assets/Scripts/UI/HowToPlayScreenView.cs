#nullable enable

using LTW.UnityClient.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace LTW.UnityClient.UI
{
    /// <summary>
    /// How to play: five cards, one rule each, paged the way the codex is.
    /// </summary>
    /// <remarks>
    /// A plain class over the <c>screen-howto</c> block, for the same reason <see cref="CodexScreenView"/>
    /// is one: <see cref="ShellScreenView"/> shows and hides screens, this owns what one of them
    /// says. It reads nothing from the driver but two display facts (tick rate and the income
    /// interval, for the GROW card's caption) and never writes to it — the screen is reachable from
    /// the title, where there is no match to disturb, and it stays that way by having nothing to
    /// disturb one with.
    ///
    /// The copy is here rather than in the UXML because the eyebrow, headline, body and preview
    /// change together per card, and five near-identical blocks of markup switched by display is
    /// the kind of duplication that lets one card drift out of step with the other four.
    /// </remarks>
    internal sealed class HowToPlayScreenView
    {
        private enum Preview
        {
            Ward,
            Creep,
            Maze,
            Income,
            Lives
        }

        private readonly struct Card
        {
            public Card(string name, string headline, string body, Preview preview)
            {
                Name = name;
                Headline = headline;
                Body = body;
                Preview = preview;
            }

            public string Name { get; }

            public string Headline { get; }

            public string Body { get; }

            public Preview Preview { get; }
        }

        /// <summary>The ward on the BUILD card: the first, plainest tower on the roster.</summary>
        private const string BuildPreviewTower = "tower.arrow";

        /// <summary>The creep on the SEND card: the first, plainest creep on the roster.</summary>
        private const string SendPreviewCreep = "creep.runner";

        /// <summary>
        /// Every seat's opening lives.
        /// </summary>
        /// <remarks>
        /// Written down, which is the one thing this project's screens try never to do, because the
        /// simulation keeps it as <c>LocalVerticalSlice.StartingLives</c> — a private const with no
        /// public read — and there is no match snapshot to read it from on the title. If that
        /// constant gains a public surface, this should read it and the literal should go.
        /// </remarks>
        private const int StartingLives = 40;

        /// <summary>Tick rate assumed when no driver is present, matching the codex and the send dock.</summary>
        private const float FallbackTicksPerSecond = 4f;

        /// <summary>Income interval assumed when no driver is present, matching UnitySimulationDriver's own fallback.</summary>
        private const int FallbackIncomeIntervalTicks = 50;

        /// <summary>
        /// Where this screen's stage sits.
        /// </summary>
        /// <remarks>
        /// <see cref="UnitPreviewStage"/> parks itself at x = 0, a thousand units under the board,
        /// and the codex's stage is already there. Two stages on the same spot would share a fill
        /// light — the point light each one adds has a 12-unit range — so this one is slid sideways
        /// after it mounts, further than that range and than either camera's 20-unit far plane.
        /// </remarks>
        private static readonly Vector3 StageOffset = new(40f, 0f, 0f);

        private static readonly Card[] Cards =
        {
            new(
                "BUILD",
                "PLACE YOUR WARDS",
                "Towers go on your platforms, either side of the path. Tap BUILD, pick a ward, tap a cell.",
                Preview.Ward),
            new(
                "MAZE",
                "BEND THE PATH",
                "Creeps walk the shortest open path from gate to gate. Build across it and they take the long way round. Never seal it: a sealed path is refused.",
                Preview.Maze),
            new(
                "SEND",
                "ATTACK TO GROW",
                "Creeps are sent to the next player's lane, not yours. Every send costs gold and raises your income. Sending is how you attack and how you grow.",
                Preview.Creep),
            new(
                "GROW",
                "INCOME IS POWER",
                "Income pays out every few seconds. Line tiers and send tiers unlock on income, not gold — a bigger income buys stronger wards and tougher sends.",
                Preview.Income),
            new(
                "SURVIVE",
                "HOLD THE LINE",
                "40 lives. Each creep that reaches your leak gate costs one (Siege costs more). Last lane standing wins.",
                Preview.Lives)
        };

        /// <summary>
        /// The maze card's lane, 5 wide by 7 tall, as characters: '.' platform, '#' path, 'T' ward,
        /// 'G' gate.
        /// </summary>
        /// <remarks>
        /// Row 0 is the top. The path enters at the top gate, is turned right by the first ward,
        /// doubles back left under it, is turned again by the second, and leaves by the bottom
        /// gate — two bends from two cells, which is the whole of the lesson in one picture.
        /// </remarks>
        private static readonly string[] MazeRows =
        {
            "..G..",
            "..##.",
            "..T#.",
            ".###.",
            ".#T..",
            ".##..",
            "..G.."
        };

        private readonly VisualElement root;
        private readonly Transform stageParent;
        private readonly UnitySimulationDriver? simulationDriver;

        private readonly Label? eyebrow;
        private readonly Label? headline;
        private readonly Label? body;
        private readonly Label? position;
        private readonly VisualElement? stageElement;
        private readonly Label? stagePending;
        private readonly VisualElement? maze;
        private readonly VisualElement? income;
        private readonly Label? incomeCaption;
        private readonly VisualElement? lives;
        private readonly Label? livesValue;

        private UnitPreviewStage? stage;
        private int index;

        /// <summary>What the card was last built for, so a per-frame Refresh is nearly free.</summary>
        private int renderedIndex = -1;

        internal HowToPlayScreenView(VisualElement howToRoot, Transform stageOwner, UnitySimulationDriver? driver)
        {
            root = howToRoot;
            stageParent = stageOwner;
            simulationDriver = driver;

            eyebrow = root.Q<Label>("howto-eyebrow");
            headline = root.Q<Label>("howto-headline");
            body = root.Q<Label>("howto-body");
            position = root.Q<Label>("howto-position");
            stageElement = root.Q<VisualElement>("howto-stage");
            stagePending = root.Q<Label>("howto-stage-pending");
            maze = root.Q<VisualElement>("howto-maze");
            income = root.Q<VisualElement>("howto-income");
            incomeCaption = root.Q<Label>("howto-income-caption");
            lives = root.Q<VisualElement>("howto-lives");
            livesValue = root.Q<Label>("howto-lives-value");

            Wire("howto-prev", () => Step(-1));
            Wire("howto-next", () => Step(1));

            BuildMaze();

            if (livesValue != null)
            {
                livesValue.text = StartingLives.ToString();
            }
        }

        /// <summary>Called every frame the screen is up; rebuilds only when the card changed.</summary>
        internal void Refresh()
        {
            if (index == renderedIndex)
            {
                return;
            }

            renderedIndex = index;
            Render();
        }

        /// <summary>
        /// Releases the stage and returns to the first card when the screen is left.
        /// </summary>
        /// <remarks>
        /// Back to card one, unlike the codex, which keeps its place. A codex is a reference and a
        /// player returns to the entry they were reading; this is a five-step explanation, and
        /// reopening it on step four is a worse start than step one every time.
        ///
        /// The stage is cleared for the reason the codex's is: its camera renders every frame it is
        /// enabled, whether or not anything on screen reads the result.
        /// </remarks>
        internal void Close()
        {
            stage?.Clear();
            index = 0;
            renderedIndex = -1;
        }

        private void Wire(string name, System.Action handler)
        {
            var button = root.Q<Button>(name);
            if (button == null)
            {
                Debug.LogError($"HOWTO missing button '{name}' in the shell document.");
                return;
            }

            button.clicked += handler;
        }

        private void Step(int delta)
        {
            var count = Cards.Length;
            index = ((index + delta) % count + count) % count;
        }

        // ---------------------------------------------------------------------------- render

        private void Render()
        {
            var card = Cards[index];

            if (eyebrow != null)
            {
                eyebrow.text = $"{index + 1} / {Cards.Length} · {card.Name}";
            }

            if (headline != null)
            {
                headline.text = card.Headline;
            }

            if (body != null)
            {
                body.text = card.Body;
            }

            if (position != null)
            {
                position.text = $"{index + 1} / {Cards.Length}";
            }

            stageElement?.EnableInClassList("is-shown", card.Preview is Preview.Ward or Preview.Creep);
            maze?.EnableInClassList("is-shown", card.Preview == Preview.Maze);
            income?.EnableInClassList("is-shown", card.Preview == Preview.Income);
            lives?.EnableInClassList("is-shown", card.Preview == Preview.Lives);

            switch (card.Preview)
            {
                case Preview.Ward:
                    EnsureStage();
                    BindStage(stage != null && stage.ShowTower(BuildPreviewTower));
                    break;

                case Preview.Creep:
                    EnsureStage();
                    BindStage(stage != null && stage.ShowCreep(SendPreviewCreep));
                    break;

                case Preview.Income:
                    RenderIncomeCaption();
                    stage?.Clear();
                    break;

                default:
                    // The other cards draw nothing live; the stage is stopped so its camera does not
                    // keep rendering a ward nobody can see behind the maze grid.
                    stage?.Clear();
                    break;
            }
        }

        private void EnsureStage()
        {
            if (stage != null)
            {
                return;
            }

            var stageObject = new GameObject("LTW How To Play Preview Stage");
            stageObject.transform.SetParent(stageParent, false);
            stage = stageObject.AddComponent<UnitPreviewStage>();
        }

        /// <summary>
        /// Points the stage element at the render texture, or at the no-model state.
        /// </summary>
        /// <remarks>
        /// Bound on every render rather than once, exactly as the codex does: the texture does not
        /// exist until the stage has mounted its first subject. The sideways slide happens here
        /// too, after the first mount has put the stage at its default origin — see
        /// <see cref="StageOffset"/>. Camera and subject are both children of the stage, so moving
        /// it afterwards keeps the framing the mount computed.
        /// </remarks>
        private void BindStage(bool hasModel)
        {
            if (stage != null)
            {
                stage.transform.position = new Vector3(0f, stage.transform.position.y, 0f) + StageOffset;
            }

            if (stageElement == null)
            {
                return;
            }

            if (hasModel && stage?.Texture != null)
            {
                stageElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(stage.Texture));
            }
            else
            {
                stageElement.style.backgroundImage = StyleKeyword.None;
            }

            stagePending?.EnableInClassList("is-shown", !hasModel);
        }

        /// <summary>The GROW caption: the payout interval in seconds, read from the driver.</summary>
        private void RenderIncomeCaption()
        {
            if (incomeCaption == null)
            {
                return;
            }

            var intervalTicks = simulationDriver != null ? simulationDriver.IncomeIntervalTicks : FallbackIncomeIntervalTicks;
            var ticksPerSecond = simulationDriver != null ? simulationDriver.TicksPerSecond : FallbackTicksPerSecond;
            var seconds = ticksPerSecond > 0f ? intervalTicks / ticksPerSecond : 0f;
            incomeCaption.text = seconds > 0f
                ? $"PAYS OUT EVERY {seconds:0.#}s"
                : $"PAYS OUT EVERY {intervalTicks} TICKS";
        }

        /// <summary>Lays the maze card's cells out from <see cref="MazeRows"/>, one row element per row.</summary>
        private void BuildMaze()
        {
            if (maze == null)
            {
                return;
            }

            maze.Clear();

            for (var rowIndex = 0; rowIndex < MazeRows.Length; rowIndex++)
            {
                var row = new VisualElement { name = $"howto-maze-row-{rowIndex}", pickingMode = PickingMode.Ignore };
                row.AddToClassList("ltw-maze__row");

                var glyphs = MazeRows[rowIndex];
                for (var column = 0; column < glyphs.Length; column++)
                {
                    var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                    cell.AddToClassList("ltw-maze__cell");

                    switch (glyphs[column])
                    {
                        case '#':
                            cell.AddToClassList("ltw-maze__cell--path");
                            break;
                        case 'T':
                            cell.AddToClassList("ltw-maze__cell--tower");
                            break;
                        case 'G':
                            cell.AddToClassList("ltw-maze__cell--gate");
                            break;
                    }

                    row.Add(cell);
                }

                maze.Add(row);
            }
        }
    }
}
