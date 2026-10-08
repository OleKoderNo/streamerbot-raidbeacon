using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

public class CPHInline
{
    // TEST AND PREVIEW
    // TestMode uses sample data and ALWAYS prevents Twitch posting.
    // PreviewOnly prevents Twitch posting when using real event data.
    // Both modes still run the OBS overlay and audio.
    private const bool TestMode = true;
    private const bool PreviewOnly = true;

    private const string TestLogin = "exampleraider";
    private const string TestDisplayName = "ExampleRaider";
    private const int TestViewerCount = 1;

    // OBS
    private const int ObsConnection = 0;
    private const string SceneName = "RaidBeacon";
    private const string BrowserSourceName = "RaidBeacon Fleet";
    private const string OverlayFile =
        @"C:\YOUR_PROJECTS\streamerbot-raidbeacon\overlay\fleet-overlay.html";
    // FLEET AND TEXT
    // Templates support:
    // {username}, {displayName}, {viewers}, {viewerLabel}, {url}
    private const int ShipsPerViewer = 5;
    private const string HeadingTemplate = "BOARDING PARTY!";
    private const string MessageTemplate =
        "{displayName} arrived with {viewers} {viewerLabel}!";
    private const string SingularViewerLabel = "raider";
    private const string PluralViewerLabel = "raiders";

    // TWITCH
    private const bool EnableChatMessage = true;
    private const bool EnableShoutout = true;

    // false = broadcaster account; true = connected bot account.
    private const bool UseBotAccount = false;
    private const bool AllowBroadcasterFallback = true;

    private const string ChatTemplate =
        "Ahoy! {displayName} arrived with {viewers} {viewerLabel}! " +
        "Welcome aboard! Check them out at {url}";

    // BROWSER EFFECT AUDIO
    private const bool EnableEffectSounds = true;
    private const double CannonVolume = 0.12;
    private const double SplashVolume = 0.10;
    private const int MaxEffectSounds = 6;
    private const int EffectGapMs = 100;

    // STREAMER.BOT ARRIVAL AUDIO
    private const bool EnableArrivalSound = true;
    private const string ArrivalSoundFile =
        @"C:\YOUR_PROJECTS\streamerbot-raidbeacon\assets\audio\cannon_fire.ogg";
    private const float ArrivalSoundVolume = 0.25f;
    private const string ArrivalSoundName = "RaidBeacon Arrival";

    // COMPLETION TRACKING
    // Must match OverlaySignalCheck.cs.
    private const string GlobalPrefix = "RaidBeacon.Run.";
    private const int StartTimeoutSeconds = 20;
    private const int PlaybackTimeoutSeconds = 1800;
    private const int PollIntervalMs = 100;

    private sealed class RaidData
    {
        public string Login;
        public string DisplayName;
        public int Viewers;
    }

    /// <summary>
    /// Validates one raid, handles Twitch requests, and runs its fleet.
    /// The action must use the blocking RaidBeacon Alerts queue.
    /// </summary>
    public bool Execute()
    {
        try
        {
            RaidData raid = ReadRaid();
            ValidateSettings();

            // Prepare all messages before performing external actions.
            string heading = FormatText(HeadingTemplate, raid);
            string message = FormatText(MessageTemplate, raid);
            string chat = EnableChatMessage
                ? FormatText(ChatTemplate, raid)
                : "";

            Require(CPH.ObsIsConnected(ObsConnection),
                "OBS is not connected.");

            CPH.LogInfo(
                "[RaidBeacon] Mode: " +
                (TestMode ? "TEST" : PreviewOnly ? "PREVIEW" : "LIVE"));

            CPH.LogInfo(
                "[RaidBeacon] Raider: " + raid.Login +
                "; viewers: " + raid.Viewers.ToString(CultureInfo.InvariantCulture));

            HandleTwitch(raid.Login, chat);

            CPH.LogInfo("[RaidBeacon] Overlay: " + message);
            return RunFleet(raid.Viewers, heading, message);
        }
        catch (Exception error)
        {
            CPH.LogError("[RaidBeacon] Raid action failed: " + error.Message);
            return false;
        }
    }

