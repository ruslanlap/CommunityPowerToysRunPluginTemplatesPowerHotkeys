// Main.cs - Plugin Entry Point
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Wox.Plugin;
using ManagedCommon;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Community.PowerToys.Run.Plugin.CheatSheets
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider, IDisposable
    {
        public static string PluginID => "B8C8F8E9A1B2C3D4E5F6G7H8I9J0K1L2";
        private string IconPath { get; set; }
        private PluginInitContext Context { get; set; }
        private bool Disposed { get; set; }

        private readonly CheatSheetService _cheatSheetService;
        private readonly CacheService _cacheService;

        public string Name => "Cheat Sheets Finder";
        public string Description => "Find cheat sheets and command examples instantly";

        public Main()
        {
            _cacheService = new CacheService();
            _cheatSheetService = new CheatSheetService(_cacheService);
        }

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return GetDefaultResults();
            }

            var results = new List<Result>();
            var searchTerm = query.Search.Trim();

            // Get cheat sheet results from all sources
            var cheatSheets = _cheatSheetService.SearchCheatSheets(searchTerm);

            foreach (var sheet in cheatSheets.Take(7))
            {
                results.Add(new Result
                {
                    Title = sheet.Title,
                    SubTitle = sheet.Description,
                    IcoPath = IconPath,
                    Score = sheet.Score,
                    Action = c =>
                    {
                        System.Windows.Clipboard.SetText(sheet.Command);
                        return true;
                    },
                    ContextData = sheet
                });
            }

            // Add autocomplete suggestions
            if (results.Count == 0)
            {
                var suggestions = _cheatSheetService.GetAutocompleteSuggestions(searchTerm);
                foreach (var suggestion in suggestions.Take(5))
                {
                    results.Add(new Result
                    {
                        Title = $"Search for: {suggestion}",
                        SubTitle = "Press Enter to search",
                        IcoPath = IconPath,
                        Score = 50,
                        Action = c =>
                        {
                            Context.API.ChangeQuery($"{Context.CurrentPluginMetadata.ActionKeyword} {suggestion}");
                            return false;
                        }
                    });
                }
            }

            return results;
        }

        private List<Result> GetDefaultResults()
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "Cheat Sheets Finder",
                    SubTitle = "Type a command or technology name to search (e.g., 'git reset', 'docker volume')",
                    IcoPath = IconPath,
                    Score = 100
                }
            };
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        private void UpdateIconPath(Theme theme)
        {
            IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite 
                ? "Images/cheatsheet.light.png" 
                : "Images/cheatsheet.dark.png";
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public string GetTranslatedPluginTitle()
        {
            return "Cheat Sheets Finder";
        }

        public string GetTranslatedPluginDescription()
        {
            return "Find cheat sheets and command examples instantly";
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<ContextMenuResult>();

            if (selectedResult?.ContextData is CheatSheetItem item)
            {
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Copy to clipboard (Enter)",
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE8C8", // Copy icon
                    AcceleratorKey = Key.Enter,
                    Action = _ =>
                    {
                        System.Windows.Clipboard.SetText(item.Command);
                        return true;
                    }
                });

                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Open full page (Ctrl+Enter)",
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE774", // Globe icon
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ =>
                    {
                        Helper.OpenInBrowser(item.Url);
                        return true;
                    }
                });
            }

            return contextMenus;
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            // This method is required by ISettingProvider interface
            // It's called when plugin settings are updated
            // For this implementation, we don't need to handle settings updates
        }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>
        {
            new PluginAdditionalOption
            {
                Key = "EnableDevHints",
                DisplayLabel = "Enable DevHints.io",
                Value = true
            },
            new PluginAdditionalOption
            {
                Key = "EnableTldr",
                DisplayLabel = "Enable TLDR",
                Value = true
            },
            new PluginAdditionalOption
            {
                Key = "EnableCheatSh",
                DisplayLabel = "Enable Cheat.sh",
                Value = true
            },
            new PluginAdditionalOption
            {
                Key = "CacheDurationHours",
                DisplayLabel = "Cache Duration (hours)",
                Value = true
            }
        };

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed && disposing)
            {
                if (Context?.API != null)
                {
                    Context.API.ThemeChanged -= OnThemeChanged;
                }
                _cacheService?.Dispose();
                Disposed = true;
            }
        }
    }
    }