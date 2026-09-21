---
title: "Pitch, touch and demand"
linkTitle: "Pitch, touch and demand"
description: "An interactive pitch-smoothing experiment for HelloDISCO, and what a musical control reveals about demand-driven UI."
date: 2026-09-18
draft: false
authors: ["Houston Haynes"]
tags: ["Audio", "Embedded", "Design", "User Interfaces"]
---

For HelloDISCO, the goal is to turn our Clef display and input work into a playable instrument: a Minimoog-inspired synthesizer with a range-sensing theremin mode. Moving a hand toward the sensor would raise the pitch, and the screen could show how that motion becomes a musical control. The pitch-smoothing example from our design discussion illustrates how that control could work.

This browser model lets us hear the response while varying the sampling and smoothing independently. It is inspired by a possible Cortex-M4 control role. We would select the sensor and processor allocation through the board work, where we can measure the timing.

{{< pitch-slew >}}

Move the input and compare the sampled target with the smoothed response. Sampling captures the desired hand position at intervals and holds the latest target between captures. Even a smoothly moving hand can produce a staircase of target values. A real sensor would add noise and calibration requirements, with its own response to a lost or out-of-range reading.

The smoother approaches each held target gradually:

$$y_{n+1}=y_n+\alpha(x_n-y_n),\qquad \alpha=1-e^{-\Delta t/\tau}$$

Here, \(x_n\) is the current target and \(y_n\) is the smoothed control. The update interval is \(\Delta t\), and \(\tau\) is the time constant. For a fixed target, one time constant closes about 63% of the initial distance. A larger time constant gives a gentler, slower approach. A smaller one follows the target more closely, including more of its steps.

“Slew” is musical shorthand here. The implementation is one-pole smoothing, whose rate depends on the distance to the target. A strict slew-rate limiter would instead impose a maximum change per second. With either approach, the instrument has to trade a smoother control for some response delay.

The sensor's sampling interval and the smoother's time constant are separate from the oscillator's note frequency. For a positive time constant, the continuous-time first-order model has a cutoff of \(1/(2\pi\tau)\). That cutoff describes how changes in the control are filtered.

Our model maps distance linearly into three octaves, smooths that logarithmic pitch coordinate, and converts the result to a frequency between 110 and 880 Hz. Smoothing normalized distance gives the same result with this mapping. A nonlinear distance calibration would change that relationship, and smoothing frequency directly would produce a different pitch trajectory.

Audio is optional. Enable it to listen, then stop it independently of the graph. The browser schedules the target events with a 40 ms buffer, adding delay to this audition. We would measure the instrument's latency on the board and choose the volume response separately. Final amplitude shaping could remain with the audio generator.

Hiding the graph while the tone continues makes the UI question concrete. The control data still has an audio consumer, even after the chart stops drawing. Our Fidelity demand model would allow the instrument or a background service to keep those calculations active under its own ownership. An optional derivation could become idle after its last consumer releases demand, retaining a result that may later be stale.

This is the intended behavior of our [reactive-area UI model](/docs/design/user-interfaces/). A visual inspector could be opened, moved or closed while the instrument continued responding. The [Incremental demand contract](/spec/draft/incremental-computation/#63-demand-registration) specifies the foundation, and [the cold half of concurrency](/blog/cold-half-of-concurrency/) describes why we favor that starting point.

On HelloDISCO we would compare captured input timing with the resulting audio and display activity under load. Closing the inspector would provide a useful test: visual work should stop while the instrument continues serving its active consumers.
