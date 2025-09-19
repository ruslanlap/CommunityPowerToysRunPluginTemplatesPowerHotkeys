using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Community.PowerToys.Run.Plugin.CheatSheets;

/// <summary>
/// Core service that queries the remote cheat sheet providers and normalises the results.
/// </summary>
public sealed class CheatSheetService
{
    private static readonly string[] TldrPlatforms = { "common", "linux", "osx", "windows" };

    private static readonly string[] CommonTopics =
    {
        "git", "docker", "kubernetes", "python", "javascript", "typescript", "react", "vue",
        "angular", "node", "npm", "yarn", "bash", "powershell", "sql", "mongodb", "redis",
        "aws", "azure", "gcp", "linux", "vim", "regex", "css", "html", "java", "c#", "go",
        "ssh", "scp", "tmux", "ffmpeg", "curl", "kubectl", "helm", "podman"
    };

    private readonly HttpClient _httpClient;
    private readonly CacheService _cacheService;
    private CheatSheetSourceOptions _options = new();

    public CheatSheetService(CacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CheatSheetsPlugin", "1.0"));
        _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
    }

    public void ConfigureSources(CheatSheetSourceOptions options)
    {
        _options = options ?? new CheatSheetSourceOptions();
    }

    public List<CheatSheetItem> SearchCheatSheets(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<CheatSheetItem>();
        }

        var trimmed = searchTerm.Trim();
        var normalizedKey = trimmed.ToLowerInvariant();
        var cacheKey = $"cheats::{normalizedKey}::{_options.EnableDevHints}_{_options.EnableTldr}_{_options.EnableCheatSh}";

        var cached = _cacheService.Get<List<CheatSheetItem>>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        var tasks = new List<Task<List<CheatSheetItem>>>();

        if (_options.EnableCheatSh)
        {
            tasks.Add(SearchCheatSh(trimmed));
        }

        if (_options.EnableDevHints)
        {
            tasks.Add(SearchDevHints(trimmed));
        }

        if (_options.EnableTldr)
        {
            tasks.Add(SearchTldr(trimmed));
        }

        if (tasks.Count == 0)
        {
            return new List<CheatSheetItem>();
        }

