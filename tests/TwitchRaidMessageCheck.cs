using System;
using System.Globalization;
using System.Text.RegularExpressions;

public class CPHInline
{
    // CONFIGURATION
    private const bool PreviewOnly = true;
    private const bool EnableChatMessage = true;
    private const bool EnableShoutout = false;

    // false sends the welcome from your broadcaster account.
    private const bool UseBotAccount = false;
    private const bool AllowBroadcasterFallback = true;

    private const string ChatTemplate =
        "Ahoy! {displayName} arrived with {viewers} {viewerLabel}! " +
        "Welcome aboard! Check them out at {url}";

    private const string SingularViewerLabel = "raider";
    private const string PluralViewerLabel = "raiders";

    // TEST DATA
    // Enter a real streamer's login without @ or a URL.
    private const string TestRaiderLogin = "";
    private const string TestDisplayName = "";
    private const int TestViewerCount = 1;

    public bool Execute()
    {
        string login;
        string message;

        try
        {
            login = TestRaiderLogin.Trim().ToLowerInvariant();

            if (!Regex.IsMatch(login, @"\A[a-z0-9_]+\z"))
            {
                throw new ArgumentException(
                    "Set TestRaiderLogin to a Twitch login " +
                    "containing only letters, numbers or underscores."
                );
            }

            if (TestViewerCount < 1)
            {
                throw new ArgumentException(
                    "TestViewerCount must be positive."
                );
            }

            string displayName = string.IsNullOrWhiteSpace(TestDisplayName)
                ? login
                : TestDisplayName.Trim();

            displayName = Regex.Replace(
                displayName,
                @"[\p{Cc}\p{Zl}\p{Zp}]",
                " "
            );

            message = FormatMessage(
                login,
                displayName,
                TestViewerCount
            );
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[RaidBeacon] Configuration error: " + exception.Message
            );

            return false;
        }

        CPH.LogInfo("[RaidBeacon] Chat preview: " + message);
        CPH.LogInfo("[RaidBeacon] Shoutout target: " + login);

        if (PreviewOnly)
        {
            CPH.LogInfo(
                "[RaidBeacon] Preview only. No Twitch actions performed."
            );

            return true;
        }

        // Separate calls ensure one failure does not skip the other.
        bool chatCompleted = TrySendChat(message);
        bool shoutoutCompleted = TrySendShoutout(login);

        return chatCompleted && shoutoutCompleted;
    }

    /// <summary>
    /// Expands supported placeholders in one pass.
    /// </summary>
    private string FormatMessage(
        string login,
        string displayName,
        int viewers
    )
    {
        if (string.IsNullOrWhiteSpace(ChatTemplate))
        {
            throw new ArgumentException("ChatTemplate must not be blank.");
        }

        string message = Regex.Replace(
            ChatTemplate,
            @"\{([^{}]+)\}",
            match =>
            {
                switch (match.Groups[1].Value)
                {
                    case "username":
                        return login;

                    case "displayName":
                        return displayName;

                    case "viewers":
                        return viewers.ToString(
                            CultureInfo.InvariantCulture
                        );

                    case "viewerLabel":
                        return viewers == 1
                            ? SingularViewerLabel
                            : PluralViewerLabel;

                    case "url":
                        return "https://www.twitch.tv/" + login;

                    default:
                        throw new ArgumentException(
                            "Unknown placeholder: " + match.Value
                        );
                }
            }
        );

        // Keep this welcome message on one line.
        return Regex.Replace(message, @"[\p{Cc}\p{Zl}\p{Zp}]", " ");
    }

    private bool TrySendChat(string message)
    {
        if (!EnableChatMessage)
        {
            CPH.LogInfo("[RaidBeacon] Chat message disabled.");
            return true;
        }

        try
        {
            CPH.SendMessage(
                message,
                UseBotAccount,
                AllowBroadcasterFallback
            );

            CPH.LogInfo(
                "[RaidBeacon] Chat send requested. " +
                "Confirm receipt in Twitch chat."
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[RaidBeacon] Chat request failed: " + exception.Message
            );

            return false;
        }
    }

    private bool TrySendShoutout(string login)
    {
        if (!EnableShoutout)
        {
            CPH.LogInfo("[RaidBeacon] Native shoutout disabled.");
            return true;
        }

        try
        {
            bool success = CPH.TwitchSendShoutoutByLogin(login);

            if (!success)
            {
                CPH.LogInfo(
                    "[RaidBeacon] Native shoutout returned false. " +
                    "Check stream status, cooldowns and account permissions. " +
                    "No automatic retry was attempted."
                );

                return false;
            }

            CPH.LogInfo(
                "[RaidBeacon] Native shoutout reported success."
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[RaidBeacon] Shoutout request failed: " +
                exception.Message
            );

            return false;
        }
    }
}