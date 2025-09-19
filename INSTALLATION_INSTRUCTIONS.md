# CheatSheets PowerToys Run Plugin - Installation Instructions

## 📦 What's Fixed

The main issue was **incorrect package structure**. PowerToys Run was unable to detect the plugin because:

- ❌ **Before**: Files were packaged at root level  
- ✅ **Now**: Files are properly packaged in `CheatSheets/` folder

## 🚀 Installation Steps

1. **Close PowerToys completely** (right-click PowerToys icon in system tray → Exit)

2. **Download the plugin**:
   - For x64 systems: `CheatSheets-1.0.0-x64.zip` (7.6 MB)
   - For ARM64 systems: `CheatSheets-1.0.0-arm64.zip` (7.6 MB)

3. **Navigate to PowerToys plugins directory**:
   ```
   %LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\
   ```
   Full path example: `C:\Users\YourName\AppData\Local\Microsoft\PowerToys\PowerToys Run\Plugins\`

4. **Extract the plugin**:
   - Extract the ZIP file directly to the Plugins folder
   - You should see a `CheatSheets` folder with all the plugin files inside

5. **Start PowerToys** again

6. **Test the plugin**:
   - Press `Alt + Space` to open PowerToys Run
   - Type `cs git reset` to search for git reset cheat sheets
   - You should see results from devhints.io, tldr.sh, and cheat.sh

## 🎯 Plugin Features

- **Action Keyword**: `cs` (e.g., `cs docker`, `cs python regex`)
- **Multiple Sources**: Searches devhints.io, tldr.sh, and cheat.sh simultaneously
- **Caching**: Results cached for 2 hours for faster subsequent searches
- **Auto-complete**: Provides search suggestions as you type

## 🔧 Troubleshooting

If the plugin doesn't appear:

1. **Check folder structure**:
   ```
   Plugins\
     └── CheatSheets\
         ├── plugin.json
         ├── Community.PowerToys.Run.Plugin.CheatSheets.dll
         ├── Community.PowerToys.Run.Plugin.CheatSheets.deps.json
         ├── Images\
         │   ├── cheatsheets.light.png
         │   └── cheatsheets.dark.png
         └── [other DLL files]
   ```

2. **Check PowerToys Run settings**:
   - Open PowerToys Settings
   - Go to PowerToys Run
   - Look for "CheatSheets" in the plugins list
   - Make sure it's enabled

3. **Check logs** (if needed):
   - Logs location: `%LocalAppData%\Microsoft\PowerToys\PowerToys Run\Logs\`

## 📋 Technical Details

- **Target Framework**: .NET 9
- **PowerToys Dependencies**: v0.91.0
- **Plugin ID**: `41BF0604C51A4974A0BAA108826D0A94`
- **Compatible with**: PowerToys v0.87.1 and later

---

The plugin should now be properly detected and loaded by PowerToys Run! 🎉