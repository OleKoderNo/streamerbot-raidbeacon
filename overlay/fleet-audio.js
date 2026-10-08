"use strict";

window.raidBeaconAudio = (() => {
  // Paths are relative to fleet-overlay.html.
  const files = {
    cannon: "../assets/audio/cannon_fire.ogg",
    splash: "../assets/audio/cannon_miss.ogg",
  };

  const parameters = new URLSearchParams(location.search);

  function readNumber(name, fallback, min, max, integer = false) {
    const value = parameters.has(name)
      ? Number(parameters.get(name))
      : fallback;

    if (
      !Number.isFinite(value) ||
      value < min ||
      value > max ||
      (integer && !Number.isInteger(value))
    ) {
      throw new Error("Invalid audio setting: " + name);
    }

    return value;
  }

  const enabled = parameters.get("effects") === "1";
  const limit = readNumber("maxSounds", 6, 1, 16, true);
  const gap = readNumber("effectGapMs", 100, 0, 5000, true);

  const volumes = {
    cannon: readNumber("cannonVolume", 0.12, 0, 1),
    splash: readNumber("splashVolume", 0.1, 0, 1),
  };

  const pools = {};
  const active = new Set();
  const disabled = new Set();
  const lastPlayed = { cannon: -Infinity, splash: -Infinity };
  let stopped = false;

  // Audio failures must not interrupt the visual alert.
  function disable(type, message) {
    if (stopped || disabled.has(type)) return;

    disabled.add(type);

    for (const audio of pools[type] || []) {
      audio.pause();
      active.delete(audio);
    }

    console.warn(`[RaidBeacon] ${type} audio disabled: ${message}`);
  }

  if (enabled) {
    for (const type of Object.keys(files)) {
      pools[type] = Array.from({ length: limit }, () => {
        const audio = new Audio();

        audio.preload = "auto";
        audio.volume = volumes[type];
        audio.onended = () => active.delete(audio);
        audio.onerror = () => {
          active.delete(audio);
          disable(type, "Could not load " + files[type]);
        };

        audio.src = files[type];
        audio.load();
        return audio;
      });
    }
  }

  // Skip busy or unready sounds instead of queuing delayed effects.
  function play(type) {
    if (
      !enabled ||
      stopped ||
      disabled.has(type) ||
      !pools[type] ||
      volumes[type] === 0
    ) {
      return;
    }

    const now = performance.now();

    if (active.size >= limit || now - lastPlayed[type] < gap) return;

    const audio = pools[type].find(
      (item) => !active.has(item) && item.readyState >= 3,
    );

    if (!audio) return;

    active.add(audio);
    lastPlayed[type] = now;

    try {
      audio.currentTime = 0;

      void audio.play().then(
        () => {
          if (stopped || disabled.has(type)) {
            audio.pause();
            active.delete(audio);
          }
        },
        (error) => {
          active.delete(audio);
          disable(type, error.message);
        },
      );
    } catch (error) {
      active.delete(audio);
      disable(type, error.message);
    }
  }

  // Stop remaining audio when the fleet finishes or fails.
  function stop() {
    if (stopped) return;
    stopped = true;

    for (const pool of Object.values(pools)) {
      for (const audio of pool) audio.pause();
    }

    active.clear();
  }

  window.addEventListener("pagehide", stop);

  return { play, stop };
})();
