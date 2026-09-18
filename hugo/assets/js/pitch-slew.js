import { PitchModel, pitchToFrequency, smoothingAlpha } from "./pitch-slew-model.js";

class PitchStudy {
  constructor(root) {
    this.root = root;
    this.model = new PitchModel();
    this.get = (role) => root.querySelector(`[data-role="${role}"]`);
    this.control = (name) => root.querySelector(`[data-control="${name}"]`);
    this.action = (name) => root.querySelector(`[data-action="${name}"]`);
    this.canvas = this.get("plot");
    this.context = this.canvas.getContext("2d");
    this.visible = false;
    this.plotHidden = false;
    this.timer = null;
    this.audio = null;
    this.audioStarting = false;
    this.audioGeneration = 0;
    this.disposed = false;
    this.lastReadout = 0;
    this.reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    this.controller = new AbortController();
    const listen = (node, event, handler) => node.addEventListener(event, handler, { signal: this.controller.signal });

    for (const name of ["distance", "tau", "rate", "noise"]) {
      listen(this.control(name), "input", () => {
        const value = Number(this.control(name).value);
        if (name === "distance") { this.model.distance = value; this.model.sweeping = false; }
        if (name === "tau") this.model.setTau(value / 1000);
        if (name === "rate") this.model.setSensorRate(value);
        if (name === "noise") this.model.noise = value;
        this.updateControls();
        if (name === "tau") this.updateAudio();
      });
    }
    listen(this.action("step"), "click", () => {
      this.model.sweeping = false;
      this.model.distance = this.model.distance > 325 ? 160 : 490;
      this.updateControls();
    });
    listen(this.action("sweep"), "click", () => {
      this.model.sweeping = !this.model.sweeping;
      this.updateControls();
    });
    listen(this.action("plot"), "click", () => {
      this.plotHidden = !this.plotHidden;
      this.canvas.hidden = this.plotHidden;
      this.get("hidden-plot").hidden = !this.plotHidden;
      this.action("plot").textContent = this.plotHidden ? "Show plot" : "Hide plot";
      this.action("plot").setAttribute("aria-pressed", String(this.plotHidden));
      this.updateState();
      if (!this.plotHidden) this.draw();
    });
    listen(this.action("audio"), "click", () => this.audio || this.audioStarting ? this.stopAudio() : this.startAudio());
    listen(this.control("tone"), "change", () => this.updateAudio());
    listen(document, "visibilitychange", () => {
      if (document.hidden) this.stopAudio();
      this.reconcileDemand();
    });
    listen(window, "pagehide", () => { this.stopAudio(); this.stopTimer(); });
    listen(window, "pageshow", () => this.reconcileDemand());

    this.resizeObserver = new ResizeObserver(() => this.draw());
    this.resizeObserver.observe(this.canvas.parentElement);
    this.intersectionObserver = new IntersectionObserver((entries) => {
      this.visible = entries[0].isIntersecting;
      this.reconcileDemand();
    }, { threshold: 0 });
    this.intersectionObserver.observe(root);
    this.removalObserver = new MutationObserver(() => {
      if (!root.isConnected) this.dispose();
    });
    this.removalObserver.observe(document.body, { childList: true, subtree: true });
    root.querySelector("fieldset").disabled = false;
    root.dataset.initialized = "true";
    this.updateControls();
    this.updateReadings();
  }

  updateControls() {
    const m = this.model;
    this.control("distance").value = String(Math.round(m.distance));
    this.get("distance").textContent = `${Math.round(m.distance)} mm`;
    this.get("tau").textContent = m.tau ? `${Math.round(m.tau * 1000)} ms` : "Bypass";
    this.get("rate").textContent = `${m.sensorRate} / sec`;
    this.get("noise").textContent = `±${m.noise} mm`;
    this.action("sweep").setAttribute("aria-pressed", String(m.sweeping));
    this.get("response").textContent = m.tau
      ? `At ${m.sensorRate} updates per second, a ${Math.round(m.tau * 1000)} ms time constant gives α ≈ ${smoothingAlpha(1 / m.sensorRate, m.tau).toFixed(3)}. A step closes 63% of its gap in ${Math.round(m.tau * 1000)} ms and 95% in about ${Math.round(-Math.log(0.05) * m.tau * 1000)} ms.`
      : "Bypass follows the sampled target immediately. The target is still held between sensor samples; increasing the sensor rate and smoothing the result solve different problems.";
  }

