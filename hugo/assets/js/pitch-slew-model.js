// A browser teaching model, not an STM32 clock or sensor simulation.
export const clamp = (value, low, high) => Math.min(high, Math.max(low, value));
export const distanceToPitch = (mm) => 3 * clamp((600 - mm) / 550, 0, 1);
export const pitchToFrequency = (octaves) => 110 * 2 ** octaves;
export const smoothingAlpha = (dt, tau) => tau <= 0 ? 1 : -Math.expm1(-dt / tau);
export const smoothPitch = (value, target, dt, tau) =>
  tau <= 0 ? target : value + smoothingAlpha(dt, tau) * (target - value);

export class PitchModel {
  constructor() {
    this.time = 0;
    this.distance = 325;
    this.sensorRate = 50;
    this.tau = 0.1;
    this.noise = 3;
    this.sweeping = false;
    this.target = distanceToPitch(this.distance);
    this.output = this.target;
    this.measuredDistance = this.distance;
    this.nextSample = 1 / this.sensorRate;
    this.nextTrace = 1 / 120;
    this.seed = 1947;
    this.trace = [{ time: 0, target: this.target, output: this.output }];
  }

  setSensorRate(rate) {
    this.sensorRate = rate;
    this.nextSample = this.time + 1 / rate;
  }

  setTau(seconds) {
    this.tau = seconds;
    if (seconds <= 0) this.output = this.target;
  }

  randomNoise() {
    // Repeatable bounded measurement noise, independent of drawing rate.
    this.seed = (Math.imul(1664525, this.seed) + 1013904223) >>> 0;
    return (this.seed / 4294967296 * 2 - 1) * this.noise;
  }

  sample() {
    if (this.sweeping) this.distance = 325 + 210 * Math.sin(this.time * Math.PI / 2);
    this.measuredDistance = clamp(this.distance + this.randomNoise(), 50, 600);
    this.target = distanceToPitch(this.measuredDistance);
    if (this.tau <= 0) this.output = this.target;
  }

  advance(seconds) {
    if (!Number.isFinite(seconds) || seconds < 0) throw new RangeError("Invalid elapsed time");
    const end = this.time + seconds;
    const events = [];
    while (this.time < end) {
      const at = Math.min(end, this.nextSample, this.nextTrace);
      this.output = smoothPitch(this.output, this.target, at - this.time, this.tau);
      this.time = at;
      if (at >= this.nextSample) {
        this.sample();
        events.push({ time: at, target: this.target });
        this.nextSample += 1 / this.sensorRate;
      }
      if (at >= this.nextTrace) {
        this.trace.push({ time: at, target: this.target, output: this.output });
        this.nextTrace += 1 / 120;
      }
    }
    // Seven seconds at 120 samples/s: bounded even when the plot is hidden.
    if (this.trace.length > 840) this.trace.splice(0, this.trace.length - 840);
    return events;
  }
}
