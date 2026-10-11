using ToNSaveManager.Models.Index;
using ToNSaveManager.Models.Stats;

namespace ToNSaveManager.Utils {
    internal enum GameStateProperty {
        Emulated,
        PlayerCount,
        DisplayName,
        InstanceURL,
        RoundActive,
        ActiveInRound,
        Alive,
        Reborn,
        OptedIn,
        Saboteur,
        Terrors,
        /// <summary>Terrors changed silently</summary>
        TerrorsSilent,
        RoundType,
        Item,
        Location,
        PageCount
    }

    internal static class ToNGameState {
        /// <summary>Raised after a property changed. Read the new value from <see cref="ToNGameState"/>.</summary>
        internal static event Action<GameStateProperty>? Changed;
        /// <summary>Raised when the local player takes damage.</summary>
        internal static event Action<int>? Damaged;
        /// <summary>Raised before <see cref="ClearStates"/> resets all values.</summary>
        internal static event Action? Clearing;
        /// <summary>Raised when a round location starts or ends, for elapsed time displays.</summary>
        internal static event Action? TimestampReset;


        public static bool IsEmulated { get; private set; }
        public static bool IsAlive { get; private set; } = true;
        public static bool IsReborn { get; private set; } = false;
        public static bool IsRoundActive { get; private set; } = false;
        public static bool IsSaboteour { get; private set; }
        public static bool IsOptedIn { get; private set; }
        public static int PageCount { get; private set; }

        public static ToNRoundType RoundType { get; private set; } = ToNRoundType.Intermission;
        public static TerrorMatrix Terrors { get; private set; } = TerrorMatrix.Empty;
        public static ToNIndex.Map Location { get; private set; } = ToNIndex.Map.Empty;
        public static ToNIndex.Item Item { get; private set; } = ToNIndex.Item.Empty;

        // Instance Information
        public static int PlayerCount { get; private set; }
        public static string DisplayName { get; private set; } = "Unknown";
        public static string DiscordName { get; private set; } = "Unknown";
        public static string InstanceURL { get; private set; } = string.Empty;

        /// <summary>
        /// True while the local player takes part in the running round.
        /// Becomes false when the round ends or the player dies.
        /// </summary>
        public static bool IsActiveInRound { get; private set; }

        public static void ClearStates() {
            Clearing?.Invoke();
            SetEmulated(false);
            SetPlayerCount(0);
            SetAlive(true);
            SetRoundActive(false);
            SetOptedIn(false);
            SetKiller(false);
            SetTerrorMatrix(TerrorMatrix.Empty);
            SetLocation(ToNIndex.Map.Empty);
            SetItem(ToNIndex.Item.Empty);
            SetRoundType(ToNRoundType.Intermission);
            SetPageCount(0);
        }

        public static void SetDisplayName(string displayName, bool isDiscord) {
            if (isDiscord) DiscordName = displayName;
            else DisplayName = displayName;

            ToNStats.SetDisplayName(displayName, isDiscord);
            Changed?.Invoke(GameStateProperty.DisplayName);
        }

        public static void SetInstanceURL(string instanceURL) {
            InstanceURL = instanceURL;
            ToNStats.SetInstanceURL(instanceURL);
            Changed?.Invoke(GameStateProperty.InstanceURL);
        }

        public static void SetPlayerCount(int playerCount) {
            PlayerCount = playerCount;
            ToNStats.AddPlayerCount(PlayerCount);
            Changed?.Invoke(GameStateProperty.PlayerCount);
        }

        public static void SetEmulated(bool isEmulated) {
            IsEmulated = isEmulated;
            Changed?.Invoke(GameStateProperty.Emulated);
        }

        public static void SetRoundActive(bool roundActive) {
            IsRoundActive = roundActive;

            ToNStats.AddIsStarted(roundActive);
            SetActiveInRound(roundActive);
            SetPageCount(0);

            Changed?.Invoke(GameStateProperty.RoundActive);
        }

        public static void SetAlive(bool isAlive) {
            IsAlive = isAlive;

            ToNStats.AddIsAlive(isAlive);
            if (!isAlive) SetActiveInRound(false);

            Changed?.Invoke(GameStateProperty.Alive);
        }

        private static void SetActiveInRound(bool active) {
            if (IsActiveInRound == active) return;

            IsActiveInRound = active;
            if (IsActiveInRound) ToNStats.ClearRound();

            Changed?.Invoke(GameStateProperty.ActiveInRound);
        }

        public static void SetReborn(bool reborn) {
            IsReborn = reborn;
            ToNStats.AddIsReborn(reborn);
            Changed?.Invoke(GameStateProperty.Reborn);
        }

        public static void SetOptedIn(bool isOptedIn) {
            IsOptedIn = isOptedIn;
            ToNStats.AddOptedIn(IsOptedIn);
            Changed?.Invoke(GameStateProperty.OptedIn);
        }

        public static void SetKiller(bool isKiller) {
            IsSaboteour = isKiller;
            ToNStats.AddIsKiller(isKiller);
            Changed?.Invoke(GameStateProperty.Saboteur);
        }

        public static void SetTerrorMatrix(TerrorMatrix matrix, bool silent = false) {
            Terrors = matrix;

            if (silent) {
                Changed?.Invoke(GameStateProperty.TerrorsSilent);
                return;
            }

            SetRoundType(Terrors.RoundType);
            ToNStats.AddTerrors(Terrors);
            Changed?.Invoke(GameStateProperty.Terrors);
        }

        public static void SetRoundType(ToNRoundType roundType) {
            if (RoundType == roundType) return;
            RoundType = roundType;
            Changed?.Invoke(GameStateProperty.RoundType);
        }

        public static void SetItem(ToNIndex.Item item) {
            Item = item;
            ToNStats.AddItem(item);
            Changed?.Invoke(GameStateProperty.Item);
        }

        public static void SetLocation(ToNIndex.Map location) {
            if (Location.IsEmpty != location.IsEmpty)
                ResetTimestamp();

            Location = location;
            ToNStats.AddLocation(Location, !Terrors.IsEmpty);
            Changed?.Invoke(GameStateProperty.Location);
        }

        public static void SetPageCount(int pages) {
            if (PageCount == pages) return;

            PageCount = pages;
            ToNStats.AddPageCount(pages);
            Changed?.Invoke(GameStateProperty.PageCount);
        }

        public static void ResetTimestamp() => TimestampReset?.Invoke();

        // Unified damage event
        public static void AddDamage(int damage) {
            if (!IsEmulated) ToNStats.AddDamage(damage, AppCore.Started);
            Damaged?.Invoke(damage);
        }
    }
}
