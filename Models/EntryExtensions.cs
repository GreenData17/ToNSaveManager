namespace ToNSaveManager.Models
{
    internal static class EntryExtensions
    {
        public static void CopyToClipboard(this Entry entry)
        {
            Logger.Log("COPYING TO CLIPBOARD: " + entry);
            // Windows 11 please... :[
            // Clipboard.Clear();
            // Clipboard.SetText(Content);
            try {
                Clipboard.SetDataObject(entry.Content, true, 4, 200);
            } catch (Exception e) {
                Logger.Error("Error trying to set clipboard object: " + e);
            }
            // Clipboard.SetText(Content);
        }
    }
}
