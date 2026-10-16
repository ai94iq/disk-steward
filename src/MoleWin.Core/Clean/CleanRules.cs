namespace MoleWin.Core.Clean;

// Where caches live, ported from the MIT-licensed mo 1.30.0 Windows rules
// (lib/clean/*.ps1, lib/core/base.ps1; notice in third_party/). Kept: what a normal user may
// clean. Left out: targets that need admin (Windows Temp, Prefetch, Windows Update), permanent
// deletes (emptying the Recycle Bin), user documents (old Downloads), and folders that hold
// app state rather than cache (Teams databases, Office file cache, Zoom data, clipboard).
public static class CleanRules
{
    private const string Local = "%LOCALAPPDATA%";
    private const string Roaming = "%APPDATA%";
    private const string Home = "%USERPROFILE%";

    public static IReadOnlyList<CleanTarget> Defaults { get; } =
    [
        // System
        new("temp", CleanCategory.System, "%TEMP%", OlderThanDays: 7),
        new("error-reports", CleanCategory.System, $@"{Local}\Microsoft\Windows\WER", OlderThanDays: 7),
        new("crash-dumps", CleanCategory.System, $@"{Local}\CrashDumps", CleanMode.Files, "*.dmp"),
        new("roaming-logs", CleanCategory.System, Roaming, CleanMode.Files, "*.log", 7),
        new("home-logs", CleanCategory.System, Home, CleanMode.Files, "*.log", 7),
        new("onedrive-logs", CleanCategory.System, $@"{Local}\Microsoft\OneDrive\logs", OlderThanDays: 7),
        new("drivefs-logs", CleanCategory.System, $@"{Local}\Google\DriveFS\Logs", OlderThanDays: 7),

        // Browsers
        new("chrome-cache", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\Default\Cache"),
        new("chrome-code", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\Default\Code Cache"),
        new("chrome-gpu", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\Default\GPUCache"),
        new("chrome-sw", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\Default\Service Worker\CacheStorage"),
        new("chrome-shader", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\ShaderCache"),
        new("chrome-grshader", CleanCategory.Browsers, $@"{Local}\Google\Chrome\User Data\GrShaderCache"),
        new("edge-cache", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\Default\Cache"),
        new("edge-code", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\Default\Code Cache"),
        new("edge-gpu", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\Default\GPUCache"),
        new("edge-sw", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\Default\Service Worker\CacheStorage"),
        new("edge-shader", CleanCategory.Browsers, $@"{Local}\Microsoft\Edge\User Data\ShaderCache"),
        new("firefox-cache", CleanCategory.Browsers, $@"{Local}\Mozilla\Firefox\Profiles\*\cache2"),
        new("brave-cache", CleanCategory.Browsers, $@"{Local}\BraveSoftware\Brave-Browser\User Data\Default\Cache"),
        new("opera-cache", CleanCategory.Browsers, $@"{Roaming}\Opera Software\Opera Stable\Cache"),

        // Apps
        new("spotify-data", CleanCategory.Apps, $@"{Local}\Spotify\Data"),
        new("spotify-storage", CleanCategory.Apps, $@"{Local}\Spotify\Storage"),
        new("discord-cache", CleanCategory.Apps, $@"{Roaming}\discord\Cache"),
        new("discord-code", CleanCategory.Apps, $@"{Roaming}\discord\Code Cache"),
        new("discord-gpu", CleanCategory.Apps, $@"{Roaming}\discord\GPUCache"),
        new("slack-cache", CleanCategory.Apps, $@"{Roaming}\Slack\Cache"),
        new("slack-code", CleanCategory.Apps, $@"{Roaming}\Slack\Code Cache"),
        new("slack-gpu", CleanCategory.Apps, $@"{Roaming}\Slack\GPUCache"),
        new("slack-sw", CleanCategory.Apps, $@"{Roaming}\Slack\Service Worker\CacheStorage"),
        new("teams-cache", CleanCategory.Apps, $@"{Roaming}\Microsoft\Teams\Cache"),
        new("teams-gpu", CleanCategory.Apps, $@"{Roaming}\Microsoft\Teams\GPUCache"),
        new("teams-tmp", CleanCategory.Apps, $@"{Roaming}\Microsoft\Teams\tmp"),
        new("vscode-cache", CleanCategory.Apps, $@"{Roaming}\Code\Cache"),
        new("vscode-cached-data", CleanCategory.Apps, $@"{Roaming}\Code\CachedData"),
        new("vscode-cached-ext", CleanCategory.Apps, $@"{Roaming}\Code\CachedExtensionVSIXs"),
        new("vscode-code", CleanCategory.Apps, $@"{Roaming}\Code\Code Cache"),
        new("vscode-gpu", CleanCategory.Apps, $@"{Roaming}\Code\GPUCache"),
        new("vscode-insiders-cache", CleanCategory.Apps, $@"{Roaming}\Code - Insiders\Cache"),
        new("vscode-insiders-data", CleanCategory.Apps, $@"{Roaming}\Code - Insiders\CachedData"),
        new("adobe-media", CleanCategory.Apps, $@"{Roaming}\Adobe\Common\Media Cache Files"),
        new("adobe-peak", CleanCategory.Apps, $@"{Roaming}\Adobe\Common\Peak Files"),
        new("epic-webcache", CleanCategory.Apps, $@"{Local}\EpicGamesLauncher\Saved\webcache"),
        new("office-wef", CleanCategory.Apps, $@"{Local}\Microsoft\Office\16.0\Wef"),
        new("zed-cache", CleanCategory.Apps, $@"{Local}\Zed\cache"),
        new("sublime-cache", CleanCategory.Apps, $@"{Roaming}\Sublime Text\Cache"),
        new("vs-component-cache", CleanCategory.Apps, $@"{Local}\Microsoft\VisualStudio\*\ComponentModelCache"),
        new("vs-image-cache", CleanCategory.Apps, $@"{Local}\Microsoft\VisualStudio\*\ImageCache"),

        // GPU shader caches (rebuilt by the driver on demand)
        new("nvidia-dx", CleanCategory.Gpu, $@"{Local}\NVIDIA\DXCache"),
        new("nvidia-gl", CleanCategory.Gpu, $@"{Local}\NVIDIA\GLCache"),
        new("nvidia-nv", CleanCategory.Gpu, $@"{Local}\NVIDIA Corporation\NV_Cache"),
        new("amd-dx", CleanCategory.Gpu, $@"{Local}\AMD\DXCache"),
        new("amd-gl", CleanCategory.Gpu, $@"{Local}\AMD\GLCache"),
        new("amd-vk", CleanCategory.Gpu, $@"{Local}\AMD\VkCache"),
        new("intel-shader", CleanCategory.Gpu, $@"{Local}\Intel\ShaderCache"),
        new("d3d-cache", CleanCategory.Gpu, $@"{Local}\D3DSCache"),
        new("dx-pipeline", CleanCategory.Gpu, $@"{Local}\Microsoft\DirectX Shader Cache"),
        new("vulkan-cache", CleanCategory.Gpu, $@"{Local}\VulkanCache"),

        // Developer tools (package managers download again when needed)
        new("npm-cache", CleanCategory.Developer, $@"{Local}\npm-cache"),
        new("npm-cache-roaming", CleanCategory.Developer, $@"{Roaming}\npm-cache"),
        new("yarn-cache", CleanCategory.Developer, $@"{Local}\Yarn\Cache"),
        new("yarn-berry-cache", CleanCategory.Developer, $@"{Home}\.yarn\cache"),
        new("bun-cache", CleanCategory.Developer, $@"{Home}\.bun\install\cache"),
        new("node-gyp", CleanCategory.Developer, $@"{Local}\node-gyp\Cache"),
        new("electron-cache", CleanCategory.Developer, $@"{Local}\electron\Cache"),
        new("typescript-cache", CleanCategory.Developer, $@"{Local}\Microsoft\TypeScript"),
        new("pip-cache", CleanCategory.Developer, $@"{Local}\pip\Cache"),
        new("pyenv-cache", CleanCategory.Developer, $@"{Home}\.pyenv\cache"),
        new("poetry-cache", CleanCategory.Developer, $@"{Local}\pypoetry\Cache"),
        new("nuget-http", CleanCategory.Developer, $@"{Local}\NuGet\v3-cache"),
        new("nuget-plugins", CleanCategory.Developer, $@"{Local}\NuGet\plugins-cache"),
        new("go-mod-cache", CleanCategory.Developer, $@"{Home}\go\pkg\mod\cache"),
        new("aws-cli", CleanCategory.Developer, $@"{Home}\.aws\cli\cache"),
        new("azure-logs", CleanCategory.Developer, $@"{Home}\.azure\logs"),
        new("gcloud-logs", CleanCategory.Developer, $@"{Roaming}\gcloud\logs"),
        new("kube-cache", CleanCategory.Developer, $@"{Home}\.kube\cache"),
        new("terraform-cache", CleanCategory.Developer, $@"{Roaming}\terraform.d\plugin-cache"),
        new("hex-cache", CleanCategory.Developer, $@"{Home}\.hex\cache"),
        new("opam-download", CleanCategory.Developer, $@"{Home}\.opam\download-cache"),
    ];

    // mo 1.30.0's default whitelist: never cleaned, even when a rule would reach it.
    public static IReadOnlyList<string> Whitelist { get; } =
    [
        $@"{Local}\Microsoft\Windows\Explorer",
        $@"{Local}\Microsoft\Windows\Fonts",
        $@"{Roaming}\Microsoft\Windows\Recent",
        $@"{Local}\Packages\*",
        $@"{Home}\.vscode\extensions",
        $@"{Home}\.nuget",
        $@"{Home}\.cargo",
        $@"{Home}\.rustup",
        $@"{Home}\.m2\repository",
        $@"{Home}\.gradle\caches\modules-2\files-*",
        $@"{Home}\.ollama\models",
        $@"{Local}\JetBrains",
    ];
}
