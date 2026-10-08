using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

public class CPHInline
{
    // OBS CONFIGURATION
    // Use your existing OBS connection, scene, and browser source names.
    private const int ObsConnection = 0;
    private const string SceneName = "RaidBeacon";
    private const string BrowserSourceName = "RaidBeacon Fleet";

    // Replace this with the full path to your overlay HTML file.
    private const string OverlayFile =
        @"C:\YOUR_PROJECTS\streamerbot-raidbeacon\tests\fleet-overlay.html";

    // FLEET AND TEXT
    // Supported placeholders: {displayName}, {viewers}, {viewerLabel}.
    private const int ShipsPerViewer = 5;
    private const string HeadingTemplate = "BOARDING PARTY!";
    private const string MessageTemplate =
        "{displayName} arrived with {viewers} {viewerLabel}!";
    private const string SingularViewerLabel = "raider";
    private const string PluralViewerLabel = "raiders";

    // ANIMATION AUDIO
    // Played by the browser overlay at cannon and splash events.
    // Volumes range from 0.0 (silent) to 1.0 (full volume).
    // Sounds exceeding the concurrency or spacing limits are skipped.
    private const bool EnableEffectSounds = true;
    private const double CannonVolume = 0.12;
    private const double SplashVolume = 0.10;
    private const int MaxEffectSounds = 6;
    private const int EffectGapMs = 100;

    // ARRIVAL AUDIO
    // Played once through Streamer.bot after the overlay reports startup.
    // This sound is separate from the browser's effect-sound limit.
    private const bool EnableArrivalSound = true;
    private const string ArrivalSoundFile =
        @"C:\YOUR_PROJECTS\streamerbot-raidbeacon\assets\audio\cannon_fire.ogg";
    private const float ArrivalSoundVolume = 0.25f;
    private const string ArrivalSoundName = "RaidBeacon Arrival";

    // COMPLETION TRACKING
    // GlobalPrefix must match the value in OverlaySignalCheck.cs.
    // PlaybackTimeoutSeconds is an emergency limit, not a fixed duration.
    private const string GlobalPrefix = "RaidBeacon.Run.";
    private const int StartTimeoutSeconds = 20;
    private const int PlaybackTimeoutSeconds = 1800;
    private const int PollIntervalMs = 100;

    // DEVELOPMENT TEST DATA
    // Keep enabled until the incoming raid integration is ready to test.
    private const bool UseTestData = true;
    private const int TestViewerCount = 1;
    private const string TestDisplayName = "ExampleRaider";

    /// <summary>
    /// Validates raid data and settings, then runs one complete fleet alert.
    /// Assign this action to the blocking RaidBeacon Alerts queue.
    /// </summary>
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

