using ManagedCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Wox.Plugin;
using PowerToysRun.ShortcutFinder.Plugin.Helpers;

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

        private List<ShortcutInfo> _shortcuts = new();
        private FileSystemWatcher _watcher;
        private string _shortcutsDirectory;
        private Dictionary<string, List<ShortcutInfo>> _shortcutsBySource = new();

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());

            _shortcutsDirectory = Path.Combine(Context.CurrentPluginMetadata.PluginDirectory, "Shortcuts");

            LoadAllShortcuts(_shortcutsDirectory);
            WatchForChanges(_shortcutsDirectory);
        }

        private void LoadAllShortcuts(string shortcutsDirectory)
        {
            _shortcuts.Clear();
            _shortcutsBySource.Clear();

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
                        {
                            s.Source = source;
                            // Нормалізуємо shortcut для кращого пошуку
                            s.NormalizedShortcut = NormalizeShortcut(s.Shortcut);
                        }

                        _shortcuts.AddRange(list);
                        _shortcutsBySource[source] = list;
                    }
                }
                catch
                {
                    // Optionally log error if needed
                }
            }
        }

        private string NormalizeShortcut(string shortcut)
        {
            if (string.IsNullOrEmpty(shortcut)) return "";

            return shortcut
                .Replace("Ctrl", "Control")
                .Replace("Win", "Windows")
                .Replace("Alt", "Alt")
                .Replace("+", " ")
                .ToLowerInvariant()
                .Trim();
        }

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
        /// Парсить запит і витягує команду, фільтр програми та термін пошуку
        /// Підтримує формати:
        /// - "copy" -> search, null, "copy"
        /// - "copy /chrome" -> search, "chrome", "copy"
        /// - "list:chrome" -> list, "chrome", "chrome"
        /// - "apps" -> apps, null, null
        /// </summary>
        private (string command, string appFilter, string searchTerm) ParseQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return ("search", null, "");

            query = query.Trim();

            // Перевіряємо команду "apps"
            if (query.Equals("apps", StringComparison.OrdinalIgnoreCase))
                return ("apps", null, null);

            // Перевіряємо команду "list:app"
            if (query.StartsWith("list:", StringComparison.OrdinalIgnoreCase))
            {
                string app = query.Substring(5).Trim();
                return ("list", app, app);
            }

            // Шукаємо фільтр програми у форматі "/appname"
            var parts = query.Split(new[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2)
            {
                // Формат: "searchterm /appname"
                string searchTerm = parts[0].Trim();
                string appFilter = parts[1].Trim();
                return ("search", appFilter, searchTerm);
            }
            else if (query.StartsWith("/"))
            {
                // Формат: "/appname" (показати всі shortcuts для програми)
                string appFilter = query.Substring(1).Trim();
                return ("list", appFilter, appFilter);
            }

            // Звичайний пошук без фільтра
            return ("search", null, query);
        }

        private List<ShortcutInfo> SearchShortcuts(string query, string appFilter = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new();

            string q = query.ToLowerInvariant().Trim();

            // Фільтруємо shortcuts за програмою, якщо вказано фільтр
            var shortcutsToSearch = _shortcuts;
            if (!string.IsNullOrWhiteSpace(appFilter))
            {
                string filter = appFilter.ToLowerInvariant();
                shortcutsToSearch = _shortcuts.Where(s => 
                    s.Source?.ToLowerInvariant().Contains(filter) == true
                ).ToList();

                // Якщо точного збігу немає, шукаємо часткові збіги
                if (shortcutsToSearch.Count == 0)
                {
                    shortcutsToSearch = _shortcuts.Where(s =>
                        s.Source?.ToLowerInvariant().StartsWith(filter) == true ||
                        s.Source?.ToLowerInvariant().Contains(filter) == true
                    ).ToList();
                }
            }

            var results = new List<ShortcutInfo>();

            // 1. Точний збіг shortcut
            var exactMatches = shortcutsToSearch.Where(s => 
                s.Shortcut?.ToLowerInvariant() == q ||
                s.NormalizedShortcut?.Contains(q.Replace(" ", "")) == true
            ).ToList();

            // 2. Збіг по description (пріоритет)
            var descriptionMatches = shortcutsToSearch.Where(s =>
                s.Description?.ToLowerInvariant().Contains(q) == true &&
                !exactMatches.Contains(s)
            ).ToList();

            // 3. Збіг по keywords
            var keywordMatches = shortcutsToSearch.Where(s =>
                s.Keywords?.Any(k => k.ToLowerInvariant().Contains(q)) == true &&
                !exactMatches.Contains(s) &&
                !descriptionMatches.Contains(s)
            ).ToList();

            // 4. Збіг по category
            var categoryMatches = shortcutsToSearch.Where(s =>
                s.Category?.ToLowerInvariant().Contains(q) == true &&
                !exactMatches.Contains(s) &&
                !descriptionMatches.Contains(s) &&
                !keywordMatches.Contains(s)
            ).ToList();

            // 5. Часткові збіги
            var partialMatches = shortcutsToSearch.Where(s =>
                (s.Shortcut?.ToLowerInvariant().Contains(q) == true ||
                 s.Description?.ToLowerInvariant().Contains(q) == true) &&
                !exactMatches.Contains(s) &&
                !descriptionMatches.Contains(s) &&
                !keywordMatches.Contains(s) &&
                !categoryMatches.Contains(s)
            ).ToList();

            // Сортуємо за релевантністю
            results.AddRange(exactMatches.OrderBy(s => s.Source).ThenBy(s => s.Description));
            results.AddRange(descriptionMatches.OrderBy(s => s.Source).ThenBy(s => s.Description));
            results.AddRange(keywordMatches.OrderBy(s => s.Source).ThenBy(s => s.Description));
            results.AddRange(categoryMatches.OrderBy(s => s.Source).ThenBy(s => s.Description));
            results.AddRange(partialMatches.OrderBy(s => s.Source).ThenBy(s => s.Description));

            return results.Take(50).ToList(); // Обмежуємо кількість результатів
        }

        public List<Result> Query(Query query)
        {
            string search = query.Search?.Trim();

            // Якщо пустий запит, показуємо підказки
            if (string.IsNullOrWhiteSpace(search))
            {
                return GetHelpResults();
            }

            // Парсимо команду та додаткові параметри
            var (command, appFilter, searchTerm) = ParseQuery(search);

            // Спеціальні команди
            switch (command.ToLowerInvariant())
            {
                case "list":
                    return GetAppShortcuts(appFilter ?? searchTerm);

                case "apps":
                    return GetAvailableApps();

                case "search":
                default:
                    var found = SearchShortcuts(searchTerm, appFilter);
                    var results = new List<Result>();

            foreach (var shortcut in found)
            {
                var score = CalculateScore(shortcut, searchTerm, appFilter);

                results.Add(new Result
                {
                    QueryTextDisplay = search,
                    IcoPath = IconPath,
                    Title = FormatTitle(shortcut),
                    SubTitle = FormatSubTitle(shortcut, appFilter),
                    ToolTipData = new ToolTipData(shortcut.Description, $"{shortcut.Shortcut}\n\nSource: {shortcut.Source}\nCategory: {shortcut.Category}"),
                    Score = score,
                    Action = _ =>
                    {
                        ClipboardHelper.SetClipboard(shortcut.Shortcut);
                        Context.API.ShowMsg("Hotkey Copied", $"'{shortcut.Shortcut}' copied to clipboard", string.Empty);
                        return true;
                    },
                    ContextData = shortcut
                });
            }

            // Якщо нічого не знайдено
            if (results.Count == 0)
            {
                string noResultsMessage = !string.IsNullOrWhiteSpace(appFilter) 
                    ? $"No hotkeys found for '{searchTerm}' in {appFilter}"
                    : $"No hotkeys found for '{searchTerm}'";

                string suggestion = !string.IsNullOrWhiteSpace(appFilter)
                    ? $"Try removing /{appFilter} filter or check app name"
                    : "Try: 'apps' to see available apps, or 'search /appname' to filter by app";

                results.Add(new Result
                {
                    QueryTextDisplay = search,
                    IcoPath = IconPath,
                    Title = noResultsMessage,
                    SubTitle = suggestion,
                    Action = _ => true,
                    ContextData = search
                });
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        private List<Result> GetHelpResults()
        {
            return new List<Result>
            {
                new Result
                {
                    IcoPath = IconPath,
                    Title = "Search hotkeys by keyword",
                    SubTitle = "Example: 'copy', 'paste', 'ctrl+c'",
                    Action = _ => true
                },
                new Result
                {
                    IcoPath = IconPath,
                    Title = "List all available apps",
                    SubTitle = "Type: apps",
                    Action = _ =>
                    {
                        Context.API.ChangeQuery("hk apps", true);
                        return false;
                    }
                },
                new Result
                {
                    IcoPath = IconPath,
                    Title = "List shortcuts for specific app",
                    SubTitle = "Type: list:appname (e.g., 'list:chrome')",
                    Action = _ => true
                }
            };
        }

        private List<Result> GetAvailableApps()
        {
            var results = new List<Result>();

            foreach (var app in _shortcutsBySource.Keys.OrderBy(k => k))
            {
                var count = _shortcutsBySource[app].Count;
                results.Add(new Result
                {
                    IcoPath = IconPath,
                    Title = $"{app} ({count} shortcuts)",
                    SubTitle = $"Click to see all {app} shortcuts",
                    Action = _ =>
                    {
                        Context.API.ChangeQuery($"hk list:{app}", true);
                        return false;
                    },
                    ContextData = app
                });
            }

            return results;
        }

        private List<Result> GetAppShortcuts(string appName)
        {
            var results = new List<Result>();

            var matchingApps = _shortcutsBySource.Keys
                .Where(k => k.ToLowerInvariant().Contains(appName.ToLowerInvariant()))
                .ToList();

            foreach (var app in matchingApps)
            {
                var shortcuts = _shortcutsBySource[app];

                foreach (var shortcut in shortcuts.OrderBy(s => s.Category).ThenBy(s => s.Description))
                {
                    results.Add(new Result
                    {
                        IcoPath = IconPath,
                        Title = FormatTitle(shortcut),
                        SubTitle = FormatSubTitle(shortcut),
                        ToolTipData = new ToolTipData(shortcut.Description, $"{shortcut.Shortcut}\n\nCategory: {shortcut.Category}"),
                        Action = _ =>
                        {
                            ClipboardHelper.SetClipboard(shortcut.Shortcut);
                            Context.API.ShowMsg("Hotkey Copied", $"'{shortcut.Shortcut}' copied to clipboard", string.Empty);
                            return true;
                        },
                        ContextData = shortcut
                    });
                }
            }

            if (results.Count == 0)
            {
                results.Add(new Result
                {
                    IcoPath = IconPath,
                    Title = $"No app found matching '{appName}'",
                    SubTitle = "Type 'apps' to see all available applications",
                    Action = _ =>
                    {
                        Context.API.ChangeQuery("hk apps", true);
                        return false;
                    }
                });
            }

            return results;
        }

        private string FormatTitle(ShortcutInfo shortcut)
        {
            return $"{shortcut.Shortcut} - {shortcut.Description}";
        }

        private string FormatSubTitle(ShortcutInfo shortcut, string appFilter = null)
        {
            string subtitle = $"{shortcut.Source} | {shortcut.Category ?? "General"}";

            // Якщо використовується фільтр, підкреслюємо це
            if (!string.IsNullOrWhiteSpace(appFilter))
            {
                subtitle = $"📍 {subtitle} (filtered by {appFilter})";
            }

            return subtitle;
        }

        private int CalculateScore(ShortcutInfo shortcut, string query, string appFilter = null)
        {
            if (string.IsNullOrWhiteSpace(query)) return 0;

            int score = 0;
            string q = query.ToLowerInvariant();

            // Бонус за точний збіг з фільтром програми
            if (!string.IsNullOrWhiteSpace(appFilter))
            {
                string filter = appFilter.ToLowerInvariant();
                if (shortcut.Source?.ToLowerInvariant() == filter)
                    score += 200; // Великий бонус за точний збіг програми
                else if (shortcut.Source?.ToLowerInvariant().Contains(filter) == true)
                    score += 100; // Менший бонус за частковий збіг програми
            }

            // Точний збіг shortcut - найвищий пріоритет
            if (shortcut.Shortcut?.ToLowerInvariant() == q)
                score += 1000;
            else if (shortcut.Shortcut?.ToLowerInvariant().Contains(q) == true)
                score += 800;

            // Збіг з description
            if (shortcut.Description?.ToLowerInvariant() == q)
                score += 900;
            else if (shortcut.Description?.ToLowerInvariant().StartsWith(q) == true)
                score += 700;
            else if (shortcut.Description?.ToLowerInvariant().Contains(q) == true)
                score += 500;

            // Збіг з keywords
            if (shortcut.Keywords?.Any(k => k.ToLowerInvariant() == q) == true)
                score += 600;
            else if (shortcut.Keywords?.Any(k => k.ToLowerInvariant().Contains(q)) == true)
                score += 300;

            // Популярні програми отримують бонус
            var popularApps = new[] { "chrome", "firefox", "vscode", "word", "excel", "windows", "photoshop" };
            if (popularApps.Contains(shortcut.Source?.ToLowerInvariant()))
                score += 50;

            return score;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult.ContextData is ShortcutInfo shortcut)
            {
                return new List<ContextMenuResult>
                {
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
                            ClipboardHelper.SetClipboard(shortcut.Shortcut);
                            Context.API.ShowMsg("Copied", $"'{shortcut.Shortcut}' copied to clipboard", string.Empty);
                            return true;
                        }
                    },
                    new ContextMenuResult
                    {
                        PluginName = Name,
                        Title = "Copy description",
                        FontFamily = "Segoe MDL2 Assets",
                        Glyph = "\xE8C8",
                        Action = _ =>
                        {
                            ClipboardHelper.SetClipboard(shortcut.Description);
                            Context.API.ShowMsg("Copied", $"Description copied to clipboard", string.Empty);
                            return true;
                        }
                    },
                    new ContextMenuResult
                    {
                        PluginName = Name,
                        Title = $"Show all {shortcut.Source} shortcuts",
                        FontFamily = "Segoe MDL2 Assets",
                        Glyph = "\xE8FD",
                        Action = _ =>
                        {
                            Context.API.ChangeQuery($"hk list:{shortcut.Source}", true);
                            return false;
                        }
                    }
                };
            }
            else if (selectedResult.ContextData is string search)
            {
                return new List<ContextMenuResult>
                {
                    new ContextMenuResult
                    {
                        PluginName = Name,
                        Title = "Copy query to clipboard",
                        FontFamily = "Segoe MDL2 Assets",
                        Glyph = "\xE8C8",
                        AcceleratorKey = Key.C,
                        AcceleratorModifiers = ModifierKeys.Control,
                        Action = _ =>
                        {
                            ClipboardHelper.SetClipboard(search);
                            return true;
                        }
                    }
                };
            }

            return new List<ContextMenuResult>();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed || !disposing)
                return;

            if (Context?.API != null)
                Context.API.ThemeChanged -= OnThemeChanged;

            _watcher?.Dispose();
            Disposed = true;
        }

        private void UpdateIconPath(Theme theme) => IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite
            ? "Images/hotkeys.light.png"
            : "Images/hotkeys.dark.png";

        private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);

        public class ShortcutInfo
        {
            public string Shortcut { get; set; }
            public string Description { get; set; }
            public List<string> Keywords { get; set; }
            public string Category { get; set; }
            public string Source { get; set; }
            public string Language { get; set; }
            public string NormalizedShortcut { get; set; } // Додано для кращого пошуку
        }
    }
}