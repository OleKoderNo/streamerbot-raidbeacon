using System;
using System.Globalization;
using System.IO;

public class CPHInline
{
    // CONFIGURATION
    private const int ObsConnection = 0;
    private const string SceneName = "RaidBeacon";
    private const string BrowserSourceName = "RaidBeacon Fleet";

    // Replace this with your actual project path.
    private const string OverlayFile =
        @"C:\YOUR_PROJECTS\streamerbot-raidbeacon\tests\fleet-overlay.html";

    private const int ShipsPerViewer = 5;

    // Keep true while testing manually.
    private const bool UseTestData = true;
    private const int TestViewerCount = 1;

    // EXECUTION
    public bool Execute()
    {
        CPH.LogInfo("[RaidBeacon] Starting fleet overlay check.");

        try
        {
            int viewers;

            if (UseTestData)
            {
                viewers = TestViewerCount;
            }
            else if (!CPH.TryGetArg<int>("viewers", out viewers))
            {
                CPH.LogError(
                    "[RaidBeacon] Missing or invalid viewers argument."
                );

                return false;
            }

            if (viewers < 1 || ShipsPerViewer < 1)
            {
                CPH.LogError(
                    "[RaidBeacon] Viewer count and ShipsPerViewer " +
                    "must both be positive."
                );

                return false;
            }

            long totalShips = (long)viewers * ShipsPerViewer;

            // The renderer also counts two shots per ship.
            // Keep both counts within JavaScript's safe integer range.
            if (totalShips > 9007199254740991L / 2)
            {
                CPH.LogError(
                    "[RaidBeacon] Ship and shot counts are too large."
                );

                return false;
            }

            if (!Path.IsPathRooted(OverlayFile) ||
                !File.Exists(OverlayFile))
            {
                CPH.LogError(
                    "[RaidBeacon] OverlayFile must be an existing " +
                    "HTML file specified with a full path."
                );

                return false;
            }

            if (!CPH.ObsIsConnected(ObsConnection))
            {
                CPH.LogError("[RaidBeacon] OBS is not connected.");
                return false;
            }

            string overlayUrl = BuildOverlayUrl(viewers);

            CPH.ObsSetBrowserSource(
                SceneName,
                BrowserSourceName,
                overlayUrl,
                ObsConnection
            );

            CPH.ObsSetSourceVisibility(
                SceneName,
                BrowserSourceName,
                true,
                ObsConnection
            );

            CPH.LogInfo(
                "[RaidBeacon] Mode: " +
                (UseTestData ? "TEST" : "RAID ARGUMENTS")
            );

            CPH.LogInfo(
                "[RaidBeacon] Requested fleet: " +
                viewers.ToString(CultureInfo.InvariantCulture) +
                " viewers, " +
                totalShips.ToString(CultureInfo.InvariantCulture) +
                " ships, " +
                (totalShips * 2).ToString(CultureInfo.InvariantCulture) +
                " shots."
            );

            CPH.LogInfo(
                "[RaidBeacon] Playback requested. " +
                "Confirm the animation and cleanup in OBS."
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[RaidBeacon] Fleet check failed: " + exception.Message
            );

            return false;
        }
    }

    /// <summary>
    /// Builds a local-file URL containing the fleet settings.
    /// A unique run ID allows repeated tests with identical counts.
    /// </summary>
    private string BuildOverlayUrl(int viewers)
    {
        string fullPath = Path.GetFullPath(OverlayFile);
        string fileUrl = new Uri(fullPath).AbsoluteUri;

        return fileUrl +
            "?viewers=" +
            viewers.ToString(CultureInfo.InvariantCulture) +
            "&shipsPerViewer=" +
            ShipsPerViewer.ToString(CultureInfo.InvariantCulture) +
            "&run=" +
            Guid.NewGuid().ToString("N");
    }
}