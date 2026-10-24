using System.ComponentModel;
using ToNSaveManager.Models;

namespace ToNSaveManager.Windows {
    public sealed class ListRow : INotifyPropertyChanged {
        internal object Item { get; }

        public ListRow(object item) {
            Item = item;
        }

        private bool m_IsCopied;
        internal bool IsCopied {
            get => m_IsCopied;
            set {
                if (m_IsCopied == value) return;
                m_IsCopied = value;
                Refresh();
            }
        }

        public string Text => IsCopied ? Entry.TextCopied : Item.ToString() ?? string.Empty;

        public string? Tooltip => Item is Entry entry ? entry.GetTooltip(Settings.Get.SaveNames, Settings.Get.SaveRoundInfo) : null;

        public event PropertyChangedEventHandler? PropertyChanged;

        internal void Refresh() {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tooltip)));
        }
    }
}
