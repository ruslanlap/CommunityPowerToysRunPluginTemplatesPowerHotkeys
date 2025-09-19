// Main.cs - Plugin Entry Point (merged with template style)
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.CheatSheets
{
    /// <summary>
    /// Main class of this plugin that implements all used interfaces.
    /// </summary>
    public class Main : IPlugin, IContextMenu, IDisposable, IPluginI18n, ISettingProvider
    {
        /// <summary>
        /// ID of the plugin (kept from the working template).
        /// </summary>
        public static string PluginID => "41BF0604C51A4974A0BAA108826D0A94";

        /// <summary>
        /// Name of the plugin.
        /// </summary>
        public string Name => "Cheat Sheets Finder";

        /// <summary>
        /// Description of the plugin.
        /// </summary>
        public string Description => "Find cheat sheets and command examples instantly";

        private PluginInitContext Context { get; set; }

        private string IconPath { get; set; }

        private bool Disposed { get; set; }

        // --- Services from your original implementation ---
        private readonly CheatSheetService _cheatSheetService;
        private readonly CacheService _cacheService;

        // --- Settings cache ---
        private bool _enableDevHints = true;
        private bool _enableTldr = true;
        private bool _enableCheatSh = true;
        private int _cacheDurationHours = 12;

        public Main()
        {
            _cacheService = new CacheService();
            _cheatSheetService = new CheatSheetService(_cacheService);
        }

        /// <summary>
        /// Return a filtered list, based on the given query.
        /// </summary>
        /// <param name="query">The query to filter the list.</param>
        /// <returns>A filtered list, can be empty when nothing was found.</returns>
        public List<Result> Query(Query query)
        {
            var search = (query?.Search ?? string.Empty).Trim();

            // Default help card
            if (string.IsNullOrWhiteSpace(search))
            {
                return new List<Result>
                {
                    new Result
                    {
                        IcoPath = IconPath,
                        Title = "Cheat Sheets Finder",
                        SubTitle = "Type a command or tech to search (e.g., 'git reset', 'docker volume', 'regex lookahead')",
                        ToolTipData = new ToolTipData("Cheat Sheets Finder", "Search across DevHints, TLDR, and cheat.sh"),
                        Action = _ => true,
                    }
                };
            }

            // Configure sources based on settings
            _cheatSheetService.ConfigureSources(new CheatSheetSourceOptions
            {
                EnableDevHints = _enableDevHints,
                EnableTldr = _enableTldr,
                EnableCheatSh = _enableCheatSh,
                CacheDuration = TimeSpan.FromHours(Math.Max(1, _cacheDurationHours)),
            });

            // Search results from all sources
            var items = _cheatSheetService.SearchCheatSheets(search) ?? Enumerable.Empty<CheatSheetItem>();

            var results = new List<Result>();

            foreach (var sheet in items.Take(7))
            {
                results.Add(new Result
                {
                    QueryTextDisplay = search,
                    IcoPath = IconPath,
                    Title = sheet.Title,
                    SubTitle = string.IsNullOrWhiteSpace(sheet.Description) ? sheet.SourceName : sheet.Description,
                    ToolTipData = new ToolTipData(sheet.Title, $"{sheet.SourceName}\n{sheet.Command}"),
                    Score = sheet.Score,
                    Action = _ =>
                    {
                        Clipboard.SetDataObject(sheet.Command ?? string.Empty);
                        return true;
                    },
                    ContextData = sheet,
                });
            }

            // If nothing matched, show autocomplete suggestions
            if (results.Count == 0)
            {
                var suggestions = _cheatSheetService.GetAutocompleteSuggestions(search) ?? Enumerable.Empty<string>();
                foreach (var s in suggestions.Take(5))
                {
                    results.Add(new Result
                    {
                        QueryTextDisplay = search,
                        IcoPath = IconPath,
                        Title = $"Search for: {s}",
                        SubTitle = "Press Enter to search",
                        Score = 50,
                        Action = _ =>
                        {
                            Context.API.ChangeQuery($"{Context.CurrentPluginMetadata.ActionKeyword} {s}");
                            return false;
                        },
                        ContextData = s,
                    });
                }
            }

            // Fallback: simple copy of the raw query (kept from template behavior)
            if (results.Count == 0)
            {
                results.Add(new Result
                {
                    QueryTextDisplay = search,
                    IcoPath = IconPath,
                    Title = "Copy query to clipboard",
                    SubTitle = "No cheat sheets found. Press Enter to copy your text.",
                    ToolTipData = new ToolTipData("Copy", "Copies your input to clipboard"),
                    Action = _ =>
                    {
                        Clipboard.SetDataObject(search);
                        return true;
                    },
                    ContextData = search,
                });
            }

            return results;
        }

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>.
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin.</param>
        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        /// <summary>
        /// Return a list context menu entries for a given <see cref="Result"/> (shown at the right side of the result).
        /// </summary>
        /// <param name="selectedResult">The <see cref="Result"/> for the list with context menu entries.</param>
        /// <returns>A list context menu entries.</returns>
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var menus = new List<ContextMenuResult>();

            // Unified: copy (template), copy (Enter), open full page (Ctrl+Enter) when we have a CheatSheetItem
            if (selectedResult?.ContextData is CheatSheetItem item)
            {
                // Copy (Enter)
                menus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Copy to clipboard (Enter)",
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE8C8", // Copy
                    AcceleratorKey = Key.Enter,
                    Action = _ =>
                    {
                        Clipboard.SetDataObject(item.Command ?? string.Empty);
                        return true;
                    },
                });

                // Open in browser (Ctrl+Enter)
                menus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Open full page (Ctrl+Enter)",
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE774", // Globe
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ =>
                    {
                        if (!string.IsNullOrWhiteSpace(item.Url))
                        {
                            Helper.OpenInBrowser(item.Url);
                        }
                        return true;
                    },
                });
            }

            // Keep template’s generic copy (Ctrl+C) for plain string ContextData
            if (selectedResult?.ContextData is string search)
            {
                menus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Copy to clipboard (Ctrl+C)",
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE8C8", // Copy
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ =>
                    {
                        Clipboard.SetDataObject(search);
                        return true;
                    },
                });
            }

            return menus;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Wrapper method for <see cref="Dispose()"/> that disposes additional objects and events from the plugin itself.
        /// </summary>
        /// <param name="disposing">Indicate that the plugin is disposed.</param>
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

            _cacheService?.Dispose();
            Disposed = true;
        }

        private void UpdateIconPath(Theme theme) =>
            IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite
                ? "Images/cheatsheets.light.png"
                : "Images/cheatsheets.dark.png";

        private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);

        // --- IPluginI18n ---
        public string GetTranslatedPluginTitle() => "Cheat Sheets Finder";
        public string GetTranslatedPluginDescription() => "Find cheat sheets and command examples instantly";

        // --- ISettingProvider ---
        public Control CreateSettingPanel()
        {
            // Optional: return a real WPF control if you want a custom panel.
            // For now keep it simple; settings are managed via AdditionalOptions.
            return null;
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings == null || settings.AdditionalOptions == null) return;

            foreach (var option in settings.AdditionalOptions)
            {
                try
                {
                    switch (option.Key)
                    {
                        case "EnableDevHints":
                            _enableDevHints = option.Value;
                            break;
                        case "EnableTldr":
                            _enableTldr = option.Value;
                            break;
                        case "EnableCheatSh":
                            _enableCheatSh = option.Value;
                            break;
                        case "CacheDurationHours":
                            _cacheDurationHours = option.Value ? 12 : 2;
                            break;
                    }
                }
                catch
                {
                    // ignore malformed options and continue
                }
            }
        }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>
        {
            new PluginAdditionalOption
            {
                Key = "EnableDevHints",
                DisplayLabel = "Enable DevHints.io",
                Value = true,
            },
            new PluginAdditionalOption
            {
                Key = "EnableTldr",
                DisplayLabel = "Enable TLDR",
                Value = true,
            },
            new PluginAdditionalOption
            {
                Key = "EnableCheatSh",
                DisplayLabel = "Enable cheat.sh",
                Value = true,
            },
            // NOTE: PowerToys exposes AdditionalOptions as bools by default.
            // If you want a true numeric field, define JSON settings schema and bind it.
            // Here we keep the toggle to "enable long cache" as a simple placeholder.
            new PluginAdditionalOption
            {
                Key = "CacheDurationHours",
                DisplayLabel = "Use extended cache duration",
                Value = true,
            },
        };
    }
}
