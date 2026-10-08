"use strict";

window.raidBeaconSignals = (() => {
  async function hashBase64(value) {
    const bytes = new TextEncoder().encode(value);
    const digest = await crypto.subtle.digest("SHA-256", bytes);

    return btoa(String.fromCharCode(...new Uint8Array(digest)));
  }

  /**
   * Reports an overlay state to Streamer.bot.
   * An accepted request means the signal action was requested,
   * not that the complete raid alert has finished.
   */
  function send(state, message = "") {
    const config = window.raidBeaconConnection;
    const runId = new URLSearchParams(location.search).get("run");

    if (!config) {
      return Promise.reject(
        new Error("Missing raidbeacon.local.js configuration."),
      );
    }

    if (!/^[a-f0-9]{32}$/i.test(runId || "")) {
      return Promise.reject(new Error("Missing or invalid run ID."));
    }

    if (!["started", "complete", "failed"].includes(state)) {
      return Promise.reject(new Error("Invalid overlay state."));
    }

    if (
      !Number.isFinite(config.timeoutMs) ||
      config.timeoutMs <= 0 ||
      !config.signalAction?.trim()
    ) {
      return Promise.reject(new Error("Invalid signal configuration."));
    }

    return new Promise((resolve, reject) => {
      const socket = new WebSocket(config.url);
      const authId = `${runId}:${state}:auth`;
      const requestId = `${runId}:${state}:signal`;

      let settled = false;
      let helloReceived = false;
      let sent = false;

      const timer = setTimeout(() => {
        finish(new Error("Streamer.bot signal request timed out."));
      }, config.timeoutMs);

      function finish(error) {
        if (settled) return;
        settled = true;

        clearTimeout(timer);
        socket.close();

        if (error) reject(error);
        else resolve();
      }

      function sendSignal() {
        if (settled || sent) return;
        sent = true;

        socket.send(
          JSON.stringify({
            request: "DoAction",
            id: requestId,
            action: { name: config.signalAction },
            args: {
              raidBeaconRunId: runId,
              raidBeaconState: state,
              raidBeaconError: String(message).slice(0, 300),
            },
          }),
        );
      }

      socket.onmessage = async (event) => {
        try {
          if (settled) return;

          const data = JSON.parse(event.data);

          if (data.request === "Hello" && !helloReceived) {
            helloReceived = true;

            if (!data.authentication) {
              sendSignal();
              return;
            }

            if (!config.password) {
              throw new Error("Streamer.bot requires a WebSocket password.");
            }

            const { salt, challenge } = data.authentication;
            const secret = await hashBase64(config.password + salt);
            const authentication = await hashBase64(secret + challenge);

            if (settled) return;

            socket.send(
              JSON.stringify({
                request: "Authenticate",
                id: authId,
                authentication,
              }),
            );

            return;
          }

          if (data.id === authId) {
            if (data.status !== "ok") {
              throw new Error("Streamer.bot authentication failed.");
            }

            sendSignal();
            return;
          }

          if (data.id === requestId) {
            if (data.status !== "ok") {
              throw new Error(
                "Streamer.bot rejected the overlay signal. " +
                  "Check the signal action name and that it is enabled.",
              );
            }

            finish();
          }
        } catch (error) {
          finish(error);
        }
      };

      socket.onerror = () => {
        finish(new Error("Could not connect to Streamer.bot."));
      };

      socket.onclose = () => {
        if (!settled) {
          finish(new Error("Streamer.bot closed the signal connection."));
        }
      };
    });
  }

  return { send };
})();
