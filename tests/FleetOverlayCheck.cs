using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

public class CPHInline
{
    // CONFIGURATION
    private const int ObsConnection = 0;
    private const string SceneName = "RaidBeacon";
    private const string BrowserSourceName = "RaidBeacon Fleet";
    private const string OverlayFile =
        @"C:\YOUR_PROJECTS\streamerbot-raidbeacon\tests\fleet-overlay.html";

    private const int ShipsPerViewer = 5;
    private const string HeadingTemplate = "BOARDING PARTY!";
    private const string MessageTemplate =
        "{displayName} arrived with {viewers} {viewerLabel}!";
    private const string SingularViewerLabel = "raider";
    private const string PluralViewerLabel = "raiders";

    // ARRIVAL SOUND
    private const bool EnableArrivalSound = true;
    private const string ArrivalSoundFile =
        @"C:\YOUR_PROJECTS\streamerbot-raidbeacon\assets\audio\cannon_fire.ogg";
    private const float ArrivalSoundVolume = 0.25f;
    private const string ArrivalSoundName = "RaidBeacon Arrival";

    // COMPLETION TRACKING — prefix must match OverlaySignalCheck.cs.
    private const string GlobalPrefix = "RaidBeacon.Run.";
    private const int StartTimeoutSeconds = 20;
    private const int PlaybackTimeoutSeconds = 1800;
    private const int PollIntervalMs = 100;

    // TEST DATA
    private const bool UseTestData = true;
    private const int TestViewerCount = 1;
    private const string TestDisplayName = "ExampleRaider";

    public bool Execute()
    {
        try
        {
            int viewers = TestViewerCount;
            string name = TestDisplayName;

            if (!UseTestData)
            {
                Require(CPH.TryGetArg<int>("viewers", out viewers),
                    "Missing or invalid viewers argument.");

                if (!CPH.TryGetArg<string>("user", out name) ||
                    string.IsNullOrWhiteSpace(name))
                    CPH.TryGetArg<string>("userName", out name);
            }

            name = Regex.Replace(
                name ?? "", @"[\p{Cc}\p{Zl}\p{Zp}]", " ").Trim();

            Require(name.Length > 0, "Missing raider name.");
            Require(viewers > 0 && ShipsPerViewer > 0,
                "Viewer count and ShipsPerViewer must be positive.");
            Require((long)viewers * ShipsPerViewer <= 9007199254740991L / 2,
                "Ship and shot counts are too large.");
            Require(Path.IsPathRooted(OverlayFile) && File.Exists(OverlayFile),
                "OverlayFile must be an existing file with a full path.");
            Require(StartTimeoutSeconds > 0 && PlaybackTimeoutSeconds > 0 &&
                    PollIntervalMs > 0 && PollIntervalMs <= 1000,
                "Invalid timeout or polling settings.");
            Require(CPH.ObsIsConnected(ObsConnection), "OBS is not connected.");

            string heading = FormatText(HeadingTemplate, name, viewers);
            string message = FormatText(MessageTemplate, name, viewers);

            CPH.LogInfo("[RaidBeacon] " + message);
            return RunFleet(viewers, heading, message);
        }
        catch (Exception error)
        {
            CPH.LogError("[RaidBeacon] Fleet check failed: " + error.Message);
            return false;
        }
    }

    private bool RunFleet(int viewers, string heading, string message)
    {
        string runId = Guid.NewGuid().ToString("N");
        string key = GlobalPrefix + runId + ".";
        string url = new Uri(Path.GetFullPath(OverlayFile)).AbsoluteUri +
            "?viewers=" + viewers.ToString(CultureInfo.InvariantCulture) +
            "&shipsPerViewer=" + ShipsPerViewer.ToString(CultureInfo.InvariantCulture) +
            "&heading=" + Uri.EscapeDataString(heading) +
            "&message=" + Uri.EscapeDataString(message) +
            "&run=" + runId;

        try
        {
            CPH.SetGlobalVar(key + "started", false, false);
            CPH.SetGlobalVar(key + "result", "", false);
            CPH.SetGlobalVar(key + "active", true, false);

            CPH.ObsSetBrowserSource(SceneName, BrowserSourceName, url, ObsConnection);
            CPH.ObsSetSourceVisibility(SceneName, BrowserSourceName, true, ObsConnection);
            CPH.LogInfo("[RaidBeacon] Waiting for overlay start: " + runId);

            return WaitForFleet(key, runId);
        }
        finally
        {
            CPH.SetGlobalVar(key + "active", false, false);

            try
            {
                CPH.ObsSetBrowserSource(
                    SceneName, BrowserSourceName, "about:blank", ObsConnection);
            }
            catch (Exception error)
            {
                CPH.LogError("[RaidBeacon] Browser cleanup failed: " + error.Message);
            }

            foreach (string suffix in new[] { "started", "result", "active" })
                CPH.UnsetGlobalVar(key + suffix, false);
        }
    }

    private bool WaitForFleet(string key, string runId)
    {
        var timer = Stopwatch.StartNew();
        bool started = false;

        while (true)
        {
            Require(CPH.ObsIsConnected(ObsConnection),
                "OBS disconnected during playback.");

            string result = CPH.GetGlobalVar<string>(key + "result", false);
            Require(result != "failed", "The overlay reported a failure.");

            if (!started && CPH.GetGlobalVar<bool>(key + "started", false))
            {
                started = true;
                timer.Restart();
                CPH.LogInfo("[RaidBeacon] Overlay started: " + runId);
                PlayArrivalSound();
            }

            if (result == "complete")
            {
                Require(started, "Completion arrived without a start report.");
                CPH.LogInfo("[RaidBeacon] Overlay completed: " + runId);
                return true;
            }

            int limit = started ? PlaybackTimeoutSeconds : StartTimeoutSeconds;
            Require(timer.Elapsed.TotalSeconds < limit,
                started ? "Overlay completion timed out." : "Overlay startup timed out.");

            CPH.Wait(PollIntervalMs);
        }
    }

    private void PlayArrivalSound()
    {
        if (!EnableArrivalSound) return;

        try
        {
            Require(Path.IsPathRooted(ArrivalSoundFile) && File.Exists(ArrivalSoundFile),
                "ArrivalSoundFile must be an existing file with a full path.");
            Require(!float.IsNaN(ArrivalSoundVolume) &&
                    ArrivalSoundVolume >= 0f && ArrivalSoundVolume <= 1f,
                "ArrivalSoundVolume must be between 0.0 and 1.0.");
            Require(!string.IsNullOrWhiteSpace(ArrivalSoundName),
                "ArrivalSoundName must not be blank.");

            CPH.PlaySound(
                ArrivalSoundFile, ArrivalSoundVolume, false, ArrivalSoundName, false);
            CPH.LogInfo("[RaidBeacon] Arrival sound playback requested.");
        }
        catch (Exception error)
        {
            CPH.LogError("[RaidBeacon] Sound failed; fleet continues: " + error.Message);
        }
    }

    private string FormatText(string template, string name, int viewers)
    {
        Require(!string.IsNullOrWhiteSpace(template), "Text templates must not be blank.");

        return Regex.Replace(template, @"\{([^{}]+)\}", match =>
        {
            switch (match.Groups[1].Value)
            {
                case "displayName": return name;
                case "viewers": return viewers.ToString(CultureInfo.InvariantCulture);
                case "viewerLabel":
                    return viewers == 1 ? SingularViewerLabel : PluralViewerLabel;
                default:
                    throw new ArgumentException("Unknown placeholder: " + match.Value);
            }
        });
    }

    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}