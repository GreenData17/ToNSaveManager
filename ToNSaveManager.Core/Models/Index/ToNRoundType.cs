using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ToNSaveManager.Localization;

namespace ToNSaveManager.Models.Index {
    public enum ToNRoundType {
        Intermission = 0,

        // Normal
        Classic,         //  1
        Fog,             //  2
        Punished,        //  3
        Sabotage,        //  4
        Cracked,         //  5
        Bloodbath,       //  6
        Double_Trouble,  //  7 // Bloodbath - Two killers have same id
        EX,              //  8 // Bloodbath - All killers have same id
        Ghost,           //  9
        Unbound,         // 10

        Randomizer,      // 11
        Classic_exe,     // 12

        // Contains alternates
        Midnight           =  50,
        Alternate,         // 51
        Fog_Alternate,     // 52
        Ghost_Alternate,   // 53

        // Moons // Replace spaces with underscore
        Mystic_Moon  =  100,
        Blood_Moon,  // 101
        Twilight,    // 102
        Solstice,    // 103

        // Specials
        RUN,         // 104
        Eight_Pages, // 105 // Replace 8 with Eight
        // Events
        GIGABYTE,    // 106 // April Fools
        Cold_Night,  // 107 // Winterfest

        Custom = 999, // What is Beyond up to now?
    }

    public enum ToNRoundResult {
        R, // Respawn
        W, // Win
        D, // Leaving
        L, // Lost < This one never happens, but it will be there for legacy purposes
        X, // Forgot to save lol
        B, // REBORN (Died with Maxwell)
    }

    internal static class ToNRoundTypeExtensions {
        static readonly Dictionary<ToNRoundType, string> RoundTypeNames = new Dictionary<ToNRoundType, string>();
        static string? RoundTypeNamesLanguage;

        /// <summary>
        /// Gets the localized display name of a round type, cached per selected language.
        /// </summary>
        internal static string GetDisplayName(this ToNRoundType roundType) {
            if (RoundTypeNamesLanguage != LANG.SelectedKey) {
                RoundTypeNames.Clear();
                RoundTypeNamesLanguage = LANG.SelectedKey;

                ToNRoundType[] values = Enum.GetValues<ToNRoundType>();
                foreach (ToNRoundType rt in values) {
                    RoundTypeNames[rt] = LANG.S("ROUND_TYPE." + rt.ToString().ToUpperInvariant()) ?? rt.ToString();
#if NO_SPOILERS
                    switch (rt) {
                        case ToNRoundType.Double_Trouble:
                        case ToNRoundType.EX:
                        case ToNRoundType.Ghost:
                        case ToNRoundType.Ghost_Alternate:
                        case ToNRoundType.Unbound:
                        case ToNRoundType.GIGABYTE:
                            RoundTypeNames[rt] = "SPOILERS";
                            break;
                        default:
                            break;
                    }
#endif
                }
            }

            return RoundTypeNames[roundType];
        }
    }
}
