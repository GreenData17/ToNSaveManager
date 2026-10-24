using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;
using ToNSaveManager.Models;
using ToNSaveManager.Models.Index;
using ToNSaveManager.Utils;
using ToNSaveManager.Utils.API;
using ToNSaveManager.Utils.JSPlugins.API;
using ToNSaveManager.Utils.LogParser;
using static ToNSaveManager.Models.Index.ToNIndex;
using Color = System.Drawing.Color;

namespace ToNSaveManager.Windows {
    public partial class EmulatorWindow : Window {
        #region Sub Classes
        internal class ToNOperation {
            public ToNRoundType RoundType = ToNRoundType.Classic;
            public Terror? TerrorIndex;
            public Terror? TerrorIndex2;
            public Terror? TerrorIndex3;
            public Map? MapIndex;
            public bool IsSaboteur = false; // Only on sabotage

            public bool IsHidden => RoundType == ToNRoundType.Eight_Pages ||
                RoundType == ToNRoundType.Fog || RoundType == ToNRoundType.Fog_Alternate;
        }

        internal class RoundTypeProxy {
            public string DisplayName;
            public ToNRoundType Value;

            public RoundTypeProxy(ToNRoundType roundType) {
                DisplayName = roundType == ToNRoundType.Intermission ? "Random" : roundType.GetDisplayName();
                Value = roundType;
            }

            public override string ToString() {
                return ((int)Value).ToString().PadRight(3) + " | " + DisplayName;
            }
        }
        #endregion

        internal static EmulatorWindow? Instance;

        internal static ToNOperation Operation = new ToNOperation();
        internal static ToNIndex Database => ToNIndex.Instance;

        #region Index Properties
        internal static Map[] Maps;
        internal static Terror[] Terrors;
        internal static Terror[] Alternates;
        internal static Terror[] Whitelisted;

        internal static Terror[] Unbound;

        internal static Terror[] EightPages;
        internal static Map[] Maps8P;

        internal static Dictionary<ToNRoundType, int> RoundTypeToMapID = new Dictionary<ToNRoundType, int>() {
            { ToNRoundType.RUN,       254},
            { ToNRoundType.Cold_Night, 37},
            { ToNRoundType.GIGABYTE,    2},
            { ToNRoundType.Twilight,   67},
        };

        #endregion

        #region Initialization
        static EmulatorWindow() {
            Maps = Database.Maps.Values.Where((t, i) => i < Database.Maps.Count - 1).ToArray();
            Terrors = Database.Terrors.Values.ToArray();
            Alternates = Database.Alternates.Values.ToArray();
            Whitelisted = Terrors.Where(t => !t.CantBB).ToArray();

            Unbound = Database.Unbound.Values.ToArray();

            var redirect = Database.EightPRedirect;
            var eightP = Database.EightPages.ToDictionary();

            foreach (var pair in redirect) {
                int key = pair.Key;
                int group = pair.Value[0];
                int terror = pair.Value[1];

                Terror t = Database.GetTerror(terror, group);
                eightP[key] = new Terror() {
                    CantBB = t.CantBB, // Can't participate in bb
                    Group = t.Group,
                    // Inherited
                    Id = key,
                    Color = t.Color,
                    Name = t.Name
                };
            }

            EightPages = eightP.Values.ToArray();
            Maps8P = Maps.Where(m => m.EightP).ToArray();
        }

        public EmulatorWindow() {
            InitializeComponent();

            FuncDataTemplate<object> monsterTemplate = new FuncDataTemplate<object>((value, scope) => {
                if (value is not EntryBase item) return new TextBlock();
                return new TextBlock {
                    Text = item.Id.ToString().PadRight(3) + " | " + item.ToString(),
                    Foreground = new SolidColorBrush(item.Color.ToAvalonia())
                };
            });
            comboMonster.ItemTemplate = monsterTemplate;
            comboMonster2.ItemTemplate = monsterTemplate;
            comboMonster3.ItemTemplate = monsterTemplate;

            foreach (Button button in new[] { buttonStepStart, buttonStepKillerSet, buttonStepReveal, buttonDamage, buttonDeath, buttonStepEndRound }) {
                button.PropertyChanged += (sender, e) => {
                    if (e.Property == IsEnabledProperty) buttonStep_Enabled(button);
                };
            }

            Form1_Load();
        }
        #endregion

