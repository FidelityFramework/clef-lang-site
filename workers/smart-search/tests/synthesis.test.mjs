import assert from "node:assert/strict";
import test from "node:test";
import { handleSynthesizeRequest, handleSynthesizeStreamRequest } from "../dist/fable/src/Handlers.js";

// Capture the actual AI request from both HTTP handlers. Stop at the model call
// so these checks need no live AI service, stream, database or analytics writes.
for (const [route, handler, stream] of [
  ["/synthesize", handleSynthesizeRequest, undefined],
  ["/synthesize-stream", handleSynthesizeStreamRequest, true],
]) {
  test(`${route} sends the shared automatic-proof architecture to the model`, async () => {
    const query = "Quantum optionality";
    const snippet = "Specialized physical models build on ordinary typed programs.";
    let sent;
    const stop = new Error("Captured model request");
    const request = { json: async () => ({ query, results: [{ title: query, url: "/quantum/", snippet, score: 1 }] }) };
    const env = { AI: { run: async (_model, input) => { sent = input; throw stop; } } };
    await assert.rejects(handler(request, env, {}), error => error === stop);

    const prompt = sent.messages[0].content;
    assert.ok(prompt.includes(query));
    assert.ok(prompt.includes(snippet));
    assert.match(prompt, /Tier 1[^\n]+without developer proof annotations/);
    assert.match(prompt, /Tier 2[^\n]+Graph coeffects[^\n]+automatically/);
    assert.match(prompt, /Tier 3[^\n]+PSG hyperedges[^\n]+lemmas with checked premises/);
    assert.match(prompt, /Tier 4[^\n]+cRHL[^\n]+pRHL[^\n]+checked derivations/);
    assert.match(prompt, /Rocq-founded Tier 3 lemma retains that foundation/);
    assert.match(prompt, /unitarity proofs are a possible specialized application/);
    assert.match(prompt, /Unsupported premises, timeouts and missing semantic bridges remain unresolved/);
    assert.equal(sent.max_tokens, 256);
    assert.equal(sent.temperature, 0.3);
    assert.equal(sent.stream, stream);
  });
}
