// Run with: node scripts/check-pitch-slew.mjs
// Independent mathematical expectations for the browser teaching model.
import assert from "node:assert/strict";
import {
  PitchModel, distanceToPitch, pitchToFrequency, smoothPitch,
} from "../hugo/assets/js/pitch-slew-model.js";

function near(actual, expected, tolerance = 1e-10) {
  assert.ok(Math.abs(actual - expected) < tolerance, `${actual} != ${expected}`);
}

near(pitchToFrequency(distanceToPitch(600)), 110);
near(pitchToFrequency(distanceToPitch(50)), 880);
near(pitchToFrequency(distanceToPitch(325)), 311.1269837220809);
near(pitchToFrequency(distanceToPitch(900)), 110);

// One time constant closes 1 - 1/e of a step, independently of update count.
for (const rate of [20, 50, 100]) {
  let value = 0;
  for (let i = 0; i < rate / 10; i++) value = smoothPitch(value, 1, 1 / rate, 0.1);
  near(value, 0.6321205588285577);
}
let irregular = 0;
for (const dt of [0.017, 0.001, 0.042, 0.04]) {
  irregular = smoothPitch(irregular, 1, dt, 0.1);
}
near(irregular, 0.6321205588285577);
near(smoothPitch(0, 1, 0.2995732273553991, 0.1), 0.95);
near(smoothPitch(0.9, 0.2, 0.02, 0), 0.2);

// Calling the model from different drawing cadences must not change sensor
// history or the resulting filter, including its deterministic noise stream.
for (const rate of [5, 20, 50, 100]) {
  const a = new PitchModel(), b = new PitchModel();
  a.setSensorRate(rate); b.setSensorRate(rate);
  a.distance = b.distance = 80;
  for (let i = 0; i < 600; i++) a.advance(1 / 60);
  for (let i = 0; i < 1440; i++) b.advance(1 / 144);
  near(a.output, b.output, 1e-8);
  assert.equal(a.seed, b.seed, "Sensor samples must not depend on draw cadence");
  assert.ok(a.trace.length <= 840, "Plot history must remain bounded");
}

console.log("Pitch model: mapping, step response, time normalization, bypass, independent sampling and bounded history passed.");
