using System.Text.RegularExpressions;
using ToNSaveManager.Models;
using ToNSaveManager.Models.Stats;
using ToNSaveManager.Utils.API;
using ToNSaveManager.Utils.Discord;
using ToNSaveManager.Utils.LogParser;
using ToNSaveManager.Utils.OpenRGB;

namespace ToNSaveManager.Utils {
    /// <summary>
    /// Forwards Core events to the integrations (WebSocket, OSC, Discord, OpenRGB).
    /// </summary>
    internal static class Integrations {
        /// <summary>Called by <see cref="AppCore.Initialize"/>, before any log is read.</summary>
        internal static void Register() {
            ToNStats.StatChanged += WebSocketAPI.EventStats.Send;

            ToNGameState.Clearing += WebSocketAPI.ClearBuffer;
            ToNGameState.TimestampReset += DSRichPresence.UpdateTimestamp;
            ToNGameState.Changed += OnGameStateChanged;
            ToNGameState.Damaged += OnDamaged;

            ToNLogContext.InstanceJoined += (instanceID, isToNWorld) => {
                WebSocketAPI.SendValue("INSTANCE", instanceID);
                DSRichPresence.SetInstanceID(instanceID, isToNWorld);
            };
            ToNLogContext.InstanceLeft += () => {
                DSRichPresence.SetInstanceID(string.Empty, false);
                DSRichPresence.Initialize();
            };
            ToNLogContext.PlayerJoined += WebSocketAPI.EventPlayerJoin.Send;
            ToNLogContext.Awake += () => LilOSC.SetChatboxMessage(string.Empty);

            ToNLogHandler.LineRead += OnLineRead;
            ToNLogHandler.Tick += OnTick;
            ToNLogHandler.SaveStored += OnSaveStored;
            ToNLogHandler.PlayerDied += () => LilOSC.SetDamage(byte.MaxValue);
            ToNLogHandler.LobbyCleared += LilOSC.UpdateChatboxContent;
        }

        /// <summary>Starts the integrations, call it after the log handler started.</summary>
        internal static void Start() {
            LilOSC.Initialize();
            WebSocketAPI.Initialize();
            DSRichPresence.Initialize(true);
            LilOSC.UpdateChatboxContent();
            OpenRGBControl.SetTerrorMatrix(OpenRGBControl.Terrors);
        }

        internal static void Stop() {
            OSCLib.Dispose();
        }

        #region Log Handling
        const string ROUND_DEATH_MSG_KEYWORD = "[DEATH][";
        const string EVENT_MASTER_CHANGE = "[Behaviour] OnMasterClientSwitched";

        static readonly Regex PATTERN_ITEM_ACTIVATED = new Regex(@"\[[\w ]+\] (Activated!|Deactivated!)", RegexOptions.Compiled);

        private static void OnLineRead(string line, ToNLogContext context) {
            if (context.IsRecent && AppCore.Started) HandleIntegrationLine(line, context);

            if (Settings.Get.WebSocketEnabled && Settings.Get.WebTrackerCompatibility && context.IsRecent) {
                WebSocketAPI.OnReadLine(line);
            }
        }

        private static bool HandleIntegrationLine(string line, ToNLogContext context) {
            if (Settings.Get.WebSocketEnabled && line.StartsWith(ROUND_DEATH_MSG_KEYWORD)) {
                int index = line.IndexOf(']', ROUND_DEATH_MSG_KEYWORD.Length);
                int length = index - ROUND_DEATH_MSG_KEYWORD.Length;
                if (length < 1) return true;

                string name = line.Substring(ROUND_DEATH_MSG_KEYWORD.Length, length);
                string cont = line.Substring(index + 2).Trim();
                WebSocketAPI.EventDeath.Send(name, cont, context.DisplayName == name);

                if (Settings.Get.OSCDeathEvent) {
                    int deathId = Settings.Get.GetDeathID(name);
                    if (deathId > 0) LilOSC.SetDeathID(deathId);
                    else LilOSC.SetDeathID();
                }
                return true;
            }

            if (Settings.Get.OSCEnabled) {
                if (Settings.Get.OSCMasterChange && line.StartsWith(EVENT_MASTER_CHANGE)) {
                    LilOSC.SendHostChange();
                    WebSocketAPI.SendValue<object?>("MASTER_CHANGE", null, false);
                    return true;
                }

                if (PATTERN_ITEM_ACTIVATED.IsMatch(line)) {
                    LilOSC.SetItemStatus(!line.Contains("Deactivated!"));
                    return true;
                }
            }

            return false;
        }