    /// <summary>
    /// Reads sample data or incoming raid arguments.
    /// Live mode never substitutes sample data for missing arguments.
    /// </summary>
    private RaidData ReadRaid()
    {
        var raid = new RaidData
        {
            Login = TestLogin,
            DisplayName = TestDisplayName,
            Viewers = TestViewerCount
        };

        if (!TestMode)
        {
            Require(CPH.TryGetArg<int>("viewers", out raid.Viewers),
                "Missing or invalid viewers argument.");

            Require(CPH.TryGetArg<string>("userName", out raid.Login),
                "Missing raider userName argument.");

            if (!CPH.TryGetArg<string>("user", out raid.DisplayName) ||
                string.IsNullOrWhiteSpace(raid.DisplayName))
            {
                raid.DisplayName = raid.Login;
            }
        }

        raid.Login = (raid.Login ?? "").Trim().ToLowerInvariant();
        raid.DisplayName = CleanText(raid.DisplayName).Trim();

        Require(Regex.IsMatch(raid.Login, @"\A[a-z0-9_]+\z"),
            "Raider login must contain only letters, numbers or underscores.");
        Require(raid.DisplayName.Length > 0, "Missing raider display name.");
        Require(raid.Viewers > 0 && ShipsPerViewer > 0,
            "Viewer count and ShipsPerViewer must be positive.");
        Require((long)raid.Viewers * ShipsPerViewer <= 9007199254740991L / 2,
            "Ship and shot counts are too large.");

        return raid;
    }

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
    /// Attempts each enabled Twitch operation independently.
    /// Twitch errors do not prevent the visual alert.
    /// </summary>
    private void HandleTwitch(string login, string chat)
    {
        bool preview = TestMode || PreviewOnly;

        if (!EnableChatMessage)
        {
            CPH.LogInfo("[RaidBeacon] Chat message disabled.");
        }
        else if (preview)
        {
            CPH.LogInfo("[RaidBeacon] Chat preview: " + chat);
        }
        else
        {
            try
            {
                CPH.SendMessage(chat, UseBotAccount, AllowBroadcasterFallback);
                CPH.LogInfo(
                    "[RaidBeacon] Chat send requested. Confirm receipt in chat.");
            }
            catch (Exception error)
            {
                CPH.LogError("[RaidBeacon] Chat request failed: " + error.Message);
            }
        }

        if (!EnableShoutout)
        {
            CPH.LogInfo("[RaidBeacon] Native shoutout disabled.");
        }
        else if (preview)
        {
            CPH.LogInfo("[RaidBeacon] Native shoutout preview: " + login);
        }
        else
        {
            try
            {
                bool accepted = CPH.TwitchSendShoutoutByLogin(login);

                CPH.LogInfo(accepted
                    ? "[RaidBeacon] Native shoutout reported success."
                    : "[RaidBeacon] Native shoutout returned false. " +
                      "Check stream status, permissions and cooldowns. No retry.");
            }
            catch (Exception error)
            {
                CPH.LogError(
                    "[RaidBeacon] Shoutout request failed: " + error.Message);
            }
        }

        if (preview)
            CPH.LogInfo("[RaidBeacon] No Twitch messages or shoutouts were sent.");
    }

    /// <summary>
    /// Starts one overlay run and holds the queue until it finishes.
    /// Each run uses its own ID and temporary state.
    /// </summary>
    private bool RunFleet(int viewers, string heading, string message)
    {
        string runId = Guid.NewGuid().ToString("N");
        string key = GlobalPrefix + runId + ".";

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
                started
                    ? "Overlay completion timed out."
                    : "Overlay startup timed out.");

            CPH.Wait(PollIntervalMs);
        }
    }

    /// <summary>
    /// Arrival audio is optional; failures do not stop the fleet.
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
    /// Expands supported placeholders once, preserving inserted text.
    /// </summary>
    private string FormatText(string template, RaidData raid)
    {
        Require(!string.IsNullOrWhiteSpace(template),
            "Text templates must not be blank.");

        string text = Regex.Replace(template, @"\{([^{}]+)\}", match =>
        {
            switch (match.Groups[1].Value)
            {
                case "username":
                    return raid.Login;
                case "displayName":
                    return raid.DisplayName;
                case "viewers":
                    return raid.Viewers.ToString(CultureInfo.InvariantCulture);
                case "viewerLabel":
                    return raid.Viewers == 1
                        ? SingularViewerLabel
                        : PluralViewerLabel;
                case "url":
                    return "https://www.twitch.tv/" + raid.Login;
                default:
                    throw new ArgumentException(
                        "Unknown placeholder: " + match.Value);
            }
        });

        text = CleanText(text).Trim();
        Require(text.Length > 0, "Formatted text must not be blank.");
        return text;
    }

    private string CleanText(string value)
    {
        return Regex.Replace(value ?? "", @"[\p{Cc}\p{Zl}\p{Zp}]", " ");
    }

    private bool IsValidVolume(double value)
    {
        return !double.IsNaN(value) &&
               !double.IsInfinity(value) &&
               value >= 0 && value <= 1;
    }

    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}