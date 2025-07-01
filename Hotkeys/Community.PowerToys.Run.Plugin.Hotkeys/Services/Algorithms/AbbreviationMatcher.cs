// ===== 6. Services/Algorithms/AbbreviationMatcher.cs =====
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Community.PowerToys.Run.Plugin.Hotkeys.Models;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services.Algorithms
{
    public class AbbreviationMatcher : IAbbreviationMatcher
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, string> _commonAbbreviations;

        public AbbreviationMatcher(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _commonAbbreviations = InitializeCommonAbbreviations();
        }

        private Dictionary<string, string> InitializeCommonAbbreviations()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Common shortcuts
                {"ctrl", "control"},
                {"alt", "alternate"},
                {"del", "delete"},
                {"ins", "insert"},
                {"pg", "page"},
                {"num", "numeric"},
                {"caps", "capslock"},
                {"tab", "tabulate"},
                {"esc", "escape"},

                // Common actions
                {"cp", "copy"},
                {"mv", "move"},
                {"rm", "remove"},
                {"sv", "save"},
                {"op", "open"},
                {"cl", "close"},
                {"pr", "print"},
                {"fd", "find"},
                {"rp", "replace"},
                {"sl", "select"},
                {"undo", "undo"},
                {"redo", "redo"},

                // Applications
                {"vs", "visual studio"},
                {"vsc", "visual studio code"},
                {"chrome", "google chrome"},
                {"ff", "firefox"},
                {"edge", "microsoft edge"},
                {"ps", "photoshop"},
                {"ai", "illustrator"},
                {"xl", "excel"},
                {"wd", "word"},
                {"pp", "powerpoint"},
                {"ot", "outlook"},
                {"tm", "teams"},
                {"sk", "skype"},

                // Directions
                {"l", "left"},
                {"r", "right"},
                {"u", "up"},
                {"d", "down"},
                {"beg", "beginning"},
                {"end", "end"},

                // Common tech terms
                {"db", "database"},
                {"api", "application programming interface"},
                {"ui", "user interface"},
                {"ux", "user experience"},
                {"css", "cascading style sheets"},
                {"js", "javascript"},
                {"ts", "typescript"},
                {"sql", "structured query language"},
                {"html", "hypertext markup language"},
                {"xml", "extensible markup language"},
                {"json", "javascript object notation"},
                {"http", "hypertext transfer protocol"},
                {"https", "hypertext transfer protocol secure"},
                {"ftp", "file transfer protocol"},
                {"ssh", "secure shell"},
                {"ssl", "secure sockets layer"},
                {"tls", "transport layer security"},
                {"tcp", "transmission control protocol"},
                {"udp", "user datagram protocol"},
                {"ip", "internet protocol"},
                {"dns", "domain name system"},
                {"url", "uniform resource locator"},
                {"uri", "uniform resource identifier"},
                {"gpu", "graphics processing unit"},
                {"cpu", "central processing unit"},
                {"ram", "random access memory"},
                {"ssd", "solid state drive"},
                {"hdd", "hard disk drive"},
                {"usb", "universal serial bus"},
                {"wifi", "wireless fidelity"},
                {"lan", "local area network"},
                {"wan", "wide area network"},
                {"vpn", "virtual private network"}
            };
        }

        public bool IsAbbreviationMatch(string abbreviation, string fullText)
        {
            if (string.IsNullOrWhiteSpace(abbreviation) || string.IsNullOrWhiteSpace(fullText))
                return false;

            abbreviation = abbreviation.ToLowerInvariant().Trim();
            fullText = fullText.ToLowerInvariant().Trim();

            // Check common abbreviations first
            if (_commonAbbreviations.ContainsKey(abbreviation))
            {
                var expandedAbbr = _commonAbbreviations[abbreviation];
                return fullText.Contains(expandedAbbr);
            }

            // Check if abbreviation matches first letters of words
            var words = Regex.Split(fullText, @"[\s\-_+]+")
                             .Where(w => !string.IsNullOrWhiteSpace(w))
                             .ToArray();

            if (words.Length == 0) return false;

            // Try to match first letters
            if (IsFirstLetterMatch(abbreviation, words))
                return true;

            // Try to match as subsequence
            return IsSubsequenceMatch(abbreviation, fullText);
        }

        private bool IsFirstLetterMatch(string abbreviation, string[] words)
        {
            if (abbreviation.Length > words.Length)
                return false;

            for (int i = 0; i < abbreviation.Length; i++)
            {
                if (words[i].Length == 0 || words[i][0] != abbreviation[i])
                    return false;
            }

            return true;
        }

        private bool IsSubsequenceMatch(string abbreviation, string text)
        {
            int abbIndex = 0;
            int textIndex = 0;

            while (abbIndex < abbreviation.Length && textIndex < text.Length)
            {
                if (abbreviation[abbIndex] == text[textIndex])
                {
                    abbIndex++;
                }
                textIndex++;
            }

            return abbIndex == abbreviation.Length;
        }

        public List<SearchResult> FindAbbreviationMatches(string query, IEnumerable<ShortcutInfo> shortcuts)
        {
            var results = new List<SearchResult>();

            foreach (var shortcut in shortcuts)
            {
                var matchedFields = new List<string>();
                var matchedTerms = new List<string>();

                // Check shortcut
                if (IsAbbreviationMatch(query, shortcut.Shortcut))
                {
                    matchedFields.Add("Shortcut");
                    matchedTerms.Add(shortcut.Shortcut);
                }

                // Check description
                if (IsAbbreviationMatch(query, shortcut.Description))
                {
                    matchedFields.Add("Description");
                    matchedTerms.Add(shortcut.Description);
                }

                // Check keywords
                if (shortcut.Keywords?.Any() == true)
                {
                    foreach (var keyword in shortcut.Keywords)
                    {
                        if (IsAbbreviationMatch(query, keyword))
                        {
                            matchedFields.Add("Keywords");
                            matchedTerms.Add(keyword);
                        }
                    }
                }

                // Check aliases
                if (shortcut.Aliases?.Any() == true)
                {
                    foreach (var alias in shortcut.Aliases)
                    {
                        if (IsAbbreviationMatch(query, alias))
                        {
                            matchedFields.Add("Aliases");
                            matchedTerms.Add(alias);
                        }
                    }
                }

                // Check source/app name
                if (IsAbbreviationMatch(query, shortcut.Source))
                {
                    matchedFields.Add("Source");
                    matchedTerms.Add(shortcut.Source);
                }

                if (matchedFields.Any())
                {
                    results.Add(new SearchResult
                    {
                        Shortcut = shortcut,
                        Score = CalculateAbbreviationScore(query, matchedTerms),
                        MatchType = SearchMatchType.AbbreviationMatch,
                        MatchedField = string.Join(", ", matchedFields),
                        MatchedTerms = matchedTerms
                    });
                }
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        private double CalculateAbbreviationScore(string query, List<string> matchedTerms)
        {
            if (!matchedTerms.Any()) return 0.0;

            // Base score for abbreviation match
            double baseScore = 70.0;

            // Bonus for exact length match (abbreviation perfectly matches word count)
            var bestMatch = matchedTerms.First();
            var words = Regex.Split(bestMatch, @"[\s\-_+]+")
                             .Where(w => !string.IsNullOrWhiteSpace(w))
                             .ToArray();

            if (query.Length == words.Length)
                baseScore += 20.0;

            // Bonus for common abbreviations
            if (_commonAbbreviations.ContainsKey(query.ToLowerInvariant()))
                baseScore += 15.0;

            return Math.Min(100.0, baseScore);
        }

        public string GenerateAbbreviation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var words = Regex.Split(text, @"[\s\-_+]+")
                             .Where(w => !string.IsNullOrWhiteSpace(w))
                             .ToArray();

            if (words.Length == 1)
            {
                // For single words, take first 2-3 letters
                return text.Length >= 3 ? text.Substring(0, 3).ToLowerInvariant() : text.ToLowerInvariant();
            }

            // For multiple words, take first letter of each
            return string.Join("", words.Select(w => w[0])).ToLowerInvariant();
        }
    }
}