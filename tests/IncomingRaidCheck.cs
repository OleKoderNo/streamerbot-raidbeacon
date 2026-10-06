using System;
using System.Globalization;
using System.Text.RegularExpressions;

public class CPHInline
{
    // CONFIGURATION
    private const string OverlayTemplate =
        "{displayName} raided with {viewers} {viewerLabel}!";

    private const string ChatTemplate =
        "Welcome {displayName} and your {viewers} {viewerLabel}! " +
        "Check them out at {url}";

    private const string SingularViewerLabel = "viewer";
    private const string PluralViewerLabel = "viewers";

    // EXECUTION
    public bool Execute()
    {
        CPH.LogInfo("[RaidBeacon] Starting incoming raid check.");

        string username;
        string displayName;
        int viewerCount;

        // Required: the raider's Twitch login.
        if (!CPH.TryGetArg<string>("userName", out username) ||
            string.IsNullOrWhiteSpace(username))
        {
            CPH.LogError(
                "[RaidBeacon] Missing or empty userName argument."
            );

            return false;
        }

        // Required: the number of viewers arriving with the raid.
        if (!CPH.TryGetArg<int>("viewers", out viewerCount) ||
            viewerCount < 1)
        {
            CPH.LogError(
                "[RaidBeacon] Missing or invalid viewers argument. " +
                "Expected a positive whole number."
            );

            return false;
        }

        username = username.Trim().ToLowerInvariant();

        // Check the login before using it in a channel URL.
        if (!Regex.IsMatch(username, @"\A[a-z0-9_]+\z"))
        {
            CPH.LogError(
                "[RaidBeacon] userName contains unexpected characters."
            );

            return false;
        }

        // Optional: use the login if the display name is unavailable.
        if (!CPH.TryGetArg<string>("user", out displayName) ||
            string.IsNullOrWhiteSpace(displayName))
        {
            displayName = username;
        }

        // Preserve international characters while keeping logs on one line.
        displayName = Regex.Replace(
            displayName.Trim(),
            @"[\p{Cc}\p{Zl}\p{Zp}]",
            " "
        );

        try
        {
            string overlayText = FormatMessage(
                OverlayTemplate,
                username,
                displayName,
                viewerCount
            );

            string chatMessage = FormatMessage(
                ChatTemplate,
                username,
                displayName,
                viewerCount
            );

            CPH.LogInfo("[RaidBeacon] Username: " + username);
            CPH.LogInfo("[RaidBeacon] Display name: " + displayName);
            CPH.LogInfo("[RaidBeacon] Overlay: " + overlayText);
            CPH.LogInfo("[RaidBeacon] Chat preview: " + chatMessage);

            CPH.LogInfo(
                "[RaidBeacon] Incoming raid data check passed. " +
                "No chat messages or OBS changes were made."
            );

            return true;
        }
        catch (ArgumentException exception)
        {
            CPH.LogError(
                "[RaidBeacon] Template error: " + exception.Message
            );

            return false;
        }
    }

    /// <summary>
    /// Expands supported placeholders once and rejects unknown placeholders.
    /// </summary>
    private string FormatMessage(
        string template,
        string username,
        string displayName,
        int viewerCount
    )
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ArgumentException(
                "Message templates must not be empty."
            );
        }

        return Regex.Replace(template, @"\{([^{}]+)\}", match =>
        {
            switch (match.Groups[1].Value)
            {
                case "username":
                    return username;

                case "displayName":
                    return displayName;

                case "viewers":
                    return viewerCount.ToString(
                        CultureInfo.InvariantCulture
                    );

                case "viewerLabel":
                    return viewerCount == 1
                        ? SingularViewerLabel
                        : PluralViewerLabel;

                case "url":
                    return "https://www.twitch.tv/" + username;

                default:
                    throw new ArgumentException(
                        "Unknown placeholder: " + match.Value
                    );
            }
        });
    }
}