        #region UI Code
        internal static readonly ComboBox[] TerrorComboBoxes = new ComboBox[3];

        public static void Open(Window parent) {
            // Can't reopen a closed window
            if (Instance == null || Instance.IsClosed) Instance = new();
            Instance.ShowCentered(parent, true);
        }

        internal bool IsClosed { get; private set; }
        protected override void OnClosed(EventArgs e) {
            IsClosed = true;
            base.OnClosed(e);
        }

        private async void Form1_Load() {
            if (Database.Maps.Count < 1) {
                await MessageBox.Show(this, "Invalid Database", "Invalid Database", AlertButtons.OK, AlertIcon.Error);
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
                return;
            }

            SetMonsterDataSource(null);
            SetLocationDataSource(null);

            TerrorComboBoxes[0] = comboMonster;
            TerrorComboBoxes[1] = comboMonster2;
            TerrorComboBoxes[2] = comboMonster3;

            var values = ((ToNRoundType[])Enum.GetValues(typeof(ToNRoundType))).Select(rt => new RoundTypeProxy(rt)).ToArray();
            comboRoundType.ItemsSource = values;
            comboRoundType.SelectedIndex = 0;

            buttonStep_Reset();
            UpdateSpecialCheckbox();
        }

        private void UpdateSpecialCheckbox() {
            checkSpecial.IsVisible = HasValidEncounter(Operation.TerrorIndex) || HasValidEncounter(Operation.TerrorIndex2) || HasValidEncounter(Operation.TerrorIndex3);
            if (checkSpecial.IsVisible) {
                checkSpecial.Content = LastEncounter.Name;
            } else checkSpecial.IsChecked = false;
        }

        private Terror.Encounter LastEncounter;
        private bool HasValidEncounter(Terror? terror) {
            if (terror != null && terror.Encounters != null && terror.Encounters.Length > 0 && (terror.Encounters[0].RoundType == ToNRoundType.Intermission || terror.Encounters[0].RoundType == Operation.RoundType)) {
                LastEncounter = terror.Encounters[0];
                return true;
            }

            return false;
        }

        private void SetMonsterDataSource(Terror[]? source) {
            SetComboDataSource(comboMonster, source);
        }

        private void SetLocationDataSource(Map[]? source) {
            SetComboDataSource(comboLocation, source);
        }

        private void SetComboDataSource<T>(ComboBox comboBox, T[]? source) where T : IEntry {
            comboBox.ItemsSource = null;
            comboBox.IsEnabled = source != null;
            if (source == null) return;

            T[] __source = new T[source.Length + 1];
            __source[0] = Activator.CreateInstance<T>();

            T v = __source[0];
            v.Id = -1;
            v.Name = "_RANDOM_";
            v.Color = Color.White;
            __source[0] = v;

            Array.Copy(source, 0, __source, 1, source.Length);

            comboBox.ItemsSource = __source;
            comboBox.SelectedIndex = 0;
        }

