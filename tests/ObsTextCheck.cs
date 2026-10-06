using System;

public class CPHInline
{
    // CONFIGURATION
    private const int ObsConnection = 0;

    private const string SceneName = "RaidBeacon";
    private const string TextSourceName = "RaidBeacon Text";

    private const string TestText =
        "ExampleRaider raided with 12 viewers!";

    // Duration in milliseconds: 5000 = 5 seconds.
    private const int DisplayDurationMs = 5000;

    // EXECUTION
    public bool Execute()
    {
        CPH.LogInfo("[RaidBeacon] Starting OBS text check.");

        if (DisplayDurationMs < 1 || DisplayDurationMs > 30000)
        {
            CPH.LogError(
                "[RaidBeacon] DisplayDurationMs must be between " +
                "1 and 30000."
            );

            return false;
        }

        if (!CPH.ObsIsConnected(ObsConnection))
        {
            CPH.LogError(
                "[RaidBeacon] OBS is not connected. " +
                "Check the configured OBS connection."
            );

            return false;
        }

        bool completed = false;

        try
        {
            // Hide first so the previous text is not briefly displayed.
            CPH.ObsSetSourceVisibility(
                SceneName,
                TextSourceName,
                false,
                ObsConnection
            );

            // Replace the text while the source is hidden.
            CPH.ObsSetGdiText(
                SceneName,
                TextSourceName,
                TestText,
                ObsConnection
            );

            // Display the updated source.
            CPH.ObsSetSourceVisibility(
                SceneName,
                TextSourceName,
                true,
                ObsConnection
            );

            CPH.LogInfo(
                "[RaidBeacon] Text update and show requested."
            );

            CPH.Wait(DisplayDurationMs);

            completed = true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[RaidBeacon] OBS text check failed: " +
                exception.Message
            );
        }
        finally
        {
            // Attempt cleanup even if an earlier operation throws.
            try
            {
                if (CPH.ObsIsConnected(ObsConnection))
                {
                    CPH.ObsSetSourceVisibility(
                        SceneName,
                        TextSourceName,
                        false,
                        ObsConnection
                    );

                    CPH.LogInfo(
                        "[RaidBeacon] Text hide requested."
                    );
                }
                else
                {
                    completed = false;

                    CPH.LogError(
                        "[RaidBeacon] OBS disconnected before cleanup. " +
                        "Hide the test source manually if needed."
                    );
                }
            }
            catch (Exception exception)
            {
                completed = false;

                CPH.LogError(
                    "[RaidBeacon] Could not clean up the text source: " +
                    exception.Message
                );
            }
        }

        if (completed)
        {
            CPH.LogInfo(
                "[RaidBeacon] OBS command sequence finished. " +
                "Confirm that the text appeared and disappeared."
            );
        }

        return completed;
    }
}