using ToNSaveManager.Models;
using ToNSaveManager.Models.Index;
using ToNSaveManager.Models.Stats;

namespace ToNSaveManager.Utils.LogParser {
    /// <summary>
    /// Reads the VRChat logs and turns them into game state, stats and save entries.
    /// </summary>
    internal static class ToNLogHandler {
        internal const string HomeWorldID = "wrld_a61cdabe-1218-4287-9ffc-2a4d1414e5bd";
        internal static readonly LogWatcher<ToNLogContext> LogWatcher = new LogWatcher<ToNLogContext>(HomeWorldID);

        /// <summary>A log line without its timestamp prefix, raised before Core handles it.</summary>
        internal static event Action<string, ToNLogContext>? LineRead;
        /// <summary>
        /// Raised on every log tick, after the save data was exported.
        /// Runs before the stats are exported, so <see cref="ToNStats.IsModified"/> still reports this tick's changes.
        /// </summary>
        internal static event Action? Tick;
        /// <summary>A new log collection was added to the save data. Arguments: collection, index in the save data.</summary>
        internal static event Action<History, int>? HistoryAdded;
        /// <summary>A save code was added to a collection. Arguments: collection, entry, index in the collection.</summary>
        internal static event Action<History, Entry, int>? EntryAdded;
        /// <summary>A save code was stored while playing, not while reading older logs.</summary>
        internal static event Action<Entry>? SaveStored;
        /// <summary>A save code should be copied to the clipboard. Arguments: entry, play the copy sound.</summary>
        internal static event Action<Entry, bool>? CopyRequested;
        /// <summary>The local player died in the running round.</summary>
        internal static event Action? PlayerDied;
        /// <summary>Joined a new instance, the lobby stats were cleared.</summary>
        internal static event Action? LobbyCleared;

        /// <summary>
        /// Starts reading the logs. Call this on the UI thread, all events are then raised on that thread.
        /// </summary>
        internal static void Start() {
            LogWatcher.OnLine += LogWatcher_OnLine;
            LogWatcher.OnTick += LogWatcher_OnTick;
            LogWatcher.Interval = Settings.Get.LogUpdateRate;
            LogWatcher.Start();
        }

        const string SaveStartKeyword = "[START]";
        const string SaveEndKeyword = "[END]";
        const string SaveInitKeyword = "[TERRORS SAVE CODE CREATED";
        const string SaveLoadedKeyword = "Loaded Data Successfully";

        const string ROUND_OPTIN_KEYWORD = "opted in";
        const string ROUND_OPTOUT_KEYWORD = "Player respawned";

        const string ROUND_WON_KEYWORD = "Player Won";
        const string ROUND_LOST_KEYWORD = "Player lost,";
        const string ROUND_OVER_KEYWORD = "RoundOver";
        const string ROUND_DEATH_KEYWORD = "You died.";
        const string ROUND_REBORN_KEYWORD = "LOL JK, REBORN!";
        const string ROUND_TEROR_GIGABYTE = "The Gigabytes have come.";
        const string ROUND_PAGE_FOUND = "Page Collected - ";

        const string ROUND_DEATH_MSG_KEYWORD = "[DEATH][";

        const string ROUND_SABO_END = "Clearing Items // Ran Item Removal";
        const string ROUND_IS_SABO = "You are the sussy baka of cringe naenae legend";

        const string KILLER_MATRIX_KEYWORD = "Killers have been set - ";
        const string KILLER_MATRIX_UNKNOWN = "Killers is unknown - ";
        const string KILLER_MATRIX_REVEAL = "Killers have been revealed - ";
        const string KILLER_ROUND_TYPE_KEYWORD = " // Round type is ";

        const string ROUND_MAP_LOCATION = "This round is taking place at ";
        const string ROUND_MAP_RTYPE = " and the round type is ";
        const string ROUND_MAP_SWAPPED = "Solstice has swapped the map to ";
        const string ROUND_MAP_SPECIAL = "Dring King's Citadel";

        private static void LogWatcher_OnLine(object? sender, LogWatcher<ToNLogContext>.OnLineArgs e) {
            DateTime timestamp = e.Timestamp;
            ToNLogContext context = e.Context;

            string line = e.Content.Substring(34);
            LineRead?.Invoke(line, context);

            if (HandleSaveCode(line, timestamp, context) ||
                HandleTerrorIndex(line, timestamp, context) ||
                HandleStatCollection(line, timestamp, context)) { }
        }

