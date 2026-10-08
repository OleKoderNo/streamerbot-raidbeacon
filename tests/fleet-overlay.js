"use strict";

// Visual settings. C# supplies raid data through the browser source URL.
const settings = {
  testViewers: 12,
  shipsPerViewer: 5,
  waterColor: "#1C85D9",
  assetFolder: "../assets/visual/ships/",
  shipsPerSecond: 8,
  maxActiveShips: 60,
  shipWidth: 72,
  minSpeed: 145,
  maxSpeed: 220,
  cannonSize: 28,
  impactSize: 70,
};

const parameters = new URLSearchParams(window.location.search);
const shouldPlay = parameters.has("run");

if (parameters.has("viewers")) {
  settings.testViewers = Number(parameters.get("viewers"));
}

if (parameters.has("shipsPerViewer")) {
  settings.shipsPerViewer = Number(parameters.get("shipsPerViewer"));
}

const canvas = document.getElementById("fleet");
const ctx = canvas.getContext("2d");
const errorBox = document.getElementById("error");
const raidAlert = document.getElementById("raid-alert");
const raidHeading = document.getElementById("raid-heading");
const raidMessage = document.getElementById("raid-message");

const ships = [];
const projectiles = [];
const impacts = [];

const totalShips = settings.testViewers * settings.shipsPerViewer;
const random = (min, max) => min + Math.random() * (max - min);

let spawned = 0;
let exited = 0;
let shots = 0;
let hits = 0;
let budget = 0;
let previousTime = null;
let elapsed = 0;
let stopped = false;
let art;

// The canvas is rotated by CSS to make the fleet travel left to right.
function resize() {
  canvas.width = window.innerHeight;
  canvas.height = window.innerWidth;
  ctx.imageSmoothingEnabled = false;
}

function showRaidText() {
  const heading = parameters.get("heading");
  const message = parameters.get("message");

  if (!heading?.trim() || !message?.trim()) {
    throw new Error("Missing raid heading or message.");
  }

  raidHeading.textContent = heading;
  raidMessage.textContent = message;
  raidAlert.hidden = false;
}

function hideRaidText() {
  raidAlert.hidden = true;
  raidHeading.textContent = "";
  raidMessage.textContent = "";
}

function reportError(error) {
  if (stopped) return;
  stopped = true;

  window.raidBeaconAudio?.stop();
  hideRaidText();
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  const message = error instanceof Error ? error.message : String(error);
  errorBox.hidden = false;
  errorBox.textContent = "RaidBeacon: " + message;
  console.error("[RaidBeacon]", error);

  if (window.raidBeaconSignals) {
    void window.raidBeaconSignals
      .send("failed", message)
      .catch((signalError) => {
        console.error(
          "[RaidBeacon] Failure report was not delivered.",
          signalError,
        );
      });
  }
}

function loadImage(file, width, height) {
  return new Promise((resolve, reject) => {
    const image = new Image();

    image.onload = () => {
      if (image.naturalWidth !== width || image.naturalHeight !== height) {
        reject(new Error("Unexpected asset dimensions: " + file));
      } else {
        resolve(image);
      }
    };

    image.onerror = () => reject(new Error("Could not load " + file));
    image.src = settings.assetFolder + file;
  });
}

function validate() {
  for (const key of ["testViewers", "shipsPerViewer", "maxActiveShips"]) {
    if (!Number.isSafeInteger(settings[key]) || settings[key] < 1) {
      throw new Error(key + " must be a positive integer.");
    }
  }

  if (
    !Number.isSafeInteger(totalShips) ||
    !Number.isSafeInteger(totalShips * 2)
  ) {
    throw new Error("Ship/shot count exceeds the safe integer range.");
  }

  for (const key of [
    "shipsPerSecond",
    "shipWidth",
    "minSpeed",
    "maxSpeed",
    "cannonSize",
    "impactSize",
  ]) {
    if (!Number.isFinite(settings[key]) || settings[key] <= 0) {
      throw new Error(key + " must be positive and finite.");
    }
  }

  if (settings.maxSpeed < settings.minSpeed) {
    throw new Error("maxSpeed must be >= minSpeed.");
  }
}

