import assert from "node:assert/strict";
import test from "node:test";
import { buildSynthesisPrompt, buildSynthesisPromptFull } from "../dist/fable/src/Search.js";

const result = {
  id: "quantum-optionality",
  pageTitle: "Quantum Optionality",
  sectionTitle: "Supported constructions",
  pageUrl: "/docs/design/quantum-optionality/",
  contentType: "design",
  snippet: "Specialized physical models build on ordinary typed programs.",
  publishedAt: "",
  score: 1,
};

// Exercise the compiled production prompt builders. A quantum query and excerpt
// need the architecture context even when neither mentions a verification tier.
for (const [name, build] of [
  ["full-content", () => buildSynthesisPromptFull("Quantum optionality", false, [[result, result.snippet]])],
  ["snippet-only", () => buildSynthesisPrompt("Quantum optionality", [result])],
]) {
  test(`${name} synthesis carries automatic dispatch and specialized-proof boundaries`, () => {
    const prompt = build();
    assert.ok(prompt.includes(result.snippet), "The source evidence must reach the model");
    assert.match(prompt, /Tier 1[^\n]+without developer proof annotations/);
    assert.match(prompt, /Tier 2[^\n]+Graph coeffects[^\n]+automatically/);
    assert.match(prompt, /Tier 3[^\n]+PSG hyperedges[^\n]+lemmas with checked premises/);
    assert.match(prompt, /Tier 4[^\n]+cRHL[^\n]+pRHL[^\n]+checked derivations/);
    assert.match(prompt, /Rocq-founded Tier 3 lemma retains that foundation/);
    assert.match(prompt, /general-purpose systems language/);
    assert.match(prompt, /unitarity proofs are a possible specialized application/);
    assert.match(prompt, /Unsupported premises, timeouts and missing semantic bridges remain unresolved/);
  });
}

test("a direct request retains its task and evidence alongside the architecture guard", () => {
  const request = "How are graph obligations generated?";
  const body = "The compiler derives supported conditions from graph participants.";
  const prompt = buildSynthesisPromptFull(request, true, [[result, body]]);
  assert.ok(prompt.includes(request));
  assert.ok(prompt.includes(body));
  assert.match(prompt, /Answer the USER REQUEST directly/);
  assert.match(prompt, /do not insert proof tiers into unrelated answers/);
});