        private static void OnTick() {
            DSRichPresence.Send();

            if (Settings.Get.OSCMessageInfoTemplate.IsModified)
                LilOSC.UpdateChatboxContent();

            LilOSC.SendData();
            WebSocketAPI.SendEventUpdate();
        }

        private static void OnSaveStored(Entry entry) {
            DSWebHook.Send(entry);

            if (Settings.Get.WebSocketEnabled) WebSocketAPI.SendValue<string?>("SAVED", entry.Content, false);
        }
        #endregion

        private static void OnGameStateChanged(GameStateProperty property) {
            switch (property) {
                case GameStateProperty.Emulated:
                    DSRichPresence.SetDirty();
                    break;

                case GameStateProperty.PlayerCount:
                    DSRichPresence.SetPlayerCount(ToNGameState.PlayerCount);
                    break;

                case GameStateProperty.RoundActive:
                    LilOSC.SetDirty();
                    DSRichPresence.SetDirty();
                    WebSocketAPI.SendValue("ROUND_ACTIVE", ToNGameState.IsRoundActive);
                    break;

                case GameStateProperty.ActiveInRound:
                    if (ToNGameState.IsActiveInRound) LilOSC.SetChatboxMessage(string.Empty);
                    else LilOSC.UpdateChatboxContent();
                    break;

                case GameStateProperty.Alive:
                    OpenRGBControl.SetIsAlive();
                    LilOSC.SetDirty();
                    DSRichPresence.SetDirty();
                    WebSocketAPI.SendValue("ALIVE", ToNGameState.IsAlive);
                    break;

                case GameStateProperty.Reborn:
                    LilOSC.SetDirty();
                    WebSocketAPI.SendValue("REBORN", ToNGameState.IsReborn);
                    break;

                case GameStateProperty.OptedIn:
                    LilOSC.SetDirty();
                    WebSocketAPI.SendValue("OPTED_IN", ToNGameState.IsOptedIn);
                    break;

                case GameStateProperty.Saboteur:
                    WebSocketAPI.SendValue("IS_SABOTEUR", ToNGameState.IsAlive);
                    break;

                case GameStateProperty.Terrors:
                    OpenRGBControl.SetTerrorMatrix(ToNGameState.Terrors);
                    DSRichPresence.SetDirty();
                    WebSocketAPI.EventTerror.Send(ToNGameState.Terrors);
                    LilOSC.SetDirty();
                    break;

                case GameStateProperty.TerrorsSilent:
                    LilOSC.SetDirty();
                    break;

                case GameStateProperty.RoundType:
                    DSRichPresence.SetRoundType(ToNGameState.RoundType);
                    WebSocketAPI.EventRoundType.Send(ToNGameState.RoundType);
                    break;

                case GameStateProperty.Item:
                    LilOSC.SetDirty();
                    WebSocketAPI.EventItem.Send(ToNGameState.Item);
                    break;

                case GameStateProperty.Location:
                    LilOSC.SetDirty();
                    DSRichPresence.SetDirty();
                    WebSocketAPI.EventLocation.Send(ToNGameState.Location);
                    break;

                case GameStateProperty.PageCount:
                    LilOSC.SetDirty();
                    DSRichPresence.SetDirty();
                    WebSocketAPI.SendValue("PAGE_COUNT", ToNGameState.PageCount);
                    break;
            }
        }

        private static void OnDamaged(int damage) {
            LilOSC.SetDamage(damage);
            OpenRGBControl.SetDamaged();
            WebSocketAPI.SendValue("DAMAGED", damage);
        }
    }
}
