# Combined raid action check

## Purpose

Combine raid data, welcome text, native Twitch shoutouts, and the
queued OBS fleet into one C# action.

## Setup

- Action: RaidBeacon - Combined Raid Check
- Queue: RaidBeacon Alerts, blocking
- Code: tests/CombinedRaidCheck.cs
- Signal receiver: RaidBeacon - Overlay Signal
- Signal queue: RaidBeacon Signals, separate and blocking

Existing overlay files and OBS audio settings are reused.

## Test modes

TestMode uses sample data and always prevents Twitch posting.
PreviewOnly prevents Twitch posting when TestMode is disabled.
Both modes still run the OBS overlay and audio.

## Failure behaviour

Chat and shoutout requests are handled independently.
A Twitch request failure does not prevent fleet playback.
Requests are not automatically retried.
Overlay startup and completion retain their configurable timeouts.

Successful visual completion does not prove Twitch message delivery.
A sent Twitch message cannot be undone by an overlay failure.

## Verification

- [x] Combined C# compiles.
- [x] Test mode previews chat and shoutout without sending either.
- [x] One-viewer test displays five ships and correct text.
- [x] Arrival, cannon, and splash audio work.
- [x] Overlay reports completion.
- [x] Two combined test requests execute sequentially.
- [ ] Incoming raid arguments verified.
- [ ] Live chat message received.
- [ ] Live native shoutout verified.
