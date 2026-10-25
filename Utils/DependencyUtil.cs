using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using RFRocketLibrary.Constants;
using RFRocketLibrary.Enum;
using Logger = Rocket.Core.Logging.Logger;

namespace RFRocketLibrary.Utils
{
    public static class DependencyUtil
    {
        #region Methods

        /// <summary>
        /// Allowed download hosts for dependency assemblies.
        /// </summary>
        private static readonly string[] AllowedHosts = { "cdn.jsdelivr.net", "raw.githubusercontent.com", "gitlab.com" };

        /// <summary>
        /// Validates a dependency download URL before any network request is made:
        /// only https is allowed, the host must be whitelisted, and the resolved
        /// addresses must not point at loopback/private/reserved ranges (blocks
        /// misconfigured links and trivial DNS rebinding toward internal services).
        /// </summary>
        public static void ValidateDependencyUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException($"Blocked dependency URL (https only): {url}");

            if (!AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Blocked dependency URL host '{uri.Host}' (not whitelisted).");

            foreach (var address in Dns.GetHostAddresses(uri.Host))
            {
                if (IPAddress.IsLoopback(address)
                    || (address.AddressFamily == AddressFamily.InterNetwork && (
                           address.GetAddressBytes()[0] == 10
                           || (address.GetAddressBytes()[0] == 172 && address.GetAddressBytes()[1] >= 16 && address.GetAddressBytes()[1] <= 31)
                           || (address.GetAddressBytes()[0] == 192 && address.GetAddressBytes()[1] == 168)
                           || (address.GetAddressBytes()[0] == 169 && address.GetAddressBytes()[1] == 254)))
                    || (address.AddressFamily == AddressFamily.InterNetworkV6 && (
                           address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)))
                    throw new InvalidOperationException(
                        $"Blocked dependency URL '{url}': host resolves to a loopback/private/reserved address ({address}).");
            }
        }

        private static byte[] DownloadAssembly(WebClient client, string url)
        {
            ValidateDependencyUrl(url);
            return client.DownloadData(url);
        }

        // Logging goes through Rocket's chain, which needs the game engine; on hosts without
        // it the report must not replace the original failure with a crash.
        private static void LogWarningSafe(string message)
        {
            try
            {
                Logger.LogWarning(message);
            }
            catch
            {
                // Ignore the reporter failure.
            }
        }

        private static void LogErrorSafe(string message)
        {
            try
            {
                Logger.LogError(message);
            }
            catch
            {
                // Ignore the reporter failure.
            }
        }

        private static async Task<byte[]> DownloadAssemblyAsync(WebClient client, string url)
        {
            ValidateDependencyUrl(url);
            return await client.DownloadDataTaskAsync(url);
        }

        private static string GetDependencyLink(EDependency dependency, bool mirror)
        {
            var field = typeof(DependencyLink).GetField(dependency + (mirror ? "Mirror" : string.Empty));
            if (field?.GetValue(null) is not string url || url.Length == 0)
                throw new InvalidOperationException(
                    $"No download link is registered for dependency '{dependency}'{(mirror ? " (mirror)" : string.Empty)}.");
            return url;
        }

        private static string? GetExpectedIntegrity(EDependency dependency)
        {
            return typeof(DependencyIntegrity).GetField(dependency.ToString())?.GetValue(null) as string;
        }

        /// <summary>
        /// Reads a cached dependency assembly and verifies it against the expected
        /// integrity hash. Returns null (with a log entry) when the cache is missing,
        /// unreadable or corrupted, so the caller can fall back to downloading.
        /// </summary>
        private static byte[]? ReadCachedAssembly(EDependency dependency, string cachePath)
        {
            try
            {
                byte[] data;
                using (var fs = File.OpenRead(cachePath))
                    data = StreamToByteArray(fs);

                var expected = GetExpectedIntegrity(dependency);
                if (!string.IsNullOrEmpty(expected) && GetIntegrity(data) != expected)
                {
                    LogWarningSafe(
                        $"[DependencyUtil] Cached '{dependency}' failed the integrity check (corrupted or outdated); downloading it again.");
                    try
                    {
                        File.Delete(cachePath);
                    }
                    catch (Exception e)
                    {
                        LogWarningSafe($"[DependencyUtil] Failed to remove the corrupted cache file '{cachePath}': {e.Message}");
                    }

                    return null;
                }

                return data;
            }
            catch (Exception e)
            {
                LogWarningSafe($"[DependencyUtil] Failed to read cached '{dependency}' ({cachePath}): {e.Message}");
                return null;
            }
        }

        private static byte[] DownloadAssemblyChecked(WebClient client, EDependency dependency, bool mirror)
        {
            var data = DownloadAssembly(client, GetDependencyLink(dependency, mirror));
            var expected = GetExpectedIntegrity(dependency);
            if (!string.IsNullOrEmpty(expected) && GetIntegrity(data) != expected)
                throw new InvalidOperationException($"Downloaded '{dependency}' failed the integrity check.");
            return data;
        }

        private static async Task<byte[]> DownloadAssemblyCheckedAsync(WebClient client, EDependency dependency, bool mirror)
        {
            var data = await DownloadAssemblyAsync(client, GetDependencyLink(dependency, mirror));
            var expected = GetExpectedIntegrity(dependency);
            if (!string.IsNullOrEmpty(expected) && GetIntegrity(data) != expected)
                throw new InvalidOperationException($"Downloaded '{dependency}' failed the integrity check.");
            return data;
        }