function createShip() {
  const width = settings.shipWidth * random(0.85, 1.15);

  return {
    x: random(canvas.width * 0.15, canvas.width * 0.85),
    y: (-width * 256) / 144,
    width,
    height: (width * 256) / 144,
    speed: random(settings.minSpeed, settings.maxSpeed),
    phase: random(0, Math.PI * 2),
    // One cannon on each side. Each fires once.
    guns: [
      { side: -1, threshold: random(0.25, 0.4), fired: false, age: 10 },
      { side: 1, threshold: random(0.48, 0.65), fired: false, age: 10 },
    ],
  };
}

function shipCenterX(ship) {
  return ship.x + Math.sin(elapsed * 1.5 + ship.phase) * 5;
}

function gunPosition(ship, gun) {
  return {
    x: shipCenterX(ship) + gun.side * ship.width * 0.28,
    y: ship.y + ship.height * 0.08,
  };
}

// Prefer water that is not currently occupied by another ship.
function chooseTarget(ship, gun) {
  const origin = gunPosition(ship, gun);
  const margin = Math.min(
    settings.impactSize / 2,
    canvas.width / 8,
    canvas.height / 8,
  );

  const lowX = gun.side < 0 ? margin : origin.x + 35;
  const highX = gun.side < 0 ? origin.x - 35 : canvas.width - margin;
  let target;

  for (let attempt = 0; attempt < 20; attempt += 1) {
    target = {
      x: Math.max(
        margin,
        Math.min(canvas.width - margin, random(lowX, Math.max(lowX, highX))),
      ),
      y: random(margin, Math.max(margin, canvas.height - margin)),
    };

    const occupied = ships.some(
      (other) =>
        Math.abs(target.x - shipCenterX(other)) < other.width * 0.7 &&
        Math.abs(target.y - other.y) < other.height * 0.7,
    );

    if (!occupied) break;
  }

  return target;
}

function fire(ship, gun) {
  gun.fired = true;
  gun.age = 0;

  projectiles.push({
    origin: gunPosition(ship, gun),
    target: chooseTarget(ship, gun),
    age: 0,
    duration: random(0.6, 1.1),
  });

  shots += 1;
  window.raidBeaconAudio.play("cannon");
}

function drawFrame(image, frame, frameSize, x, y, size, mirror = false) {
  ctx.save();
  ctx.translate(Math.round(x), Math.round(y));

  if (mirror) ctx.scale(-1, 1);

  ctx.drawImage(
    image,
    frame * frameSize,
    0,
    frameSize,
    frameSize,
    -size / 2,
    -size / 2,
    size,
    size,
  );

  ctx.restore();
}

function updateShips(dt) {
  for (let i = ships.length - 1; i >= 0; i -= 1) {
    const ship = ships[i];
    ship.y += ship.speed * dt;

    const progress = (ship.y + ship.height) / (canvas.height + ship.height * 2);

    for (const gun of ship.guns) {
      gun.age += dt;

      if (!gun.fired && progress >= gun.threshold) {
        fire(ship, gun);
      }
    }

    if (ship.y - ship.height / 2 > canvas.height) {
      ships.splice(i, 1);
      exited += 1;
    }
  }
}

function updateEffects(dt) {
  for (let i = projectiles.length - 1; i >= 0; i -= 1) {
    const ball = projectiles[i];
    ball.age += dt;

    if (ball.age >= ball.duration) {
      impacts.push({ x: ball.target.x, y: ball.target.y, age: 0 });
      projectiles.splice(i, 1);
      hits += 1;
      window.raidBeaconAudio.play("splash");
    }
  }

  for (let i = impacts.length - 1; i >= 0; i -= 1) {
    impacts[i].age += dt;

    if (impacts[i].age >= 0.6) {
      impacts.splice(i, 1);
    }
  }
}

