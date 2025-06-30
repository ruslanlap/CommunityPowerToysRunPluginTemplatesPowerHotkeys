using ManagedCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.Hotkeys
{
    public class Main : IPlugin, IContextMenu, IDisposable
    {
        public static string PluginID => "4BDC7426E3404ECDB2D502B7B3CEAD9F";

        public string Name => "Hotkeys";
        public string Description => "Find and copy keyboard shortcuts for any app";

        private PluginInitContext Context { get; set; }
        private string IconPath { get; set; }
        private bool Disposed { get; set; }

        // === HOTKEY FINDER LOGIC START ===

        private List<ShortcutInfo> _shortcuts = new();
        private FileSystemWatcher _watcher;
        private string _shortcutsDirectory;

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());

            // Define Shortcuts directory (inside plugin folder)
            _shortcutsDirectory = Path.Combine(Context.CurrentPluginMetadata.PluginDirectory, "Shortcuts");

            LoadAllShortcuts(_shortcutsDirectory);
            WatchForChanges(_shortcutsDirectory);
        }

        /// <summary>
        /// Load and aggregate all shortcut sets from all JSON files in all subdirectories.
        /// </summary>
        private void LoadAllShortcuts(string shortcutsDirectory)
        {
            _shortcuts.Clear();
            if (!Directory.Exists(shortcutsDirectory))
                return;

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
                            s.Source = source;
                        _shortcuts.AddRange(list);
                    }
                }
                catch
                {
                    // Optionally log error if needed
                }
            }
        }

        /// <summary>
        /// Live reload on Shortcuts folder changes.
        /// </summary>
        private void WatchForChanges(string shortcutsDirectory)
        {
            if (_watcher != null)
            {
                _watcher.Dispose();
            }

            if (!Directory.Exists(shortcutsDirectory))
                return;

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

        /// <summary>
        /// Search hotkeys by query in all loaded sets.
        /// </summary>
        private List<ShortcutInfo> SearchShortcuts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new();

            string q = query.ToLowerInvariant();

            return _shortcuts.FindAll(s =>
                (s.Description != null && s.Description.ToLowerInvariant().Contains(q)) ||
                (s.Shortcut != null && s.Shortcut.ToLowerInvariant().Contains(q)) ||
                (s.Keywords != null && s.Keywords.Exists(k => k.ToLowerInvariant().Contains(q))) ||
                (s.Category != null && s.Category.ToLowerInvariant().Contains(q)) ||
                (s.Source != null && s.Source.ToLowerInvariant().Contains(q))
            );
        }

        /// <summary>
        /// Main PowerToys Run query logic.
        /// </summary>
        public List<Result> Query(Query query)
        {
            string search = query.Search?.Trim();
            var found = SearchShortcuts(search);

            var results = new List<Result>();

            foreach (var shortcut in found)
            {
                results.Add(new Result
                {
                    QueryTextDisplay = search,
                    IcoPath = IconPath,
                    Title = shortcut.Description ?? shortcut.Shortcut,
                    SubTitle = $"{shortcut.Shortcut} | {shortcut.Source} | {shortcut.Category}",
                    ToolTipData = new ToolTipData(shortcut.Description, shortcut.Shortcut),
                    Action = _ =>
                    {
                        Clipboard.SetDataObject(shortcut.Shortcut);
                        return true;
                    },
                    ContextData = shortcut
                });
            }

            // Якщо нічого не знайдено — простий placeholder як було у твоєму темплейті
            if (results.Count == 0 && !string.IsNullOrWhiteSpace(search))
            {
                results.Add(new Result
                {
                    QueryTextDisplay = search,
                    IcoPath = IconPath,
                    Title = "No hotkeys found.",
                    SubTitle = "Try another keyword or add new shortcuts to the Shortcuts folder.",
                    ToolTipData = new ToolTipData("No results", "Nothing matched your query."),
                    Action = _ =>
                    {
                        Clipboard.SetDataObject(search);
                        return true;
                    },
                    ContextData = search
                });
            }

            return results;
        }

        /// <summary>
        /// Context menu for results: copy to clipboard.
        /// </summary>
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult.ContextData is ShortcutInfo shortcut)
            {
                return
                [
                    new ContextMenuResult
                    {
                        PluginName = Name,
                        Title = "Copy shortcut to clipboard (Ctrl+C)",
                        FontFamily = "Segoe MDL2 Assets",
                        Glyph = "\xE8C8",
                        AcceleratorKey = Key.C,
                        AcceleratorModifiers = ModifierKeys.Control,
                        Action = _ =>
                        {
                            Clipboard.SetDataObject(shortcut.Shortcut);
                            return true;
                        }
                    }
                ];
            }
            else if (selectedResult.ContextData is string search)
            {
                return
                [
                    new ContextMenuResult
                    {
                        PluginName = Name,
                        Title = "Copy query to clipboard (Ctrl+C)",
                        FontFamily = "Segoe MDL2 Assets",
                        Glyph = "\xE8C8",
                        AcceleratorKey = Key.C,
                        AcceleratorModifiers = ModifierKeys.Control,
                        Action = _ =>
                        {
                            Clipboard.SetDataObject(search);
                            return true;
                        }
                    }
                ];
            }

            return [];
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed || !disposing)
            {
                return;
            }

            if (Context?.API != null)
            {
                Context.API.ThemeChanged -= OnThemeChanged;
            }

            _watcher?.Dispose();
            Disposed = true;
        }

        private void UpdateIconPath(Theme theme) => IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite
            ? "Images/hotkeys.light.png"
            : "Images/hotkeys.dark.png";

        private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);

        // === HOTKEY FINDER LOGIC END ===

        /// <summary>
        /// Клас для представлення одного шортката (включено тут для зручності)
        /// </summary>
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
}