  updateReadings() {
    this.get("raw").textContent = pitchToFrequency(this.model.target).toFixed(1);
    this.get("smooth").textContent = pitchToFrequency(this.model.output).toFixed(1);
    if (this.model.sweeping) {
      this.control("distance").value = String(Math.round(this.model.distance));
      this.get("distance").textContent = `${Math.round(this.model.distance)} mm`;
    }
  }

  updateState() {
    this.get("state").textContent = this.timer ? (this.plotHidden ? "Data live · plot off" : "Live") : "Paused";
  }

  reconcileDemand() {
    if (this.disposed) { this.stopTimer(); return; }
    if (!this.root.isConnected) { this.dispose(); return; }
    // An audible consumer can keep data live when the widget scrolls away.
    // An actual hidden document suspends this educational model and its tone.
    const demanded = !document.hidden && (this.visible || this.audio);
    if (demanded && !this.timer) {
      this.lastWall = performance.now();
      this.timer = window.setInterval(() => this.tick(), 8);
    } else if (!demanded) this.stopTimer();
    this.updateState();
  }

  stopTimer() {
    if (this.timer) window.clearInterval(this.timer);
    this.timer = null;
    this.updateState();
  }

  tick() {
    const now = performance.now();
    // Bound recovery work after a stalled browser; this is a simulation clock.
    const dt = Math.min((now - this.lastWall) / 1000, 0.25);
    this.lastWall = now;
    const events = this.model.advance(dt);
    if (events.length) this.queueAudio(events);
    if (now - this.lastReadout >= 50) {
      this.lastReadout = now;
      this.updateReadings();
      if (this.visible && !this.plotHidden && (!this.reducedMotion.matches || now - (this.lastDraw || 0) >= 250)) {
        this.lastDraw = now;
        this.draw();
      }
    }
    if (!this.root.isConnected) this.dispose();
  }

  draw() {
    if (!this.context || this.plotHidden) return;
    const width = this.canvas.parentElement.clientWidth;
    const height = this.canvas.parentElement.clientHeight;
    if (!width || !height) return;
    const ratio = Math.min(window.devicePixelRatio || 1, 2);
    if (this.canvas.width !== Math.round(width * ratio) || this.canvas.height !== Math.round(height * ratio)) {
      this.canvas.width = Math.round(width * ratio);
      this.canvas.height = Math.round(height * ratio);
    }
    const c = this.context;
    c.setTransform(ratio, 0, 0, ratio, 0, 0);
    c.clearRect(0, 0, width, height);
    const left = 44, right = width - 12, top = 18, bottom = height - 24;
    const y = (pitch) => bottom - pitch / 3 * (bottom - top);
    const end = Math.max(6, this.model.time), start = end - 6;
    const x = (time) => left + (time - start) / 6 * (right - left);
    c.font = "10px ui-monospace, monospace";
    c.lineWidth = 1; c.setLineDash([]);
    for (let octave = 0; octave <= 3; octave++) {
      c.strokeStyle = "#2b3741"; c.beginPath(); c.moveTo(left, y(octave)); c.lineTo(right, y(octave)); c.stroke();
      c.fillStyle = "#9dabb5"; c.textAlign = "right"; c.fillText(String(110 * 2 ** octave), left - 9, y(octave) + 3);
    }
    for (let second = Math.ceil(start); second <= end; second++) {
      c.strokeStyle = "#202c35"; c.beginPath(); c.moveTo(x(second), top); c.lineTo(x(second), bottom); c.stroke();
    }
    c.save(); c.beginPath(); c.rect(left, top - 4, right - left, bottom - top + 8); c.clip();
    const trace = this.model.trace.filter((point) => point.time >= start - 1 / 120);
    for (const [key, color, dashed] of [["target", "#ffb568", true], ["output", "#6ed8c4", false]]) {
      c.beginPath(); c.strokeStyle = color; c.lineWidth = dashed ? 1.5 : 2.5; c.setLineDash(dashed ? [5, 4] : []);
      trace.forEach((point, index) => {
        if (!index) c.moveTo(x(point.time), y(point[key]));
        else {
          if (dashed) c.lineTo(x(point.time), y(trace[index - 1][key]));
          c.lineTo(x(point.time), y(point[key]));
        }
      });
      c.stroke();
    }
    c.restore(); c.setLineDash([]);
    c.fillStyle = "#9dabb5"; c.textAlign = "left"; c.fillText("time →", left, height - 7);
  }

