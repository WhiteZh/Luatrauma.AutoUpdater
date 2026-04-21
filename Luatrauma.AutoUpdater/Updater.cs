using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Luatrauma.AutoUpdater
{
    static class Updater
    {
        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        private static async Task<string?> GetRemoteETag(string url)
        {
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request);

            return response.Headers.ETag?.Tag;
        }

        public async static Task Update(bool nightly = false, bool serverOnly = false, bool forceWindows = false)
        {
            Logger.Log("Starting update...");

            string patchUrl = "https://github.com/evilfactory/LuaCsForBarotrauma/releases/download/latest/";
            if (nightly)
            {
                patchUrl = "https://github.com/evilfactory/LuaCsForBarotrauma/releases/download/nightly/";
            }
            if (OperatingSystem.IsWindows() || forceWindows)
            {
                if (serverOnly) { patchUrl += "luacsforbarotrauma_patch_windows_server.zip"; }
                else { patchUrl += "luacsforbarotrauma_patch_windows_client.zip"; }
            }
            else if (OperatingSystem.IsLinux())
            {
                if (serverOnly) { patchUrl += "luacsforbarotrauma_patch_linux_server.zip"; }
                else { patchUrl += "luacsforbarotrauma_patch_linux_client.zip"; }
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (serverOnly) { patchUrl += "luacsforbarotrauma_patch_mac_server.zip"; }
                else { patchUrl += "luacsforbarotrauma_patch_mac_client.zip"; }
            }
            else
            {
                Logger.Log("Unsupported operating system.");
                return;
            }
            
            Logger.Log($"{nameof(patchUrl)} = {patchUrl}");

            string tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Luatrauma.AutoUpdater.Temp");
            string patchZip = Path.Combine(tempFolder, "patch.zip");
            string extractionFolder = Path.Combine(tempFolder, "Extracted");

            string etagFile = Path.Combine(tempFolder, "patch.etag");

            string? remoteEtag = await GetRemoteETag(patchUrl);
            string? localEtag = File.Exists(etagFile) ? await File.ReadAllTextAsync(etagFile) : null;
            
            Logger.Log($"{nameof(remoteEtag)} = {remoteEtag}");
            Logger.Log($"{nameof(localEtag)}  = {localEtag}");

            bool skippedDownload = false;
            if (remoteEtag is not null && remoteEtag == localEtag)
            {
                skippedDownload = true;
                Logger.Log("Patch has not changed. Skipping download.");
            }
            else
            {
                Logger.Log($"Downloading patch zip from {patchUrl}");

                try
                {
                    using var client = new HttpClient();

                    byte[] fileBytes = await client.GetByteArrayAsync(patchUrl);

                    await File.WriteAllBytesAsync(patchZip, fileBytes);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to download patch zip: {e.Message}");
                    return;
                }

                Logger.Log($"Downloaded patch zip to {patchZip}");
                
                if (remoteEtag is not null)
                {
                    await File.WriteAllTextAsync(etagFile, remoteEtag);
                }
            }

            string dllFile = "Barotrauma.dll";
            if (serverOnly)
            {
                dllFile = "DedicatedServer.dll";
            }
            
            Logger.Log($"Applying patch...");

            string lastExtractedPatchZipMd5HashTxt = Path.Combine(tempFolder, "lastExtractedPatchZipMd5Hash.txt");
            string patchZipMd5Hash;
            await using (var patchZipFileStream = File.OpenRead(patchZip))
            {
                patchZipMd5Hash = Convert.ToHexString(await MD5.Create().ComputeHashAsync(patchZipFileStream)).ToLowerInvariant();
            }
            try
            {
                if (!skippedDownload)
                {
                    throw new Exception();
                }
                if (!Directory.Exists(extractionFolder))
                {
                    throw new Exception();
                }

                string? lastExtractedPatchZipMd5Hash = File.Exists(lastExtractedPatchZipMd5HashTxt) ? await File.ReadAllTextAsync(lastExtractedPatchZipMd5HashTxt) : null;
                
                Logger.Log($"{nameof(lastExtractedPatchZipMd5Hash)} = {lastExtractedPatchZipMd5Hash}");
                Logger.Log($"{nameof(patchZipMd5Hash)}              = {patchZipMd5Hash}");

                if (lastExtractedPatchZipMd5Hash is null || lastExtractedPatchZipMd5Hash != patchZipMd5Hash)
                {
                    throw new Exception();
                }

                Logger.Log("Files inside the patch zip is already extracted to the extraction folder. New extraction skipped.");
            }
            catch (Exception)
            {
                Logger.Log("Existing files inside the extraction folder are outdated or simply non-existing. Performing new extraction...");
                
                if (Directory.Exists(extractionFolder))
                {
                    Directory.Delete(extractionFolder, true);
                }

                Directory.CreateDirectory(extractionFolder);

                try
                {
                    ZipFile.ExtractToDirectory(patchZip, extractionFolder, true);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to extract patch zip: {e.Message}");
                    return;
                }

                await File.WriteAllTextAsync(lastExtractedPatchZipMd5HashTxt, patchZipMd5Hash);

                Logger.Log($"Extracted patch zip to {extractionFolder}");
            }

            // Verify that the dll version is the same as the current one
            string currentDll = Path.Combine(Directory.GetCurrentDirectory(), dllFile);
            string newDll = Path.Combine(extractionFolder, dllFile);

            if (!File.Exists(currentDll))
            {
                Logger.Log($"Failed to find the current {dllFile}", ConsoleColor.Red);
                return;
            }

            if (!File.Exists(newDll))
            {
                Logger.Log($"Failed to find the new {dllFile}", ConsoleColor.Red);
                return;
            }

            // Grab the version of the current dll
            var currentVersion = FileVersionInfo.GetVersionInfo(currentDll);
            var newVersion = FileVersionInfo.GetVersionInfo(newDll);
            
            Logger.Log($"current ddl version: {currentVersion.FileVersion}");
            Logger.Log($"new ddl version:     {newVersion.FileVersion}");

            if (currentVersion.FileVersion is null || newVersion.FileVersion is null)
            {
                Logger.Log("Failed to get version info for the dlls");
                return;
            }

            if (currentVersion.FileVersion == newVersion.FileVersion)
            {
                Logger.Log($"The patch is compatible with the current game version {newVersion.FileVersion}.");
            }
            else
            {
                Logger.Log($"The patch is not compatible with the current game version {currentVersion.FileVersion} -> {newVersion.FileVersion}, aborting.");

                Logger.Log("Theres no patch available for the current game version, the game will be launched without the patch, if there was a new game update please wait until a new patch is released.", ConsoleColor.Yellow);

                await Task.Delay(8000);

                return;
            }

            string lastModdedVersionFilePath = Path.Combine(tempFolder, "lastModdedVersion.txt");
            string? lastModdedVersion = File.Exists(lastModdedVersionFilePath) ? await File.ReadAllTextAsync(lastModdedVersionFilePath) : null;
            
            if (lastModdedVersion is not null && lastModdedVersion == currentVersion.FileVersion)
            {
                Logger.Log("Game is already modded with the latest patch. Patch skipped.");
            }
            else
            {
                CopyFilesRecursively(extractionFolder, Directory.GetCurrentDirectory());

                Logger.Log("Patch applied.");

                await File.WriteAllTextAsync(lastModdedVersionFilePath, currentVersion.FileVersion);
            }

            if (File.Exists("luacsversion.txt")) // Workshop stuff, get rid of it so it doesn't interfere
            {
                File.Delete("luacsversion.txt");
            }

            Logger.Log("Update completed.");
        }
    }
}