        private static void LogWatcher_OnTick(object? sender, EventArgs e) {
            if (Settings.Get.CopyOnSave && AppCore.Started)
                CopyRecent(false);

            AppCore.SaveData.Export();

            Tick?.Invoke();
            WriteChanges();
        }

        /// <summary>Writes the round info files and the stats, if anything changed.</summary>
        internal static void WriteChanges() {
            if (Settings.Get.RoundInfoToFile) {
                foreach (var template in Settings.Get.RoundInfoTemplates) template.WriteToFile();
            }

            ToNStats.Export(false);
        }

        private static bool HandleSaveCode(string line, DateTime timestamp, ToNLogContext context) {
            if (line.StartsWith(SaveLoadedKeyword, StringComparison.InvariantCulture)) {
                context.HasLoadedSave = true;
                return true;
            }

            /*
            int index = line.IndexOf(SaveInitKeyword, StringComparison.InvariantCulture);
            if (index > -1) {
                context.SaveCodeCreated = true;
                return true;
            }
            */

            if (!line.StartsWith(SaveStartKeyword)) return false;

            int index = line.IndexOf(SaveStartKeyword, StringComparison.InvariantCulture);
            if (index < 0) return false;

            index += SaveStartKeyword.Length;

            int end = line.IndexOf(SaveEndKeyword, index, StringComparison.InvariantCulture);
            if (end < 0) return false;
            end -= index;

            string save = line.Substring(index, end);
            if (string.IsNullOrEmpty(save) || save.Length < 1000) return false;

            AddLogEntry(context.DateKey, save, timestamp, context);
            return true;
        }

