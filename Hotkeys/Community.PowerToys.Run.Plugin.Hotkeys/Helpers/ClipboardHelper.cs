using System.Windows;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Helpers
{
    public static class ClipboardHelper
    {
        public static void SetClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch { }
        }
    }
}
