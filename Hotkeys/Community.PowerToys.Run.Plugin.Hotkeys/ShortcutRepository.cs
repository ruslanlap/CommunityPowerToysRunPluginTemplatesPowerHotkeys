using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PowerToysRun.ShortcutFinder.Plugin
{
    public class ShortcutRepository
    {
        public List<ShortcutInfo> Shortcuts { get; private set; } = new List<ShortcutInfo>();
        private FileSystemWatcher _watcher;

        public void LoadAllShortcuts(string shortcutsDirectory)
        {
            Shortcuts.Clear();

            var jsonFiles = Directory.GetFiles(shortcutsDirectory, "*.json", SearchOption.AllDirectories);

            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var list = JsonSerializer.Deserialize<List<ShortcutInfo>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (list != null)
                    {
                        string source = Path.GetFileNameWithoutExtension(file);
                        foreach (var s in list)
                        {
                            s.Source = source;
                        }
                        Shortcuts.AddRange(list);
                    }
                }
                catch
                {
                    // Optionally log error
                }
            }
        }

        public void WatchForChanges(string shortcutsDirectory)
        {
            if (_watcher != null)
            {
                _watcher.Dispose();
            }

            _watcher = new FileSystemWatcher(shortcutsDirectory)
            {
                Filter = "*.json",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };

            _watcher.Changed += (s, e) => LoadAllShortcuts(shortcutsDirectory);
            _watcher.Created += (s, e) => LoadAllShortcuts(shortcutsDirectory);
            _watcher.Deleted += (s, e) => LoadAllShortcuts(shortcutsDirectory);
            _watcher.Renamed += (s, e) => LoadAllShortcuts(shortcutsDirectory);

            _watcher.EnableRaisingEvents = true;
        }

        public List<ShortcutInfo> SearchShortcuts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<ShortcutInfo>();

            string q = query.ToLowerInvariant();

            return Shortcuts.FindAll(s =>
                (s.Description != null && s.Description.ToLowerInvariant().Contains(q)) ||
                (s.Shortcut != null && s.Shortcut.ToLowerInvariant().Contains(q)) ||
                (s.Keywords != null && s.Keywords.Exists(k => k.ToLowerInvariant().Contains(q))) ||
                (s.Category != null && s.Category.ToLowerInvariant().Contains(q)) ||
                (s.Source != null && s.Source.ToLowerInvariant().Contains(q))
            );
        }
    }
}