        private static bool HandleTerrorIndex(string line, DateTime timestamp, ToNLogContext context) {
            // Handle participation
            bool isOptedIn = line.StartsWith(ROUND_OPTIN_KEYWORD);
            if (isOptedIn || line.StartsWith(ROUND_OPTOUT_KEYWORD)) {
                context.SetOptedIn(isOptedIn);
                context.SetRoundResult(ToNRoundResult.R); // Respawned
                return true;
            }

            // Handle map location
            if (line.StartsWith(ROUND_MAP_LOCATION)) {
                int index = line.LastIndexOf("(", StringComparison.InvariantCulture) + 1;
                if (index <= 0) return true;

                int indexEnd = line.IndexOf(")", index, StringComparison.InvariantCulture);
                if (indexEnd < 0 || index >= indexEnd) return true;

                int length = (line.IndexOf(")", index, StringComparison.InvariantCulture) - index);

                string id_str = line.Substring(index, length);
                string name = line.Substring(ROUND_MAP_LOCATION.Length, index - ROUND_MAP_LOCATION.Length - 1).Trim();

                ToNIndex.Map map = ToNIndex.Map.Empty;
                if (name.Equals(ROUND_MAP_SPECIAL)) {
                    // Check special
                    map = ToNIndex.Instance.GetMap(name);
                } else if (int.TryParse(id_str, out int mapIndex)) {
                    // Check by ID
                    map = ToNIndex.Instance.GetMap(mapIndex);
                }

                if (map.IsEmpty) {
                    // Check by name
                    Logger.Warning($"Map ID check ({id_str}) failed, trying map name instead: {name}");
                    map = ToNIndex.Instance.GetMap(name);
                }

                context.SetLocation(map);

                TerrorMatrix matrix;

                if (map.Id == 254) {
                    matrix = new TerrorMatrix("RUN", byte.MaxValue, byte.MaxValue, byte.MaxValue);
                } else {
                    matrix = new TerrorMatrix(line.Substring(index + length + ROUND_MAP_RTYPE.Length).Trim());
                }

                matrix.MapID = map.Id;
                context.SetTerrorMatrix(matrix);

                return true;
            }

            // TODO: REVISIT THIS LATER PLEASE (original comment by Kittenji)
            if (line.StartsWith(ROUND_IS_SABO)) {
                context.SetIsKiller(true);
                context.SetTerrorMatrix(new TerrorMatrix(ToNRoundType.Sabotage) { IsSaboteur = true });
                return true;
            }

            if (!context.Location.IsEmpty) {
                bool isUnknown = line.StartsWith(KILLER_MATRIX_UNKNOWN); // Killers is unknown - 
                bool isRevealed = line.StartsWith(KILLER_MATRIX_REVEAL); // Killers have been revealed - 
                bool isSet = line.StartsWith(KILLER_MATRIX_KEYWORD);
                if (isUnknown || isRevealed || isSet) { // Killers have been set - 
                    int index = isRevealed ? KILLER_MATRIX_REVEAL.Length : KILLER_MATRIX_KEYWORD.Length;
                    int rndInd = line.IndexOf(KILLER_ROUND_TYPE_KEYWORD, index, StringComparison.InvariantCulture);
                    if (rndInd < 0) return true;

                    string roundType = line.Substring(rndInd + KILLER_ROUND_TYPE_KEYWORD.Length).Trim();
                    int[] killerMatrix = new int[3];

                    if (isUnknown) {
                        killerMatrix[0] = killerMatrix[1] = killerMatrix[2] = byte.MaxValue;
                    } else {
                        string[] kMatrixRaw = line.Substring(index, rndInd - index).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        for (int i = 0; i < kMatrixRaw.Length; i++) {
                            killerMatrix[i] = int.TryParse(kMatrixRaw[i], out index) ? index : -1;
                        }
                    }

                    context.SetTerrorMatrix(new TerrorMatrix(roundType, killerMatrix) { IsUnknown = isUnknown, IsRevealed = isRevealed });
                    return true;
                }
            }

            if (!context.Terrors.IsEmpty) {
                // Track round participation results / Supports live build maybe
                if (line.StartsWith(ROUND_OVER_KEYWORD)) {
                    if (context.IsRecent) ToNStats.AddRound(context.IsAlive, AppCore.Started);

                    if (context.IsOptedIn) {
                        context.SetRoundResult(context.IsAlive ? (context.IsReborn ? ToNRoundResult.B : ToNRoundResult.W) : ToNRoundResult.L);
                        context.SaveSummary();
                    }

                    context.SetIsKiller(false);
                    context.SetTerrorMatrix(TerrorMatrix.Empty);
                    context.SetLocation(ToNIndex.Map.Empty);
                    return true;
                }

                if (line.StartsWith(ROUND_DEATH_KEYWORD)) {
                    context.SetRoundResult(ToNRoundResult.L);
                    context.SetIsAlive(false);
                    if (context.IsRecent) PlayerDied?.Invoke();
                    return true;
                }

                if (line.StartsWith(ROUND_REBORN_KEYWORD)) {
                    // God dammit beyond
                    context.SetRoundResult(ToNRoundResult.B);
                    context.SetIsAlive(true);
                    context.SetIsReborn(true);
                    return true;
                }

                TerrorMatrix matrix = context.Terrors;
                if (matrix.RoundType == ToNRoundType.Classic && matrix.MapID == 2 && line.StartsWith(ROUND_TEROR_GIGABYTE)) {
                    Logger.Debug("Correcting Round Type to GIGABYTE.");
                    matrix.RoundType = ToNRoundType.GIGABYTE;
                    matrix.Terrors = [new(1, ToNIndex.TerrorGroup.Events)];
                    matrix.TerrorCount = 1;

                    context.SetTerrorMatrix(matrix);
                    return true;
                }

                if (matrix.RoundType == ToNRoundType.Eight_Pages && line.StartsWith(ROUND_PAGE_FOUND)) {
                    char pageChar = line[ROUND_PAGE_FOUND.Length];
                    if (int.TryParse(pageChar.ToString(), out int pages)) ToNGameState.SetPageCount(pages);
                    return true;
                }

                // Handle map swap
                if (line.StartsWith(ROUND_MAP_SWAPPED) && matrix.Length > 0) {
                    string id_str = line.Substring(ROUND_MAP_SWAPPED.Length).Trim();
                    if (int.TryParse(id_str, out int mapIndex)) {
                        ToNIndex.Map map = ToNIndex.Instance.GetMap(mapIndex);
                        Logger.Debug($"Map swapped to: {map} ({mapIndex})");
                        if (context.IsRecent) {
                            ToNGameState.SetLocation(map);
                            var info = matrix[0];
                            info.Phase = info.Phase + 1;
                            matrix[0] = info;
                            ToNGameState.SetTerrorMatrix(matrix, true);
                            Logger.Debug("Map Phase: " + info.Phase);
                        }
                    }

                    return true;
                }

                for (int i = matrix.StartIndex; i < matrix.Length; i++) {
                    int j;
                    var info = matrix[i];
                    if (info.IsEmpty || info.Value.IsEmpty) continue;

                    ToNIndex.Terror terror = info.Value;
                    if (terror.Phases != null && terror.Phases.Length > 0) {
                        for (j = 0; j < terror.Phases.Length; j++) {
                            var ph = terror.Phases[j];
                            if (line.StartsWith(ph.Keyword)) {
                                info.Phase = j + 1;
                                matrix[i] = info;

                                Logger.Log($"Terror {terror} changed to phase {j + 1}.");
                                context.SetTerrorMatrix(matrix);
                                return true;
                            }
                        }
                    }

                    if (terror.Encounters != null && terror.Encounters.Length > 0) {
                        for (j = 0; j < terror.Encounters.Length; j++) {
                            ToNIndex.Terror.Encounter encounter = terror.Encounters[j];
                            if ((encounter.RoundType == ToNRoundType.Intermission || matrix.RoundType == encounter.RoundType) && line.StartsWith(encounter.Keyword)) {
                                info.Encounter = j;
                                matrix[i] = info;

                                Logger.Log($"Terror {terror} changed to encounter {j}.");
                                context.SetTerrorMatrix(matrix);
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        const string STAT_STUN_LANDED = " landed a stun!";
        const string STAT_STUN_TARGET = " was stunned.";
        const string STAT_HIT = "Hit - ";

        private static bool HandleStatCollection(string line, DateTime timestamp, ToNLogContext context) {
            if (!context.IsRecent) return false;

            if (line.Contains(LogWatcher<ToNLogContext>.LocationKeyword)) {
                ToNStats.ClearLobby();
                ToNStats.ClearRound();
                LobbyCleared?.Invoke();
                return true;
            }

            if (line.Contains(STAT_STUN_LANDED)) {
                ToNStats.AddStun(true, AppCore.Started);
                return true;
            }

            if (line.Contains(STAT_STUN_TARGET)) {
                ToNStats.AddStun(false, AppCore.Started);
                return true;
            }

            if (line.StartsWith(STAT_HIT)) {
                string ammount = line.Substring(STAT_HIT.Length).Trim();
                if (int.TryParse(ammount, out int result)) ToNGameState.AddDamage(result);
                return true;
            }

            return false;
        }

        #region Data
        private static Entry? RecentData;

        private static void AddLogEntry(string dateKey, string content, DateTime timestamp, ToNLogContext context) {
            History? collection = AppCore.SaveData[dateKey];
            if (collection == null) {
                collection = new History(dateKey);
                collection.SetLogContext(context);

                int index = AppCore.SaveData.Add(collection);
                HistoryAdded?.Invoke(collection, index);
            }

            if (string.IsNullOrEmpty(context.DisplayName) && !string.IsNullOrEmpty(collection.DisplayName)) {
                context.DisplayName = collection.DisplayName;
            }

            int ind = collection.Add(content, timestamp, out Entry? entry);
            if (ind < 0) return; // Not added, duplicate

#pragma warning disable CS8604, CS8602 // Nullability is handled along with the return value of <History>.Add
            entry.PlayerCount = context.Players.Count;
            entry.Parent = collection;

            if (Settings.Get.SaveNames) entry.Players = context.GetRoomString();
            if (Settings.Get.SaveRoundInfo) {
                entry.RResult = context.IsLeavingRoom ? ToNRoundResult.D : context.Summary.Result;

                if (!context.Summary.IsEmpty) {
                    TerrorMatrix killers = context.Summary.Terrors;
                    entry.RT = killers.RoundType;
                    entry.TD = killers.Terrors;

                    if (Settings.Get.SaveRoundNote && !string.IsNullOrEmpty(context.Summary.Note)) {
                        entry.Note = context.Summary.Note;
                    }

                    var map = context.Summary.Map;
                    entry.MapID = map.IsEmpty ? -1 : map.Id;

                    context.ClearSummary();
                }

                if (!context.HasLoadedSave) entry.Pre = true;
            }

            // if (!context.IsHomeWorld) entry.Note = "(BETA) " + entry.Note;

            EntryAdded?.Invoke(collection, entry, ind);

            SetRecent(entry);
#pragma warning restore CS8604, CS8602
            AppCore.SaveData.SetDirty();

            if (context.Initialized) {
                SaveStored?.Invoke(entry);
                XSOverlay.SendSaveNotification();
            }
        }

        private static void SetRecent(Entry entry) {
            if (entry == null) return;
            if (RecentData == null || RecentData.Timestamp < entry.Timestamp) {
                entry.Fresh = true;
                RecentData = entry;
            }
        }

        internal static void CopyRecent(bool force) {
            if (!Settings.Get.AutoCopy || RecentData == null || (!RecentData.Fresh && !force)) return;

            CopyRequested?.Invoke(RecentData, false);
            RecentData.Fresh = false;
        }

        internal static void CopyRecentData() {
            Entry? entry = AppCore.SaveData.FindRecentEntry();
            if (entry != null) CopyRequested?.Invoke(entry, true);
        }
        #endregion
    }
}