        private void comboRoundType_SelectedIndexChanged(object? sender, SelectionChangedEventArgs e) {
            RoundTypeProxy? selectedValue = comboRoundType.SelectedItem as RoundTypeProxy;

            ToNRoundType roundType;
            if (selectedValue == null) roundType = ToNRoundType.Classic;
            else roundType = selectedValue.Value;


            if (Operation.RoundType != roundType) {
                Operation.RoundType = roundType;
                int roundTypeInt = (int)roundType;

                switch (roundType) {
                    case ToNRoundType.Eight_Pages:
                        SetMonsterDataSource(EightPages);
                        SetLocationDataSource(Maps8P);
                        break;

                    case ToNRoundType.Unbound:
                        SetMonsterDataSource(Unbound);
                        SetLocationDataSource(Maps);
                        break;

                    // These have map pools
                    case ToNRoundType.Intermission: // Dont do anything
                    case ToNRoundType.RUN:
                    case ToNRoundType.Cold_Night:
                    case ToNRoundType.GIGABYTE:
                    case ToNRoundType.Twilight:
                        SetMonsterDataSource(null);
                        SetLocationDataSource(null);
                        break;
                    case ToNRoundType.Solstice:
                    case ToNRoundType.Mystic_Moon:
                    case ToNRoundType.Blood_Moon:
                    case ToNRoundType.Custom:
                        SetMonsterDataSource(null);
                        SetLocationDataSource(Maps);
                        break;
                    case ToNRoundType.Classic:
                    case ToNRoundType.Fog:
                    case ToNRoundType.Fog_Alternate:
                    case ToNRoundType.Ghost_Alternate:
                    case ToNRoundType.Ghost:
                    case ToNRoundType.Punished:
                    case ToNRoundType.Alternate:
                    case ToNRoundType.Sabotage:
                    case ToNRoundType.Cracked:
                    case ToNRoundType.Classic_exe:
                    case ToNRoundType.Randomizer:
                        SetMonsterDataSource(
                            roundType == ToNRoundType.Alternate ||
                            roundType == ToNRoundType.Fog_Alternate ||
                            roundType == ToNRoundType.Ghost_Alternate
                            ? Alternates : Terrors
                        );
                        SetLocationDataSource(Maps);
                        break;
                    case ToNRoundType.Bloodbath:
                    case ToNRoundType.Double_Trouble:
                    case ToNRoundType.EX:
                    case ToNRoundType.Midnight:
                        SetMonsterDataSource(Whitelisted);
                        SetLocationDataSource(Maps);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                SetComboDataSource(comboMonster2, roundTypeInt > 5 && roundTypeInt < 8 || roundType == ToNRoundType.Midnight ? Whitelisted : null);
                SetComboDataSource(comboMonster3, roundTypeInt > 5 && roundTypeInt < 7 ? Terrors :
                    (roundType == ToNRoundType.Midnight ? Alternates : null));

                checkBoxIsKiller.IsVisible = roundType == ToNRoundType.Sabotage;
                buttonStepReveal.Content = roundType == ToNRoundType.Eight_Pages ? "Find Page 1" : "Reveal";
            }
        }

        private void SetFromCombo<T>(ComboBox element, ref T? indexOutput) where T : IEntry {
            int index = element.SelectedIndex;
            object? selectedItem = element.SelectedItem;
            if (index > -1 && selectedItem != null) {
                indexOutput = (T)selectedItem;
            } else indexOutput = default;
            Logger.Debug($"Selected: {indexOutput} | {element.Name}");

            UpdateSpecialCheckbox();
        }

        private void comboMonster_SelectedIndexChanged(object? sender, SelectionChangedEventArgs e) {
            SetFromCombo<Terror>((ComboBox)sender!, ref Operation.TerrorIndex);
        }

        private void comboMonster2_SelectedIndexChanged(object? sender, SelectionChangedEventArgs e) {
            SetFromCombo<Terror>((ComboBox)sender!, ref Operation.TerrorIndex2);
        }

        private void comboMonster3_SelectedIndexChanged(object? sender, SelectionChangedEventArgs e) {
            SetFromCombo<Terror>((ComboBox)sender!, ref Operation.TerrorIndex3);
        }

        private void comboLocation_SelectedIndexChanged(object? sender, SelectionChangedEventArgs e) {
            SetFromCombo<Map>((ComboBox)sender!, ref Operation.MapIndex);
        }
        #endregion

        // Fake log lines for tracker clients (tontrack.me)
        private void SimulateLine(string line) {
            if (checkTrackerLines.IsChecked != true || !Settings.Get.WebSocketEnabled || !Settings.Get.WebTrackerCompatibility) return;
            WebSocketAPI.OnReadLine(line);
            Logger.Debug("Simulated line: " + line);
        }

        // As written in the log
        private string RoundTypeText => CurrentRoundType.ToString().Replace('_', ' ').Replace(" Alternate", " (Alternate)");

        private void buttonStep_Reset() {
            mainPanel.IsEnabled = true;
            buttonStepStart.IsEnabled = true;
            buttonStepKillerSet.IsEnabled = false;
            buttonStepReveal.IsEnabled = false;
            buttonStepEndRound.IsEnabled = false;
            buttonDamage.IsEnabled = buttonDeath.IsEnabled = false;
            buttonStepReveal.Content = CurrentRoundType == ToNRoundType.Eight_Pages ? "Find Page 1" : "Reveal";
        }

        private void buttonStep_Enabled(Button button) {
            button.Foreground = WindowExtensions.GetBrush(button.IsEnabled ? (button.TabIndex > 3 ? "Danger" : "ListFore") : "Muted");
        }

        int TempHealth = 100;
        private void buttonStep_Click(object? sender, RoutedEventArgs e) {
            Button button = (Button)sender!;
            switch (button.TabIndex) {
                case 0: // Start
                    TempHealth = 100;
                    mainPanel.IsEnabled = false;
                    buttonStepStart.IsEnabled = false;
                    buttonStepKillerSet.IsEnabled = true;
                    buttonStepEndRound.IsEnabled = true;
                    OnRoundStart();
                    if (CurrentRoundType == ToNRoundType.RUN)
                        buttonStep_Click(buttonStepKillerSet, e);
                    break;

                case 1: // Set
                    buttonStepKillerSet.IsEnabled = false;
                    buttonStepReveal.IsEnabled = Operation.IsHidden;
                    buttonDamage.IsEnabled = buttonDeath.IsEnabled = true;
                    OnRoundSetKillers(!buttonStepReveal.IsEnabled);
                    break;

                case 2: // Reveal
                    if (Operation.RoundType == ToNRoundType.Eight_Pages) {
                        ToNGameState.SetPageCount(ToNGameState.PageCount + 1);
                        if (ToNGameState.PageCount == 1) OnRoundSetKillers(true);
                        else if (ToNGameState.PageCount == 8) buttonStepReveal.IsEnabled = false;
                        else buttonStepReveal.Content = "Find Page " + (ToNGameState.PageCount + 1);
                    } else {
                        buttonStepReveal.IsEnabled = false;
                        OnRoundSetKillers(true);
                    }
                    break;

                case 4: // Damage
                    int dmg = Random.Shared.Next(1, 6) * 5;
                    ToNGameState.AddDamage(dmg);
                    SimulateLine("Hit - " + dmg);
                    TempHealth -= dmg;
                    if (TempHealth <= 0) {
                        TempHealth = 0;
                        buttonStep_Click(buttonDeath, e);
                    }
                    break;

                case 5: // Death
                    ToNGameState.SetAlive(false);
                    ToNGameState.AddDamage(byte.MaxValue);
                    buttonDeath.IsEnabled = buttonDamage.IsEnabled = false;

                    string name = string.IsNullOrEmpty(ToNGameState.DisplayName) ? "Player" : ToNGameState.DisplayName;
                    SimulateLine($"[DEATH][{name}] {name} died in the emulator.");
                    break;

                default:
                    buttonStep_Reset();
                    OnRoundEnd();
                    break;
            }
        }

        private ToNRoundType CurrentRoundType;
        private bool CurrentIsKiller => CurrentRoundType == ToNRoundType.Sabotage && checkBoxIsKiller.IsChecked == true;

        private void OnRoundStart() {
            // Round is random
            if (comboRoundType.SelectedIndex == 0)
                comboRoundType.SelectedIndex = Random.Shared.Next(1, comboRoundType.ItemCount);

            RoundTypeProxy roundTypeProxy = comboRoundType.SelectedItem as RoundTypeProxy ?? new RoundTypeProxy(ToNRoundType.Classic);
            CurrentRoundType = roundTypeProxy.Value;

            Map selectedMap = Map.Empty;
            if (comboLocation.ItemCount > 0) {
                if (comboLocation.SelectedIndex == 0) comboLocation.SelectedIndex = Random.Shared.Next(1, comboLocation.ItemCount);
                selectedMap = comboLocation.SelectedItem as Map ?? selectedMap;
            } else if (RoundTypeToMapID.TryGetValue(roundTypeProxy.Value, out int mapId)) selectedMap = Database.GetMap(mapId);

            if (selectedMap.IsEmpty) throw new Exception("Failed to emulate map.");

            ToNGameState.SetEmulated(true);
            ToNGameState.SetOptedIn(true);
            ToNGameState.SetLocation(selectedMap);
            ToNGameState.SetTerrorMatrix(new TerrorMatrix(CurrentRoundType == ToNRoundType.GIGABYTE ? ToNRoundType.Classic : CurrentRoundType) { IsSaboteur = CurrentIsKiller });
            ToNGameState.SetKiller(CurrentIsKiller);
            ToNGameState.SetAlive(true);
            ToNGameState.SetRoundActive(true);

            SimulateLine("opted in");
            SimulateLine("Everything recieved, looks good to meee~!");
            SimulateLine($"This round is taking place at {selectedMap.Name} ({selectedMap.Id}) and the round type is {RoundTypeText}");
        }

        private void OnRoundSetKillers(bool reveal = true) {
            if (CurrentRoundType == ToNRoundType.Custom) return;

            int[] terrors = new int[TerrorComboBoxes.Length];
            for (int i = 0; i < TerrorComboBoxes.Length; i++) {
                if (reveal) {
                    var comboBox = TerrorComboBoxes[i];
                    if (comboBox.ItemCount > 0) {
                        if (comboBox.SelectedIndex == 0) comboBox.SelectedIndex = Random.Shared.Next(1, comboBox.ItemCount);
                        terrors[i] = (comboBox.SelectedItem as Terror ?? Terror.Zero).Id;
                    } else {
                        terrors[i] = CurrentRoundType == ToNRoundType.Double_Trouble || CurrentRoundType == ToNRoundType.EX ? terrors[i - 1] : 0;
                    }
                } else terrors[i] = byte.MaxValue;
            }

            if (CurrentRoundType == ToNRoundType.Bloodbath) {
                if (terrors.All(v => v == terrors[0])) CurrentRoundType = ToNRoundType.EX;
                else if (terrors.Length != terrors.Distinct().Count()) CurrentRoundType = ToNRoundType.Double_Trouble;
            }

            TerrorMatrix terrorMatrix = new TerrorMatrix(RoundTypeText, terrors);
            terrorMatrix.IsSaboteur = CurrentIsKiller;
            terrorMatrix.RoundType = CurrentRoundType;

            if (CurrentRoundType == ToNRoundType.GIGABYTE) {
                terrorMatrix.RoundType = CurrentRoundType;
                terrorMatrix.Terrors = [new TerrorInfo(1, TerrorGroup.Events)];
                terrorMatrix.TerrorCount = 1;
            }

            // TODO: Check for specials
            if (checkSpecial.IsVisible && checkSpecial.IsChecked == true) terrorMatrix.MarkEncounter();

            ToNGameState.SetTerrorMatrix(terrorMatrix);
            SimulateLine($"Killers have been set - {string.Join(' ', terrors)} // Round type is {RoundTypeText}");
        }

        private void OnRoundEnd() {
            SimulateLine(ToNGameState.IsAlive ? "Player Won" : "Player lost, not killer");
            SimulateLine("Player respawned, opted out!");

            ToNGameState.SetOptedIn(false);
            ToNGameState.SetTerrorMatrix(TerrorMatrix.Empty);
            ToNGameState.SetLocation(Map.Empty);
            ToNGameState.SetKiller(false);
            ToNGameState.SetAlive(true);
            ToNGameState.SetRoundActive(false);
            ToNGameState.SetEmulated(false);
        }
    }
}
