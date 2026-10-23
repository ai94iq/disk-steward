namespace DiskSteward.Core.Clean;

// Where caches live: each vendor's documented or observed cache layout under the user
// profile. Kept to folders the owning app rebuilds on its own. Left out: anything that needs
// admin (Windows Temp, Prefetch, Windows Update), permanent deletes (emptying the Recycle
// Bin), user files (Downloads), and folders that hold settings, sign-ins or app state
// (browser profiles beyond their caches, chat databases, offline media). One exception on
// purpose: AI tools' chat transcripts older than 30 days (claude-chats, codex-sessions), which
// nothing rebuilds but which grow without bound; they are offered unticked like everything else.
public static class CleanRules
{
    private const string Local = "%LOCALAPPDATA%";
    private const string Roaming = "%APPDATA%";
    private const string Home = "%USERPROFILE%";

    public static IReadOnlyList<CleanTarget> Defaults { get; } =
    [
        // System
        new("temp", CleanCategory.System, "%TEMP%", OlderThanDays: 7),
        new("wer-archive", CleanCategory.System, $@"{Local}\Microsoft\Windows\WER\ReportArchive"),
        new("wer-queue", CleanCategory.System, $@"{Local}\Microsoft\Windows\WER\ReportQueue"),
        new("crash-dumps", CleanCategory.System, $@"{Local}\CrashDumps", CleanMode.Files, "*.dmp"),

        // Browsers
        new("chrome-cache", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\*\Cache"),
        new("chrome-code", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\*\Code Cache"),
        new("chrome-gpu", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\*\GPUCache"),
        new("chrome-sw", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\*\Service Worker\CacheStorage"),
        new("chrome-shader", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\ShaderCache"),
        new("chrome-grshader", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\GrShaderCache"),
        new("edge-cache", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\*\Cache"),
        new("edge-code", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\*\Code Cache"),
        new("edge-gpu", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\*\GPUCache"),
        new("edge-sw", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\*\Service Worker\CacheStorage"),
        new("edge-shader", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\ShaderCache"),
        new("edge-grshader", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\GrShaderCache"),
        new("brave-cache", CleanCategory.Browsers, $@"{Local}\BraveSoftware\Brave-Browser\User Data\*\Cache"),
        new("brave-code", CleanCategory.Browsers, $@"{Local}\BraveSoftware\Brave-Browser\User Data\*\Code Cache"),
        new("brave-gpu", CleanCategory.Browsers, $@"{Local}\BraveSoftware\Brave-Browser\User Data\*\GPUCache"),
        new("vivaldi-cache", CleanCategory.Browsers, $@"{Local}\Vivaldi\User Data\*\Cache"),
        new("vivaldi-code", CleanCategory.Browsers, $@"{Local}\Vivaldi\User Data\*\Code Cache"),
        new("vivaldi-gpu", CleanCategory.Browsers, $@"{Local}\Vivaldi\User Data\*\GPUCache"),
        new("opera-cache", CleanCategory.Browsers, $@"{Local}\Opera Software\Opera Stable\Cache"),
        new("firefox-cache", CleanCategory.Browsers, $@"{Local}\Mozilla\Firefox\Profiles\*\cache2"),
        new("firefox-startup", CleanCategory.Browsers, $@"{Local}\Mozilla\Firefox\Profiles\*\startupCache"),

        // Apps
        new("discord-cache", CleanCategory.Apps, $@"{Roaming}\discord\Cache"),
        new("discord-code", CleanCategory.Apps, $@"{Roaming}\discord\Code Cache"),
        new("discord-gpu", CleanCategory.Apps, $@"{Roaming}\discord\GPUCache"),
        new("slack-cache", CleanCategory.Apps, $@"{Roaming}\Slack\Cache"),
        new("slack-code", CleanCategory.Apps, $@"{Roaming}\Slack\Code Cache"),
        new("slack-gpu", CleanCategory.Apps, $@"{Roaming}\Slack\GPUCache"),
        new("slack-sw", CleanCategory.Apps, $@"{Roaming}\Slack\Service Worker\CacheStorage"),
        new("teams-cache", CleanCategory.Apps, $@"{Roaming}\Microsoft\Teams\Cache"),
        new("teams-code", CleanCategory.Apps, $@"{Roaming}\Microsoft\Teams\Code Cache"),
        new("teams-gpu", CleanCategory.Apps, $@"{Roaming}\Microsoft\Teams\GPUCache"),
        new("vscode-cache", CleanCategory.Apps, $@"{Roaming}\Code\Cache"),
        new("vscode-code", CleanCategory.Apps, $@"{Roaming}\Code\Code Cache"),
        new("vscode-gpu", CleanCategory.Apps, $@"{Roaming}\Code\GPUCache"),
        new("notion-cache", CleanCategory.Apps, $@"{Roaming}\Notion\Cache"),
        new("notion-code", CleanCategory.Apps, $@"{Roaming}\Notion\Code Cache"),
        new("obsidian-cache", CleanCategory.Apps, $@"{Roaming}\obsidian\Cache"),
        new("obsidian-code", CleanCategory.Apps, $@"{Roaming}\obsidian\Code Cache"),
        new("postman-cache", CleanCategory.Apps, $@"{Roaming}\Postman\Cache"),
        new("postman-code", CleanCategory.Apps, $@"{Roaming}\Postman\Code Cache"),
        new("vscode-cached-data", CleanCategory.Apps, $@"{Roaming}\Code\CachedData"),
        new("vscode-vsix", CleanCategory.Apps, $@"{Roaming}\Code\CachedExtensionVSIXs"),
        new("spotify-data", CleanCategory.Apps, $@"{Local}\Spotify\Data"),
        new("steam-htmlcache", CleanCategory.Apps, $@"{Local}\Steam\htmlcache"),
        new("epic-webcache", CleanCategory.Apps, $@"{Local}\EpicGamesLauncher\Saved\webcache*"),
        new("adobe-media-cache", CleanCategory.Apps, $@"{Roaming}\Adobe\Common\Media Cache Files"),
        new("vs-component-cache", CleanCategory.Apps, $@"{Local}\Microsoft\VisualStudio\*\ComponentModelCache"),
        new("jetbrains-caches", CleanCategory.Apps, $@"{Local}\JetBrains\*\caches"),

        // Graphics drivers
        new("nvidia-dx", CleanCategory.Gpu, $@"{Local}\NVIDIA\DXCache"),
        new("nvidia-gl", CleanCategory.Gpu, $@"{Local}\NVIDIA\GLCache"),
        new("amd-dx", CleanCategory.Gpu, $@"{Local}\AMD\DxCache"),
        new("amd-dxc", CleanCategory.Gpu, $@"{Local}\AMD\DxcCache"),
        new("amd-vk", CleanCategory.Gpu, $@"{Local}\AMD\VkCache"),
        new("amd-gl", CleanCategory.Gpu, $@"{Local}\AMD\GLCache"),
        new("intel-shader", CleanCategory.Gpu, $@"{Local}\Intel\ShaderCache"),
        new("d3d-cache", CleanCategory.Gpu, $@"{Local}\D3DSCache"),

        // Developer tools
        new("npm-cache", CleanCategory.Developer, $@"{Local}\npm-cache"),
        new("yarn-cache", CleanCategory.Developer, $@"{Local}\Yarn\Cache"),
        new("pip-cache", CleanCategory.Developer, $@"{Local}\pip\Cache"),
        new("nuget-http", CleanCategory.Developer, $@"{Local}\NuGet\v3-cache"),
        new("nuget-plugins", CleanCategory.Developer, $@"{Local}\NuGet\plugins-cache"),
        new("go-build", CleanCategory.Developer, $@"{Local}\go-build"),
        new("cargo-crates", CleanCategory.Developer, $@"{Home}\.cargo\registry\cache"),
        new("bun-cache", CleanCategory.Developer, $@"{Home}\.bun\install\cache"),
        new("typescript-cache", CleanCategory.Developer, $@"{Local}\Microsoft\TypeScript"),

        // AI tools
        new("claude-chats", CleanCategory.AiTools, $@"{Home}\.claude\projects\*", CleanMode.Files, "*.jsonl", 30),
        new("codex-sessions", CleanCategory.AiTools, $@"{Home}\.codex\sessions\*\*\*", CleanMode.Files, "*.jsonl", 30),
        new("cursor-workspaces", CleanCategory.AiTools, $@"{Roaming}\Cursor\User\workspaceStorage", OlderThanDays: 30),
        new("cursor-cache", CleanCategory.AiTools, $@"{Roaming}\Cursor\Cache"),
        new("cursor-cached-data", CleanCategory.AiTools, $@"{Roaming}\Cursor\CachedData"),
        new("cursor-code", CleanCategory.AiTools, $@"{Roaming}\Cursor\Code Cache"),
        new("cursor-gpu", CleanCategory.AiTools, $@"{Roaming}\Cursor\GPUCache"),
    ];

    // Never cleaned, even when a rule would reach it: Claude Code keeps per-project memory
    // next to its chat logs, and that is notes, not cache.
    public static IReadOnlyList<string> Whitelist { get; } =
    [
        $@"{Home}\.claude\projects\*\memory",
    ];
}
