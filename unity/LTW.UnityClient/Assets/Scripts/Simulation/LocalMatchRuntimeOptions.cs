#nullable enable

using LTW.Simulation.Bridge;

namespace LTW.UnityClient.Simulation
{
    public static class LocalMatchRuntimeOptions
    {
        public static LocalMatchOptions PendingOptions { get; set; } = LocalMatchOptions.Default;

        private static LocalMatchOptions? optionsBeforePractice;

        /// <summary>Whether <see cref="PendingOptions"/> currently describes a practice match.</summary>
        public static bool PracticePending => optionsBeforePractice is not null;

        /// <summary>
        /// Swaps <see cref="PendingOptions"/> for a copy with every bot passive, remembering what
        /// was there so <see cref="LeavePractice"/> can put it back.
        /// </summary>
        /// <remarks>
        /// Swap-and-restore rather than "restore to Default" on purpose. The editor batch runner
        /// writes seeded options here before it loads the scene; a practice exit that wrote
        /// <c>LocalMatchOptions.Default</c> would silently change the seed of every later match in
        /// that run without any error to point at it. Idempotent so a second entry cannot capture
        /// the practice copy as the thing to restore.
        /// </remarks>
        public static void EnterPractice()
        {
            if (PracticePending)
            {
                return;
            }

            optionsBeforePractice = PendingOptions;
            PendingOptions = PendingOptions.WithAllBotsPassive();
        }

        /// <summary>Restores the options that were pending before <see cref="EnterPractice"/>.</summary>
        public static void LeavePractice()
        {
            if (optionsBeforePractice is null)
            {
                return;
            }

            PendingOptions = optionsBeforePractice;
            optionsBeforePractice = null;
        }
    }
}
