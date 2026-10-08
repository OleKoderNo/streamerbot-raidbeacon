using System;
using System.Text.RegularExpressions;

public class CPHInline
{
    private const string GlobalPrefix = "RaidBeacon.Run.";

    public bool Execute()
    {
        string runId;
        string state;

        if (!CPH.TryGetArg<string>("raidBeaconRunId", out runId) ||
            !Regex.IsMatch(runId ?? "", @"\A[a-f0-9]{32}\z"))
        {
            CPH.LogError("[RaidBeacon] Signal has an invalid run ID.");
            return false;
        }

        if (!CPH.TryGetArg<string>("raidBeaconState", out state) ||
            (state != "started" &&
             state != "complete" &&
             state != "failed"))
        {
            CPH.LogError("[RaidBeacon] Signal has an invalid state.");
            return false;
        }

        string key = GlobalPrefix + runId + ".";

        // Only a currently waiting fleet action may receive signals.
        if (!CPH.GetGlobalVar<bool>(key + "active", false))
        {
            CPH.LogInfo(
                "[RaidBeacon] Ignored inactive run signal: " + runId);
            return true;
        }

        // A terminal state cannot be overwritten by a later signal.
        string result = CPH.GetGlobalVar<string>(
            key + "result", false);

        if (!string.IsNullOrEmpty(result))
        {
            return true;
        }

        if (state == "started")
        {
            CPH.SetGlobalVar(key + "started", true, false);
        }
        else
        {
            CPH.SetGlobalVar(key + "result", state, false);
        }

        CPH.LogInfo(
            "[RaidBeacon] Signal " + state + " for " + runId);

        if (state == "failed")
        {
            string detail;

            if (CPH.TryGetArg<string>(
                "raidBeaconError", out detail) &&
                !string.IsNullOrWhiteSpace(detail))
            {
                detail = Regex.Replace(
                    detail, @"[\p{Cc}\p{Zl}\p{Zp}]", " ");

                if (detail.Length > 300)
                    detail = detail.Substring(0, 300);

                CPH.LogError("[RaidBeacon] Overlay error: " + detail);
            }
        }

        return true;
    }
}