function draw() {
  ctx.fillStyle = settings.waterColor;
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  // Draw impacts underneath passing ships.
  for (const impact of impacts) {
    drawFrame(
      art.impact,
      Math.min(3, Math.floor(impact.age / 0.15)),
      320,
      impact.x,
      impact.y,
      settings.impactSize,
    );
  }

  for (const ship of ships) {
    ctx.drawImage(
      art.ship,
      Math.round(shipCenterX(ship) - ship.width / 2),
      Math.round(ship.y - ship.height / 2),
      ship.width,
      ship.height,
    );

    for (const gun of ship.guns) {
      const position = gunPosition(ship, gun);
      const frame =
        gun.age < 0.45 ? Math.min(2, Math.floor(gun.age / 0.15)) : 0;

      drawFrame(
        art.cannon,
        frame,
        160,
        position.x,
        position.y,
        settings.cannonSize,
        gun.side > 0,
      );
    }
  }

  ctx.fillStyle = "#212133";

  for (const ball of projectiles) {
    const t = Math.min(1, ball.age / ball.duration);
    const x = ball.origin.x + (ball.target.x - ball.origin.x) * t;
    const y =
      ball.origin.y +
      (ball.target.y - ball.origin.y) * t -
      Math.sin(t * Math.PI) * 45;

    ctx.beginPath();
    ctx.arc(x, y, 4, 0, Math.PI * 2);
    ctx.fill();
  }
}

// Finish only after all ships, projectiles, and impacts have cleared.
function complete() {
  if (
    exited !== totalShips ||
    shots !== totalShips * 2 ||
    hits !== totalShips * 2
  ) {
    throw new Error("Fleet completed with unexpected ship or effect counts.");
  }

  stopped = true;
  window.raidBeaconAudio.stop();
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  hideRaidText();

  console.info(
    `[RaidBeacon] COMPLETE ships=${exited}/${totalShips} shots=${shots} impacts=${hits}`,
  );

  void window.raidBeaconSignals.send("complete").catch((error) => {
    console.error("[RaidBeacon] Completion report was not delivered.", error);
  });
}

function animate(now) {
  if (previousTime === null) previousTime = now;

  const dt = Math.min((now - previousTime) / 1000, 0.1);
  previousTime = now;
  elapsed += dt;

  budget = Math.min(
    budget + dt * settings.shipsPerSecond,
    settings.maxActiveShips,
  );

  // Limit concurrent ships without discarding the remaining fleet.
  while (
    budget >= 1 &&
    spawned < totalShips &&
    ships.length < settings.maxActiveShips
  ) {
    ships.push(createShip());
    spawned += 1;
    budget -= 1;
  }

  updateShips(dt);
  updateEffects(dt);
  draw();

  if (
    spawned < totalShips ||
    ships.length ||
    projectiles.length ||
    impacts.length
  ) {
    requestAnimationFrame(renderFrame);
  } else {
    complete();
  }
}

function renderFrame(now) {
  if (stopped) return;

  try {
    animate(now);
  } catch (error) {
    reportError(error);
  }
}

async function start() {
  // Stay transparent until C# requests a run.
  if (!shouldPlay) return;

  try {
    validate();

    if (!window.raidBeaconAudio) {
      throw new Error("Missing or invalid fleet-audio.js.");
    }

    if (!window.raidBeaconSignals) {
      throw new Error("Missing overlay-signals.js.");
    }

    resize();

    const [ship, cannon, impact] = await Promise.all([
      loadImage("ship.png", 144, 256),
      loadImage("cannon.png", 480, 160),
      loadImage("impact.png", 1280, 320),
    ]);

    art = { ship, cannon, impact };
    showRaidText();

    await window.raidBeaconSignals.send("started");

    console.info(
      `[RaidBeacon] START viewers=${settings.testViewers} ships=${totalShips}`,
    );

    requestAnimationFrame(renderFrame);
  } catch (error) {
    reportError(error);
  }
}

window.addEventListener("resize", resize);
start();
