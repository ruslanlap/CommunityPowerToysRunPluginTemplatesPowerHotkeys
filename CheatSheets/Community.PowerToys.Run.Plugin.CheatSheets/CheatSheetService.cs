// CheatSheetService.cs - Core Service
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.CheatSheets
{
    public class CheatSheetService
{
    private readonly HttpClient _httpClient;
    private readonly CacheService _cacheService;
    private static readonly string[] CommonTopics = new[]
    {
        "git", "docker", "kubernetes", "python", "javascript", "typescript", "react", "vue",
        "angular", "node", "npm", "yarn", "bash", "powershell", "sql", "mongodb", "redis",
        "aws", "azure", "gcp", "linux", "vim", "regex", "css", "html", "java", "c#", "go"
    };

    public CheatSheetService(CacheService cacheService)
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        _cacheService = cacheService;
    }

    public List<CheatSheetItem> SearchCheatSheets(string searchTerm)
    {
        var cacheKey = $"search_{searchTerm}";
        var cachedResults = _cacheService.Get<List<CheatSheetItem>>(cacheKey);

        if (cachedResults != null)
        {
            return cachedResults;
        }

        var results = new List<CheatSheetItem>();

        // Search all sources in parallel
        var tasks = new List<Task<List<CheatSheetItem>>>
        {
            SearchCheatSh(searchTerm),
            SearchDevHints(searchTerm),
            SearchTldr(searchTerm)
        };

        try
        {
            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(3));

            foreach (var task in tasks)
            {
                if (task.IsCompletedSuccessfully)
                {
                    results.AddRange(task.Result);
                }
            }

            // Sort by relevance
            results = results.OrderByDescending(r => r.Score).ToList();

            // Cache results
            _cacheService.Set(cacheKey, results, TimeSpan.FromHours(2));
        }
        catch (Exception)
        {
            // Return partial results if available
        }

        return results;
    }

    private async Task<List<CheatSheetItem>> SearchCheatSh(string searchTerm)
    {
        var results = new List<CheatSheetItem>();

        try
        {
            var url = $"https://cheat.sh/{Uri.EscapeDataString(searchTerm)}?T";
            var response = await _httpClient.GetStringAsync(url);

            var lines = response.Split('\n');
            var currentCommand = "";
            var currentDescription = "";

            foreach (var line in lines.Take(50))
            {
                if (line.StartsWith("#") || line.StartsWith("//"))
                {
                    currentDescription = line.TrimStart('#', '/', ' ');
                }
                else if (!string.IsNullOrWhiteSpace(line) && !line.Contains("cheat.sh"))
                {
                    currentCommand = line.Trim();

                    if (!string.IsNullOrEmpty(currentCommand))
                    {
                        results.Add(new CheatSheetItem
                        {
                            Title = currentCommand.Length > 60 ? currentCommand.Substring(0, 60) + "..." : currentCommand,
                            Description = string.IsNullOrEmpty(currentDescription) ? "From cheat.sh" : currentDescription,
                            Command = currentCommand,
                            Url = $"https://cheat.sh/{Uri.EscapeDataString(searchTerm)}",
                            SourceName = "cheat.sh",
                            Score = CalculateScore(searchTerm, currentCommand, currentDescription)
                        });
                    }
                    currentDescription = "";
                }
            }
        }
        catch (Exception)
        {
            // Fail silently
        }

        return results;
    }

    private async Task<List<CheatSheetItem>> SearchDevHints(string searchTerm)
    {
        var results = new List<CheatSheetItem>();

        try
        {
            var formattedTerm = searchTerm.ToLower().Replace(" ", "-");
            var url = $"https://devhints.io/{formattedTerm}";

            // Try to fetch the page
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                results.Add(new CheatSheetItem
                {
                    Title = $"DevHints: {searchTerm}",
                    Description = "Open comprehensive cheat sheet on DevHints.io",
                    Command = url,
                    Url = url,
                    SourceName = "devhints.io",
                    Score = 80
                });
            }
        }
        catch (Exception)
        {
            // Fail silently
        }

        return results;
    }

    private async Task<List<CheatSheetItem>> SearchTldr(string searchTerm)
    {
        var results = new List<CheatSheetItem>();

        try
        {
            var command = searchTerm.Split(' ')[0];
            var url = $"https://raw.githubusercontent.com/tldr-pages/tldr/main/pages/common/{command}.md";

            var response = await _httpClient.GetStringAsync(url);
            var lines = response.Split('\n');

            string currentDesc = "";
            string currentCmd = "";

            foreach (var line in lines)
            {
                if (line.StartsWith("- "))
                {
                    currentDesc = line.Substring(2).Trim(':').Trim();
                }
                else if (line.StartsWith("`"))
                {
                    currentCmd = line.Trim('`').Trim();

                    if (!string.IsNullOrEmpty(currentCmd))
                    {
                        results.Add(new CheatSheetItem
                        {
                            Title = currentCmd,
                            Description = currentDesc ?? "TLDR example",
                            Command = currentCmd,
                            Url = $"https://tldr.inbrowser.app/pages/common/{command}",
                            SourceName = "tldr",
                            Score = CalculateScore(searchTerm, currentCmd, currentDesc)
                        });
                    }
                }
            }
        }
        catch (Exception)
        {
            // Fail silently
        }

        return results;
    }

    public List<string> GetAutocompleteSuggestions(string searchTerm)
    {
        var suggestions = new List<string>();
        var term = searchTerm.ToLower();

        // Git suggestions
        if (term.StartsWith("git"))
        {
            suggestions.AddRange(new[] { "git reset", "git commit", "git merge", "git rebase", "git stash" });
        }
        // Docker suggestions
        else if (term.StartsWith("docker"))
        {
            suggestions.AddRange(new[] { "docker volume", "docker compose", "docker network", "docker build", "docker run" });
        }
        // General suggestions
        else
        {
            suggestions = CommonTopics.Where(t => t.Contains(term)).Take(5).ToList();
        }

        return suggestions;
    }

    private int CalculateScore(string searchTerm, string command, string description)
    {
        var score = 50;
        var searchLower = searchTerm.ToLower();
        var commandLower = command.ToLower();
        var descLower = description?.ToLower() ?? "";

        // Exact match
        if (commandLower.Contains(searchLower))
        {
            score += 30;
        }

        // Word match
        var searchWords = searchLower.Split(' ');
        foreach (var word in searchWords)
        {
            if (commandLower.Contains(word))
            {
                score += 10;
            }
            if (descLower.Contains(word))
            {
                score += 5;
            }
        }

        return score;
    }
    }
}