                // Prefer the display name, falling back to the login.
                if (!CPH.TryGetArg<string>("user", out name) ||
                    string.IsNullOrWhiteSpace(name))
                {
                    CPH.TryGetArg<string>("userName", out name);
                }
            }

            // Preserve international characters while removing controls.
            name = Regex.Replace(
                name ?? "", @"[\p{Cc}\p{Zl}\p{Zp}]", " ").Trim();

            Require(name.Length > 0, "Missing raider name.");
            Require(viewers > 0 && ShipsPerViewer > 0,
                "Viewer count and ShipsPerViewer must be positive.");

            // JavaScript must represent both ship and shot counts exactly.
            Require((long)viewers * ShipsPerViewer <= 9007199254740991L / 2,
                "Ship and shot counts are too large.");

            ValidateSettings();

            string heading = FormatText(HeadingTemplate, name, viewers);
            string message = FormatText(MessageTemplate, name, viewers);

            Require(CPH.ObsIsConnected(ObsConnection),
                "OBS is not connected.");

            CPH.LogInfo("[RaidBeacon] " + message);
            return RunFleet(viewers, heading, message);
        }
        catch (Exception error)
        {
            CPH.LogError("[RaidBeacon] Fleet check failed: " + error.Message);
            return false;
        }
    }

    /// <summary>
    /// Rejects invalid configuration before requesting OBS playback.
    /// Optional arrival audio is validated separately when it plays.
    /// </summary>
    private void ValidateSettings()
    {
        Require(Path.IsPathRooted(OverlayFile) && File.Exists(OverlayFile),
            "OverlayFile must be an existing file with a full path.");

        Require(StartTimeoutSeconds > 0 && PlaybackTimeoutSeconds > 0 &&
                PollIntervalMs > 0 && PollIntervalMs <= 1000,
            "Timeouts must be positive. PollIntervalMs must be 1–1000.");

        Require(IsValidVolume(CannonVolume) && IsValidVolume(SplashVolume),
            "Effect volumes must be between 0.0 and 1.0.");

        Require(MaxEffectSounds >= 1 && MaxEffectSounds <= 16,
            "MaxEffectSounds must be between 1 and 16.");

        Require(EffectGapMs >= 0 && EffectGapMs <= 5000,
            "EffectGapMs must be between 0 and 5000.");
    }

    /// <summary>
    /// Creates isolated state for this run, starts the overlay, and waits.
    /// Cleanup runs whether playback completes, fails, or times out.
    /// </summary>
    private bool RunFleet(int viewers, string heading, string message)
    {
        string runId = Guid.NewGuid().ToString("N");
        string key = GlobalPrefix + runId + ".";

        // Encode user-facing text and use culture-independent numbers.
        string url = new Uri(Path.GetFullPath(OverlayFile)).AbsoluteUri +
            "?viewers=" + viewers.ToString(CultureInfo.InvariantCulture) +
            "&shipsPerViewer=" + ShipsPerViewer.ToString(CultureInfo.InvariantCulture) +
            "&heading=" + Uri.EscapeDataString(heading) +
            "&message=" + Uri.EscapeDataString(message) +
            "&run=" + runId +
            "&effects=" + (EnableEffectSounds ? "1" : "0") +
            "&cannonVolume=" + CannonVolume.ToString(CultureInfo.InvariantCulture) +
            "&splashVolume=" + SplashVolume.ToString(CultureInfo.InvariantCulture) +
            "&maxSounds=" + MaxEffectSounds.ToString(CultureInfo.InvariantCulture) +
            "&effectGapMs=" + EffectGapMs.ToString(CultureInfo.InvariantCulture);

        try
        {
            // Non-persisted globals are shared with the signal receiver.
            // Activate last so the receiver sees initialized state.
            CPH.SetGlobalVar(key + "started", false, false);
            CPH.SetGlobalVar(key + "result", "", false);
            CPH.SetGlobalVar(key + "active", true, false);

            CPH.ObsSetBrowserSource(
                SceneName, BrowserSourceName, url, ObsConnection);
            CPH.ObsSetSourceVisibility(
                SceneName, BrowserSourceName, true, ObsConnection);

            CPH.LogInfo("[RaidBeacon] Waiting for overlay start: " + runId);
            return WaitForFleet(key, runId);
        }
        finally
        {
            // Stop accepting signals before unloading this run.
            CPH.SetGlobalVar(key + "active", false, false);

            try
            {
                CPH.ObsSetBrowserSource(
                    SceneName, BrowserSourceName, "about:blank", ObsConnection);
            }
            catch (Exception error)
            {
                CPH.LogError(
                    "[RaidBeacon] Browser cleanup failed: " + error.Message);
            }

            foreach (string suffix in new[] { "started", "result", "active" })
            {
                CPH.UnsetGlobalVar(key + suffix, false);
            }
        }
    }

    /// <summary>
    /// Holds the alert queue until the overlay reports completion.
    /// The signal receiver must run on a separate queue.
    /// </summary>
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

                // Switch from the startup timeout to the playback timeout.
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

            int limit = started
                ? PlaybackTimeoutSeconds
                : StartTimeoutSeconds;

            Require(timer.Elapsed.TotalSeconds < limit,
                started
                    ? "Overlay completion timed out."
                    : "Overlay startup timed out.");

            CPH.Wait(PollIntervalMs);
        }
    }

    /// <summary>
    /// Requests one arrival sound without waiting for it to finish.
    /// Audio errors are logged without interrupting the fleet.
    /// </summary>
    private void PlayArrivalSound()
    {
        if (!EnableArrivalSound) return;

        try
        {
            Require(Path.IsPathRooted(ArrivalSoundFile) &&
                    File.Exists(ArrivalSoundFile),
                "ArrivalSoundFile must be an existing file with a full path.");

            Require(IsValidVolume(ArrivalSoundVolume),
                "ArrivalSoundVolume must be between 0.0 and 1.0.");

            Require(!string.IsNullOrWhiteSpace(ArrivalSoundName),
                "ArrivalSoundName must not be blank.");

            CPH.PlaySound(
                ArrivalSoundFile, ArrivalSoundVolume, false, ArrivalSoundName, false);

            CPH.LogInfo("[RaidBeacon] Arrival sound playback requested.");
        }
        catch (Exception error)
        {
            CPH.LogError(
                "[RaidBeacon] Sound failed; fleet continues: " + error.Message);
        }
    }

    /// <summary>
    /// Expands known placeholders in one pass without reprocessing names.
    /// </summary>
    private string FormatText(string template, string name, int viewers)
    {
        Require(!string.IsNullOrWhiteSpace(template),
            "Text templates must not be blank.");

        return Regex.Replace(template, @"\{([^{}]+)\}", match =>
        {
            switch (match.Groups[1].Value)
            {
                case "displayName":
                    return name;

                case "viewers":
                    return viewers.ToString(CultureInfo.InvariantCulture);

                case "viewerLabel":
                    return viewers == 1
                        ? SingularViewerLabel
                        : PluralViewerLabel;

                default:
                    throw new ArgumentException(
                        "Unknown placeholder: " + match.Value);
            }
        });
    }

    /// <summary>
    /// Accepts finite volume values from zero through one.
    /// </summary>
    private bool IsValidVolume(double value)
    {
        return !double.IsNaN(value) &&
               !double.IsInfinity(value) &&
               value >= 0 &&
               value <= 1;
    }

    /// <summary>
    /// Stops the current operation with a descriptive validation error.
    /// </summary>
    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}