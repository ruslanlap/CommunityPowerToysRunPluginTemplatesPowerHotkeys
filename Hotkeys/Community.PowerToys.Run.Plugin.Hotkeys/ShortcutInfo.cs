using System.Collections.Generic;

namespace PowerToysRun.ShortcutFinder.Plugin
{
    public class ShortcutInfo
    {
        public string Shortcut { get; set; }
        public string Description { get; set; }
        public List<string> Keywords { get; set; }
        public string Category { get; set; }
        public string Source { get; set; }
        public string Language { get; set; }
    }
}
