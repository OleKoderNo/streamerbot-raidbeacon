using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

public class CPHInline
{
    // CONFIGURATION
    private const int ObsConnection = 0;
    private const string SceneName = "RaidBeacon";
    private const string BrowserSourceName = "RaidBeacon Fleet";

    // Keep the actual path from your working version.
    private const string OverlayFile =
        @"C:\YOUR_PROJECTS\streamerbot-raidbeacon\tests\fleet-overlay.html";

    private const int ShipsPerViewer = 5;

    // Supported placeholders:
    // {displayName}, {viewers}, {viewerLabel}
    private const string HeadingTemplate = "BOARDING PARTY!";

    private const string MessageTemplate =
        "{displayName} arrived with {viewers} {viewerLabel}!";

    private const string SingularViewerLabel = "raider";
    private const string PluralViewerLabel = "raiders";

    // DEVELOPMENT TEST DATA
    private const bool UseTestData = true;
    private const int TestViewerCount = 1;
    private const string TestDisplayName = "ExampleRaider";

    public bool Execute()
    {
        CPH.LogInfo("[RaidBeacon] Starting fleet and message check.");

        try
        {
            int viewers;
            string displayName;

            if (UseTestData)
            {
                viewers = TestViewerCount;
                displayName = TestDisplayName;
            }
            else
            {
                if (!CPH.TryGetArg<int>("viewers", out viewers))
                {
                    CPH.LogError(
                        "[RaidBeacon] Missing or invalid viewers argument."
                    );

                    return false;
                }

                // Prefer the display name; fall back to the login.
                if (!CPH.TryGetArg<string>("user", out displayName) ||
                    string.IsNullOrWhiteSpace(displayName))
                {
                    CPH.TryGetArg<string>("userName", out displayName);
                }
            }

            if (viewers < 1 || ShipsPerViewer < 1)
            {
                CPH.LogError(
                    "[RaidBeacon] Viewer count and ShipsPerViewer " +
                    "must both be positive."
                );

                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                CPH.LogError("[RaidBeacon] Missing raider name.");
                return false;
            }

            // Preserve international characters, removing control characters.
            displayName = Regex.Replace(
                displayName.Trim(),
                @"[\p{Cc}\p{Zl}\p{Zp}]",
                " "
            );

            long totalShips = (long)viewers * ShipsPerViewer;

            if (totalShips > 9007199254740991L / 2)
            {
                CPH.LogError("[RaidBeacon] Ship and shot counts are too large.");
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

            string heading = FormatText(
                HeadingTemplate,
                displayName,
                viewers
            );

            string message = FormatText(
                MessageTemplate,
                displayName,
                viewers
            );

            if (!CPH.ObsIsConnected(ObsConnection))
            {
                CPH.LogError("[RaidBeacon] OBS is not connected.");
                return false;
            }

            string overlayUrl = BuildOverlayUrl(
                viewers,
                heading,
                message
            );

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

            CPH.LogInfo("[RaidBeacon] Heading: " + heading);
            CPH.LogInfo("[RaidBeacon] Message: " + message);

            CPH.LogInfo(
                "[RaidBeacon] Ships requested: " +
                totalShips.ToString(CultureInfo.InvariantCulture)
            );

            CPH.LogInfo(
                "[RaidBeacon] Playback requested. " +
                "Confirm the fleet, text and cleanup in OBS."
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
    /// Expands supported placeholders without interpreting inserted values.
    /// </summary>
    private string FormatText(
        string template,
        string displayName,
        int viewers
    )
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ArgumentException("Text templates must not be blank.");
        }

        return Regex.Replace(template, @"\{([^{}]+)\}", match =>
        {
            switch (match.Groups[1].Value)
            {
                case "displayName":
                    return displayName;

                case "viewers":
                    return viewers.ToString(CultureInfo.InvariantCulture);

                case "viewerLabel":
                    return viewers == 1
                        ? SingularViewerLabel
                        : PluralViewerLabel;

                default:
                    throw new ArgumentException(
                        "Unknown placeholder: " + match.Value
                    );
            }
        });
    }

    /// <summary>
    /// Encodes the messages so punctuation cannot alter the URL parameters.
    /// </summary>
    private string BuildOverlayUrl(
        int viewers,
        string heading,
        string message
    )
    {
        string fileUrl = new Uri(
            Path.GetFullPath(OverlayFile)
        ).AbsoluteUri;

        return fileUrl +
            "?viewers=" +
            viewers.ToString(CultureInfo.InvariantCulture) +
            "&shipsPerViewer=" +
            ShipsPerViewer.ToString(CultureInfo.InvariantCulture) +
            "&heading=" +
            Uri.EscapeDataString(heading) +
            "&message=" +
            Uri.EscapeDataString(message) +
            "&run=" +
            Guid.NewGuid().ToString("N");
    }
}