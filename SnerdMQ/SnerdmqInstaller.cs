using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SnerdMQ
{
    public static class SnerdmqInstaller
    {
        private const string Repo = "greyhands2/snerdmq";
        private const string Version = "v0.1.1";

        public static async Task<string> EnsureDownloadedAsync()
        {
            string platform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                platform = "macos";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                platform = "windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                platform = "linux";
            else
                throw new PlatformNotSupportedException($"[Snerd] Unsupported OS: {RuntimeInformation.OSDescription}");

            string architecture;
            if (RuntimeInformation.OSArchitecture == Architecture.X64)
                architecture = "x64";
            else if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                architecture = "arm64";
            else
                throw new PlatformNotSupportedException($"[Snerd] Unsupported Architecture: {RuntimeInformation.OSArchitecture}");

            string ext = platform == "windows" ? ".exe" : "";
            string binaryName = $"snerdmq-{platform}-{architecture}{ext}";
            string downloadUrl = $"https://github.com/{Repo}/releases/download/{Version}/{binaryName}";

            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string snerdDir = Path.Combine(homeDir, ".snerdmq");
            if (!Directory.Exists(snerdDir))
            {
                Directory.CreateDirectory(snerdDir);
            }

            string destPath = Path.Combine(snerdDir, $"snerdmq{ext}");

            if (File.Exists(destPath))
            {
                return destPath;
            }

            Console.WriteLine($"[Snerd] Downloading pre-compiled engine from GitHub: {binaryName}...");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "SnerdMQ-DotNet-Installer");
                var response = await client.GetAsync(downloadUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"\n[Snerd] WARN: Binary not found at {downloadUrl}");
                    Console.Error.WriteLine("[Snerd] (This is expected if you haven't published a GitHub Release yet)");
                    return null;
                }

                using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            if (platform != "windows")
            {
                // Native chmod since .NET 7/8 File.SetUnixFileMode doesn't exist in netstandard2.0
                var process = System.Diagnostics.Process.Start("chmod", $"+x \"{destPath}\"");
                process?.WaitForExit();
            }

            Console.WriteLine($"[Snerd] Successfully installed Snerd Engine to {destPath}!");
            return destPath;
        }
    }
}