        try
        {
            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(8));
        }
        catch (AggregateException)
        {
            // Individual tasks will surface their partial results if completed.
        }
        catch (Exception)
        {
            // Ignore unexpected wait issues and continue with whatever finished.
        }

        var combined = new List<CheatSheetItem>();
        foreach (var task in tasks)
        {
            if (task.Status == TaskStatus.RanToCompletion && task.Result is { Count: > 0 })
            {
                combined.AddRange(task.Result);
            }
        }

        var deduped = combined
            .GroupBy(item => $"{item.SourceName}|{item.Command}")
            .Select(group => group.OrderByDescending(x => x.Score).First())
            .OrderByDescending(item => item.Score)
            .ToList();

        if (deduped.Count > 0)
        {
            var cacheDuration = _options.CacheDuration > TimeSpan.Zero
                ? _options.CacheDuration
                : TimeSpan.FromHours(2);

            _cacheService.Set(cacheKey, deduped, cacheDuration);
        }

        return deduped;
    }

    public List<string> GetAutocompleteSuggestions(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<string>();
        }

        var term = searchTerm.ToLowerInvariant();

        if (term.StartsWith("git", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string> { "git reset", "git commit", "git merge", "git rebase", "git stash" };
        }

        if (term.StartsWith("docker", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string> { "docker build", "docker compose", "docker run", "docker volume", "docker network" };
        }

        return CommonTopics
            .Where(topic => topic.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(5)
            .ToList();
    }

    private async Task<List<CheatSheetItem>> SearchCheatSh(string searchTerm)
    {
        var results = new List<CheatSheetItem>();

        try
        {
            var encodedQuery = Uri.EscapeDataString(searchTerm).Replace("%20", "+");
            var requestUrl = $"https://cheat.sh/{encodedQuery}?T";
            using var response = await _httpClient.GetAsync(requestUrl).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return results;
            }

            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return results;
            }

            var lines = payload.Split('\n');
            var currentDescription = string.Empty;

            foreach (var raw in lines)
            {
                if (results.Count >= 10)
                {
                    break;
                }

                var line = raw.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("#") || line.StartsWith("//") || line.StartsWith(">"))
                {
                    currentDescription = line.TrimStart('#', '/', '>', ' ').Trim();
                    continue;
                }

                if (line.Contains("://cheat.sh", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var cleaned = line.StartsWith("$") ? line.TrimStart('$').TrimStart() : line;
                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    continue;
                }

                results.Add(new CheatSheetItem
                {
                    Title = Truncate(cleaned, 80),
                    Description = string.IsNullOrWhiteSpace(currentDescription) ? "From cheat.sh" : currentDescription,
                    Command = cleaned,
                    Url = $"https://cheat.sh/{encodedQuery}",
                    SourceName = "cheat.sh",
                    Score = CalculateScore(searchTerm, cleaned, currentDescription)
                });

                currentDescription = string.Empty;
            }
        }
        catch (Exception)
        {
            // Ignore network failures.
        }

        return results;
    }

    private async Task<List<CheatSheetItem>> SearchDevHints(string searchTerm)
    {
        var results = new List<CheatSheetItem>();

        try
        {
            var slug = Slugify(searchTerm);
            if (string.IsNullOrWhiteSpace(slug))
            {
                return results;
            }

            var rawUrl = $"https://raw.githubusercontent.com/rstacruz/cheatsheets/master/{slug}.md";
            using var response = await _httpClient.GetAsync(rawUrl).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return results;
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(content))
            {
                return results;
            }

            var url = $"https://devhints.io/{slug}";
            var lines = content.Split('\n');
            string title = null;
            var codeLines = new List<string>();
            var descriptionBuilder = new StringBuilder();
            var hasCapturedSection = false;
            var insideCodeBlock = false;

            void CommitSection()
            {
                if (string.IsNullOrWhiteSpace(title) || codeLines.Count == 0)
                {
                    return;
                }

                var commandLine = codeLines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    return;
                }

                var description = descriptionBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(description))
                {
                    description = "Snippet from DevHints";
                }

                results.Add(new CheatSheetItem
                {
                    Title = title,
                    Description = description,
                    Command = commandLine,
                    Url = url,
                    SourceName = "DevHints",
                    Score = CalculateScore(searchTerm, commandLine, description)
                });

                hasCapturedSection = true;
            }

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');

                if (line.StartsWith("---", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    CommitSection();

                    title = line[4..].Trim();
                    if (title.StartsWith("`", StringComparison.Ordinal) && title.EndsWith("`", StringComparison.Ordinal))
                    {
                        title = title.Trim('`', ' ');
                    }

                    codeLines.Clear();
                    descriptionBuilder.Clear();
                    insideCodeBlock = false;
                    continue;
                }

                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    insideCodeBlock = !insideCodeBlock;
                    continue;
                }

                if (insideCodeBlock)
                {
                    codeLines.Add(line.Trim());
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("####", StringComparison.Ordinal))
                {
                    continue;
                }

                if (descriptionBuilder.Length == 0)
                {
                    descriptionBuilder.Append(line.Trim());
                }
            }

            CommitSection();

            if (!hasCapturedSection)
            {
                // Provide at least a link to DevHints so the user can open the full page.
                results.Add(new CheatSheetItem
                {
                    Title = $"Open DevHints page for {searchTerm}",
                    Description = "Open the DevHints cheat sheet in your browser.",
                    Command = url,
                    Url = url,
                    SourceName = "DevHints",
                    Score = 40
                });
            }
        }
        catch (Exception)
        {
            // Ignore network errors and continue.
        }

        return results;
    }

    private async Task<List<CheatSheetItem>> SearchTldr(string searchTerm)
    {
        var results = new List<CheatSheetItem>();

        try
        {
            var commandToken = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(commandToken))
            {
                return results;
            }

            foreach (var platform in TldrPlatforms)
            {
                var rawUrl = $"https://raw.githubusercontent.com/tldr-pages/tldr/main/pages/{platform}/{commandToken}.md";
                using var response = await _httpClient.GetAsync(rawUrl).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                string description = null;
                foreach (var rawLine in content.Split('\n'))
                {
                    var line = rawLine.Trim();

                    if (line.StartsWith("- ", StringComparison.Ordinal))
                    {
                        description = line[2..].Trim();
                    }
                    else if (line.StartsWith("`", StringComparison.Ordinal) && description != null)
                    {
                        var command = line.Trim('`').Trim();
                        results.Add(new CheatSheetItem
                        {
                            Title = command,
                            Description = description,
                            Command = command,
                            Url = $"https://tldr.inbrowser.app/pages/{platform}/{commandToken}",
                            SourceName = $"tldr ({platform})",
                            Score = CalculateScore(searchTerm, command, description)
                        });

                        description = null;
                    }
                }

                if (results.Count > 0)
                {
                    break; // Prefer the first platform that matches.
                }
            }
        }
        catch (Exception)
        {
            // Ignore network failures.
        }

        return results;
    }

    private static int CalculateScore(string searchTerm, string command, string description)
    {
        var score = 50;
        var searchLower = searchTerm.ToLowerInvariant();
        var commandLower = command?.ToLowerInvariant() ?? string.Empty;
        var descriptionLower = description?.ToLowerInvariant() ?? string.Empty;

        if (commandLower.Contains(searchLower, StringComparison.Ordinal))
        {
            score += 30;
        }

        var searchWords = searchLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in searchWords)
        {
            if (commandLower.Contains(word, StringComparison.Ordinal))
            {
                score += 10;
            }

            if (descriptionLower.Contains(word, StringComparison.Ordinal))
            {
                score += 5;
            }
        }

        return score;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }

    private static string Slugify(string term)
    {
        var builder = new StringBuilder();
        foreach (var c in term.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '.')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug;
    }
}