  async startAudio() {
    const Audio = window.AudioContext || window.webkitAudioContext;
    if (!Audio) { this.get("audio-status").textContent = "Audio is unavailable in this browser."; return; }
    const generation = ++this.audioGeneration;
    this.audioStarting = true;
    this.action("audio").textContent = "Cancel sound";
    let context;
    try {
      context = new Audio();
      await context.resume();
      if (generation !== this.audioGeneration || document.hidden || !this.root.isConnected) {
        await context.close(); return;
      }
      const oscillator = context.createOscillator(), gain = context.createGain();
      oscillator.type = "sine"; oscillator.frequency.value = 110;
      oscillator.detune.value = 1200 * (this.control("tone").value === "raw" ? this.model.target : this.model.output);
      gain.gain.setValueAtTime(0, context.currentTime);
      gain.gain.linearRampToValueAtTime(0.055, context.currentTime + 0.025);
      oscillator.connect(gain).connect(context.destination); oscillator.start();
      // Schedule every sensor event on one audio-clock timeline. The small
      // buffer is specific to browser delivery, not an instrument latency claim.
      this.audio = { context, oscillator, gain, origin: context.currentTime + 0.04 - this.model.time };
      this.audioStarting = false;
      this.action("audio").textContent = "Stop tone";
      this.action("audio").setAttribute("aria-pressed", "true");
      this.get("audio-status").textContent = "Sine tone · 40 ms scheduling buffer";
      this.updateAudio(); this.reconcileDemand();
    } catch {
      if (context && context.state !== "closed") await context.close().catch(() => {});
      if (generation === this.audioGeneration) {
        this.audioStarting = false; this.action("audio").textContent = "Play tone";
        this.get("audio-status").textContent = "Sound could not start. Try Play tone again.";
      }
    }
  }

  updateAudio() {
    if (!this.audio) return;
    const { context, oscillator } = this.audio;
    if (this.audio.origin + this.model.time < context.currentTime) {
      this.audio.origin = context.currentTime + 0.04 - this.model.time;
    }
    const parameter = oscillator.detune, at = this.audio.origin + this.model.time;
    const raw = this.control("tone").value === "raw" || this.model.tau <= 0;
    // On mode/time-constant changes, adopt the selected model trajectory,
    // instead of smoothing the former raw audio trajectory a second time.
    parameter.cancelScheduledValues(at);
    parameter.setValueAtTime((raw ? this.model.target : this.model.output) * 1200, at);
    const target = this.model.target * 1200;
    if (!raw) parameter.setTargetAtTime(target, at, this.model.tau);
  }

  queueAudio(events) {
    if (!this.audio) return;
    const { context, oscillator, origin } = this.audio;
    if (origin + events[0].time < context.currentTime) {
      // A stalled browser cannot meet real-time deadlines. Rebase the audition
      // to the current modeled state rather than dropping intermediate events
      // silently and claiming it followed the original trajectory.
      this.audio.origin = context.currentTime + 0.04 - this.model.time;
      this.updateAudio();
      return;
    }
    const raw = this.control("tone").value === "raw" || this.model.tau <= 0;
    for (const event of events) {
      const at = origin + event.time;
      if (raw) oscillator.detune.setValueAtTime(event.target * 1200, at);
      else oscillator.detune.setTargetAtTime(event.target * 1200, at, this.model.tau);
    }
  }

  stopAudio() {
    ++this.audioGeneration;
    this.audioStarting = false;
    const audio = this.audio;
    this.audio = null;
    this.action("audio").textContent = "Play tone";
    this.action("audio").setAttribute("aria-pressed", "false");
    this.get("audio-status").textContent = "Sound is off.";
    if (audio) {
      const now = audio.context.currentTime;
      audio.gain.gain.cancelScheduledValues(now);
      audio.gain.gain.setValueAtTime(audio.gain.gain.value, now);
      audio.gain.gain.linearRampToValueAtTime(0, now + 0.025);
      audio.oscillator.stop(now + 0.03);
      window.setTimeout(() => { audio.oscillator.disconnect(); audio.gain.disconnect(); audio.context.close().catch(() => {}); }, 60);
    }
    this.reconcileDemand();
  }

  dispose() {
    if (this.disposed) return;
    this.disposed = true;
    this.stopAudio(); this.stopTimer();
    this.intersectionObserver.disconnect(); this.resizeObserver.disconnect(); this.removalObserver.disconnect(); this.controller.abort();
  }
}

document.querySelectorAll("[data-pitch-slew]").forEach((root) => {
  if (!root.dataset.initialized) new PitchStudy(root);
});