        public static bool CanBeLoaded(EDependency dependency)
        {
            if(Rocket.Core.Plugins.RocketPlugin.IsDependencyLoaded(dependency.ToString()))return false;
            return AppDomain.CurrentDomain.GetAssemblies().All(x =>
                x.FullName != typeof(DependencyFullName).GetField(dependency.ToString()).GetValue(null).ToString());
        }

        public static string GetCachePath(EDependency dependency)
        {
            var rocketDir = Environment.CurrentDirectory;
            var librariesDir = Path.Combine(rocketDir, "Libraries");
            var cacheDir = Path.Combine(librariesDir, "Cache");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            return Path.Combine(cacheDir,
                typeof(DependencyIntegrity).GetField(dependency.ToString()).GetValue(null).ToString());
        }

        public static string GetIntegrity(string filePath)
        {
            using var md = MD5.Create();
            using var fileStream = File.OpenRead(filePath);
            var value = md.ComputeHash(fileStream);
            var result = BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
            fileStream.Close();

            return result;
        }

        public static string GetIntegrity(byte[] buffer)
        {
            using var md = MD5.Create();
            var value = md.ComputeHash(buffer);
            var result = BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
            return result;
        }

        public static bool IsCacheExists(EDependency dependency)
        {
            var cachePath = GetCachePath(dependency);
            return File.Exists(cachePath);
        }

        public static void Load(EDependency dependency, bool mirror = false)
        {
            if (!CanBeLoaded(dependency))
                return;

            using var wc = new WebClient();
            wc.Proxy = null;
            var cachePath = GetCachePath(dependency);
            // The library never caches itself, otherwise a cached copy would
            // pin an outdated version and defeat the self-update download.
            var useCache = dependency != EDependency.RFRocketLibrary;
            byte[]? assembly = null;

            if (useCache && IsCacheExists(dependency))
                assembly = ReadCachedAssembly(dependency, cachePath);

            if (assembly == null)
            {
                try
                {
                    assembly = DownloadAssemblyChecked(wc, dependency, mirror);
                }
                catch (Exception e)
                {
                    LogWarningSafe($"[DependencyUtil] Primary download for '{dependency}' failed: {e.Message}");
                }

                if (assembly == null)
                {
                    try
                    {
                        assembly = DownloadAssemblyChecked(wc, dependency, true);
                    }
                    catch (Exception e)
                    {
                        LogWarningSafe($"[DependencyUtil] Mirror download for '{dependency}' failed: {e.Message}");
                    }
                }

                if (assembly != null && useCache)
                {
                    try
                    {
                        File.WriteAllBytes(cachePath, assembly);
                    }
                    catch (Exception e)
                    {
                        LogWarningSafe($"[DependencyUtil] Failed to cache '{dependency}' ({cachePath}): {e.Message}");
                    }
                }
            }

            if (assembly == null)
            {
                LogErrorSafe(
                    $"[DependencyUtil] Unable to load dependency '{dependency}': every download failed and no valid cache exists.");
                throw new InvalidOperationException(
                    $"Dependency '{dependency}' could not be downloaded and no valid cache is available. See the log for details.");
            }

            Assembly.Load(assembly);
        }

        public static async Task LoadAsync(EDependency dependency, bool mirror = false)
        {
            if (!CanBeLoaded(dependency))
                return;

            using var wc = new WebClient();
            wc.Proxy = null;
            var cachePath = GetCachePath(dependency);
            // Same rule as Load: never read from or write to the cache for the
            // library itself, otherwise a cached copy would defeat self-updates.
            var useCache = dependency != EDependency.RFRocketLibrary;
            byte[]? assembly = null;

            if (useCache && IsCacheExists(dependency))
                assembly = ReadCachedAssembly(dependency, cachePath);

            if (assembly == null)
            {
                try
                {
                    assembly = await DownloadAssemblyCheckedAsync(wc, dependency, mirror);
                }
                catch (Exception e)
                {
                    LogWarningSafe($"[DependencyUtil] Primary download for '{dependency}' failed: {e.Message}");
                }

                if (assembly == null)
                {
                    try
                    {
                        assembly = await DownloadAssemblyCheckedAsync(wc, dependency, true);
                    }
                    catch (Exception e)
                    {
                        LogWarningSafe($"[DependencyUtil] Mirror download for '{dependency}' failed: {e.Message}");
                    }
                }

                if (assembly != null && useCache)
                {
                    try
                    {
                        File.WriteAllBytes(cachePath, assembly);
                    }
                    catch (Exception e)
                    {
                        LogWarningSafe($"[DependencyUtil] Failed to cache '{dependency}' ({cachePath}): {e.Message}");
                    }
                }
            }

            if (assembly == null)
            {
                LogErrorSafe(
                    $"[DependencyUtil] Unable to load dependency '{dependency}': every download failed and no valid cache exists.");
                throw new InvalidOperationException(
                    $"Dependency '{dependency}' could not be downloaded and no valid cache is available. See the log for details.");
            }

            Assembly.Load(assembly);
        }

        public static byte[] StreamToByteArray(Stream stream)
        {
            if (stream is MemoryStream memoryStream)
            {
                return memoryStream.ToArray();
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        #endregion
    }
}
