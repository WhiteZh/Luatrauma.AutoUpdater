using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
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

        public async static Task Update()
        {
            Logger.Log("Starting update...");

            Logger.Log("Loading local version info...");
            var localVersionInfo = await LocalVersionInfo.RetrieveLocalVersionInfoAsync();
            Logger.Log(localVersionInfo != null
                ? $"Local version info loaded:\n{localVersionInfo}"
                : "Local version info is NOT loaded");

            Logger.Log("Loading remote version info...");
            RemoteVersionInfo? remoteVersionInfo = null;
            try
            {
                remoteVersionInfo = await RemoteVersionInfo.FetchRemoteVersionInfoAsync();
                Logger.Log(remoteVersionInfo != null
                    ? $"Remote version info loaded:\n{remoteVersionInfo}"
                    : "Remote version info is NOT loaded");
            }
            catch (Exception e)
            {
                Logger.Log($"Error encountered while fetching remote version info: {e.Message}");
            }

            if (localVersionInfo != null && remoteVersionInfo != null &&
                localVersionInfo.UpdatedAt == remoteVersionInfo.UpdatedAt)
            {
                Logger.Log("Both version info contain the same updated_at attribute, skipping patch fetching");
            }
            else
            {
                string patchUrl = null;
                if (OperatingSystem.IsWindows())
                {
                    patchUrl =
                        "https://github.com/evilfactory/LuaCsForBarotrauma/releases/download/latest/luacsforbarotrauma_patch_windows_client.zip";
                }
                else if (OperatingSystem.IsLinux())
                {
                    patchUrl =
                        "https://github.com/evilfactory/LuaCsForBarotrauma/releases/download/latest/luacsforbarotrauma_patch_linux_client.zip";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    patchUrl =
                        "https://github.com/evilfactory/LuaCsForBarotrauma/releases/download/latest/luacsforbarotrauma_patch_mac_client.zip";
                }

                if (patchUrl == null)
                {
                    Logger.Log("Unsupported operating system.");
                    return;
                }

                string tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Luatrauma.AutoUpdater.Temp");
                string patchZip = Path.Combine(tempFolder, "patch.zip");
                string extractionFolder = Path.Combine(tempFolder, "Extracted");

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

                try
                {
                    if (Directory.Exists(extractionFolder))
                    {
                        Directory.Delete(extractionFolder, true);
                    }

                    Directory.CreateDirectory(extractionFolder);

                    ZipFile.ExtractToDirectory(patchZip, extractionFolder, true);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to extract patch zip: {e.Message}");
                    return;
                }

                Logger.Log($"Extracted patch zip to {Directory.GetCurrentDirectory()}");

                Logger.Log($"Applying patch...");

                // Verify that the dll version is the same as the current one
                string currentDll = Path.Combine(Directory.GetCurrentDirectory(), "Barotrauma.dll");
                string newDll = Path.Combine(extractionFolder, "Barotrauma.dll");

                if (!File.Exists(currentDll))
                {
                    Logger.Log("Failed to find the current Barotrauma.dll", ConsoleColor.Red);
                    return;
                }

                if (!File.Exists(newDll))
                {
                    Logger.Log("Failed to find the new Barotrauma.dll", ConsoleColor.Red);
                    return;
                }

                // Grab the version of the current dll
                var currentVersion = FileVersionInfo.GetVersionInfo(currentDll);
                var newVersion = FileVersionInfo.GetVersionInfo(newDll);

                if (currentVersion == null || newVersion == null)
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
                    Logger.Log(
                        $"The patch is not compatible with the current game version {currentVersion.FileVersion} -> {newVersion.FileVersion}, aborting.");

                    Logger.Log(
                        "Theres no patch available for the current game version, the game will be launched without the patch, if there was a new game update please wait until a new patch is released.",
                        ConsoleColor.Yellow);

                    await Task.Delay(8000);

                    return;
                }

                CopyFilesRecursively(extractionFolder, Directory.GetCurrentDirectory());

                Logger.Log("Patch applied.");

                if (File.Exists("luacsversion.txt")) // Workshop stuff, get rid of it so it doesn't interfere
                {
                    File.Delete("luacsversion.txt");
                }

                // update local version info
                if (remoteVersionInfo == null)
                {
                    Logger.Log("Remote version info is null, deleting local version info...");

                    try
                    {
                        File.Delete(LocalVersionInfo.DefaultFileName);
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Failed to delete local version info: {e.Message}");
                    }
                }
                else
                {
                    Logger.Log("Updating local version info...");

                    var newLocalVersionInfo = new LocalVersionInfo(
                        UpdatedAt: remoteVersionInfo.UpdatedAt
                    );

                    try
                    {
                        await newLocalVersionInfo.WriteToFileAsync();

                        Logger.Log("Updated local version info successfully written");
                    }
                    catch (Exception e)
                    {
                        Logger.Log(
                            $"Error encountered while writing updated local version info to file: {e.Message}",
                            ConsoleColor.Red
                        );
                    }
                }
            }

            Logger.Log("Update completed.");
        }
    }
}