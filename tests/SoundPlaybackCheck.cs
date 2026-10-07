using System;
using System.IO;

public class CPHInline
{
    // CONFIGURATION — edit these values.
    private const bool EnableSound = true;

    private const string SoundFile =
        @"C:\YOUR-PATH\streamerbot-raidbeacon\assets\audio\raid-start.wav";

    // 0.0 = silent; 1.0 = full volume.
    private const float SoundVolume = 0.25f;

    // Suitable for this isolated test.
    // Fleet integration can start playback without waiting.
    private const bool WaitForSoundToFinish = true;

    private const string PlaybackName = "RaidBeacon Arrival";

    public bool Execute()
    {
        CPH.LogInfo("[RaidBeacon] Starting sound playback check.");

        if (!EnableSound)
        {
            CPH.LogInfo("[RaidBeacon] Sound disabled. Playback skipped.");
            return true;
        }

        try
        {
            ValidateSettings();

            CPH.LogInfo(
                "[RaidBeacon] Requesting playback: " +
                Path.GetFileName(SoundFile));

            CPH.PlaySound(
                SoundFile,
                SoundVolume,
                WaitForSoundToFinish,
                PlaybackName,
                false);

            CPH.LogInfo(
                "[RaidBeacon] Playback call returned. " +
                "Confirm the sound is audible and present in an OBS recording.");

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[RaidBeacon] Sound playback check failed: " +
                exception.Message);

            return false;
        }
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(SoundFile))
            throw new ArgumentException("SoundFile must not be blank.");

        if (!Path.IsPathRooted(SoundFile))
            throw new ArgumentException(
                "SoundFile must be a full absolute file path.");

        if (!File.Exists(SoundFile))
            throw new FileNotFoundException(
                "Sound file was not found. Check SoundFile.", SoundFile);

        if (float.IsNaN(SoundVolume) ||
            float.IsInfinity(SoundVolume) ||
            SoundVolume < 0f ||
            SoundVolume > 1f)
        {
            throw new ArgumentOutOfRangeException(
                "SoundVolume",
                "SoundVolume must be between 0.0 and 1.0.");
        }

        if (string.IsNullOrWhiteSpace(PlaybackName))
            throw new ArgumentException("PlaybackName must not be blank.");
    }
}