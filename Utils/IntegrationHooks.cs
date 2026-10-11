using ToNSaveManager.Models.Stats;
using ToNSaveManager.Utils.API;
using ToNSaveManager.Utils.Discord;
using ToNSaveManager.Utils.OpenRGB;

namespace ToNSaveManager.Utils {
    /// <summary>
    /// Forwards Core events to the integrations (WebSocket, OSC, Discord, OpenRGB).
    /// TODO: move into Core together with the integrations.
    /// </summary>
    internal static class IntegrationHooks {
        internal static void Register() {
            ToNStats.StatChanged += WebSocketAPI.EventStats.Send;

            ToNGameState.Clearing += WebSocketAPI.ClearBuffer;
            ToNGameState.TimestampReset += DSRichPresence.UpdateTimestamp;
            ToNGameState.Changed += OnGameStateChanged;
            ToNGameState.Damaged += OnDamaged;
        }

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
                    else StatsWindow.UpdateChatboxContent();
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
