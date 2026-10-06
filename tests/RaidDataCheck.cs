using System;

public class CPHInline
{
    // CONFIGURATION
    // Change these values to test different raid details.
    private const string TestUsername = "exampleraider";
    private const string TestDisplayName = "ExampleRaider";
    private const int TestViewerCount = 12;

    // Available placeholders:
    // {username}, {displayName}, {viewers}, {viewerLabel}, {url}
    private const string OverlayTemplate =
        "{displayName} raided with {viewers} {viewerLabel}!";

    private const string ChatTemplate =
        "Welcome {displayName} and your {viewers} {viewerLabel}! " +
        "Check them out at {url}";

    // EXECUTION
    public bool Execute()
    {
        CPH.LogInfo("[RaidBeacon] Starting raid data check.");

        if (string.IsNullOrWhiteSpace(TestUsername))
        {
            CPH.LogError("[RaidBeacon] TestUsername must not be empty.");
            return false;
        }

        if (TestViewerCount < 1)
        {
            CPH.LogError("[RaidBeacon] TestViewerCount must be at least 1.");
            return false;
        }

        string username = TestUsername.Trim().ToLowerInvariant();

        string displayName = string.IsNullOrWhiteSpace(TestDisplayName)
            ? username
            : TestDisplayName.Trim();

        string overlayText = FormatMessage(
            OverlayTemplate,
            username,
            displayName,
            TestViewerCount
        );

        string chatMessage = FormatMessage(
            ChatTemplate,
            username,
            displayName,
            TestViewerCount
        );

        CPH.LogInfo("[RaidBeacon] Overlay: " + overlayText);
        CPH.LogInfo("[RaidBeacon] Chat preview: " + chatMessage);
        CPH.LogInfo("[RaidBeacon] Raid data check passed.");

        return true;
    }

    /// <summary>
    /// Replaces message placeholders with the supplied raid details.
    /// </summary>
    private string FormatMessage(
        string template,
        string username,
        string displayName,
        int viewerCount
    )
    {
        string viewerLabel = viewerCount == 1 ? "viewer" : "viewers";
        string profileUrl = "https://www.twitch.tv/" + username;

        return template
            .Replace("{username}", username)
            .Replace("{viewers}", viewerCount.ToString())
            .Replace("{viewerLabel}", viewerLabel)
            .Replace("{url}", profileUrl)
            .Replace("{displayName}", displayName);
    }
}