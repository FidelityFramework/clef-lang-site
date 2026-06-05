---
name: avoid-ai-prose
description: Apply when writing or editing any prose for clef-lang-site — blog posts, design docs, publication drafts, section _index.md files, tweets, quote-tweets, email summaries, slack messages, any first-person prose representing the project's voice. Catches the surface tells and underlying dispositions that mark prose as machine-generated and undermine the credibility of the technical content. Triggers on .md edits under hugo/content/, hugo/_vendor/, site-design/, or any prose drafting in conversation. Does NOT apply to source-code comments, API docs, or commit messages.
---

# Avoid AI Prose

The user has corrected the same patterns repeatedly across long working sessions, with explicit and increasing frustration. Each pattern is a downstream symptom of one underlying disposition: the **high-school term paper voice**, where structure is announced rather than allowed to emerge from the content. The discipline is to recognize that disposition in real time and write from a different one.

This skill consolidates nine feedback memories. When more depth is needed for a specific rule, the relevant memory file is linked below the rule.

## The underlying disposition to avoid

The high-school term paper voice is the register a student is taught because they are being graded on whether the structure of the essay is visible to the teacher. Topic sentences announce each paragraph. Sections open with thesis statements. Closings restate the thesis. Every metaphor gets explained. Every claim is announced before it is delivered.

A working writer does not need the structure to be visible. The reader sees the structure from the content. Three corrective moves, in order of importance:

1. **Delete every sentence whose only job is to tell the reader what the next sentence will say.** "The key insight is...", "This means that...", "What follows is...", "In this section we will...". Remove the sentence and read the paragraph again. If the paragraph still makes sense, the sentence was announcement.
2. **Describe what the post is, do not announce what the post will do.** "This post is how X lines up" describes. "In this post we will show how X lines up" announces. Long-form readers have already decided to read the post; it does not need to sell them on it.
3. **Acknowledge the actual sequence of events the work followed.** The engineering came first, the formal recognition came later, the categorical name was found in the literature after the work was in place. Honest sequencing is more credible than the dressed-up version where the writer pretends the theory came first.

Source: [feedback_prose_style.md](/home/hhh/.claude/projects/-home-hhh-repos-clef-lang-site/memory/feedback_prose_style.md) rule 15.

## Surface tells — scan-after-write

These are the patterns that mark prose as machine-generated. **The scan-after-write step is the only reliable corrective; the content-level rules alone will not catch them during drafting.** Each pattern below lists what to search for and what to do when it appears.

### Em and en dashes as clause separators

**Banned:** `—` (U+2014) and `–` (U+2013) used as sentence breaks. Hyphens in compound words ("dual-pass", "Root-mediated") are fine.

After writing any sentence longer than ~25 words, scan for `—` and `–` before saving. The em-dash form is the highest-probability completion when constructing long parenthetical asides; the content rule alone will not prevent it from appearing in real-time drafting.

**Replacement:** comma, colon, parenthetical, or sentence split, whichever best preserves the original structure.

### Run-on comma-tail enumeration

**Banned:** three or more comma-separated noun phrases attached to one verb. The threshold is mechanical, not subjective.

Warning signs to scan for before saving:
- "and" appearing more than twice in a single sentence
- a sentence whose final third is a series of noun phrases connected by commas and a closing "and"
- two or more parenthetical clauses with their own internal lists
- subject and verb 15+ words apart

When any sign appears, stop the sentence at the first complete clause and start a new one. The split version may feel choppy on first read; the comma-tail version is worse because the reader has to backtrack to figure out which noun the next clause modifies.

Source: [feedback_sentence_length_and_enumeration.md](/home/hhh/.claude/projects/-home-hhh-repos-clef-lang-site/memory/feedback_sentence_length_and_enumeration.md).

### Three structurally identical mid-length declaratives

**Banned:** three sentences in a row, each carrying a self-contained claim, with no rhythmic variation between them.

Example of the pattern to avoid: "Three pieces of mathematics describe the same underlying structure. Recognizing this lets a working engineer borrow infrastructure across communities. The three pieces are X, Y, and Z."

That cadence reads as thesis-consequence-enumeration delivered in the same beat, and it is the dead giveaway of AI-generated prose at the top of a long-form post. Human writers vary sentence length and only enumerate after they have established why the enumeration matters.

**Fix:** discursive entry point (personal recollection, historical anchor, or wide observation about a field), varied rhythm (long flowing sentences punctuated by shorter ones), reader brought into a disposition before being handed any technical inventory. Save the synthesis claim for paragraph two or three at the earliest.

### "It's not X, it's Y" and absence-describing

**Banned (surface):** the literal "X is not Y. It is Z" sentence shape.

**Banned (deeper, more dangerous):** describing what something *fails to do* or *cannot express* or *does not produce* when the same content can be described as what it *does do* or *does produce*.

Examples of the deeper form to avoid:
- "the analysis stalls" — describes absence of forward progress; say what the analysis switches to
- "the rule has no way to express that..." — describes absence of capability; say what the rule does return
- "the question cannot be answered" — describes absence of an answer; say what is returned in place
- "the bound is not tight enough" — describes absence of tightness; say what the bound's actual width is and what the threshold needs

**Fix:** state the positive content directly. If a gap is genuinely the load-bearing point, describe it as the difference between two positive things (what is returned vs. what the threshold needs), not as the absence of one of them.

### Hyperbole and evaluative inflation

**Banned:** "remarkable", "powerful", "elegant", "beautiful", "groundbreaking", "revolutionary", and similar evaluative inflation.

**Fix:** state what something does. Let the reader judge.

### Throat-clearing

**Banned:** "it's worth noting that", "interestingly", "fundamentally", "at its core", "notably", "importantly".

**Fix:** delete and write the next sentence.

### Register-mismatched filler

**Banned (substantive-feeling placeholders that are doing imprecision work):** "move" (the same move), "play" (the right play), "trick", "lever", "knob", "dial", "lift", "win", "beat", "outflank".

These words signal the writer has not yet articulated what is actually shared between two things. The fix is not a less casual synonym; the fix is to write the precise structural content the placeholder was standing in for. **If the precise content cannot be written, the comparison is not yet earned and should not appear at all.**

### Trailing prepositions in closing sentences

The final sentence of a section or post sets the cadence the reader leaves on. A trailing preposition ("which side it sits on", "the regime it operates under", "the boundary it falls outside of") or trailing pronoun hands the cadence away.

**Fix:** front the preposition ("on which side it sits"), restructure to end on a noun or strong verb, or rewrite the sentence.

### Closing recap / thesis restatement

**Banned:** final paragraphs whose job is to restate what was just said. The user calls these "high school term paper filler" and considers them insulting to the reader.

**Fix:** end on the last substantive point.

### Reader-sorting orientation patterns

**Banned in section _index, post openings, orientation surfaces:** "If you're coming from X..." / "If you're arriving from Y...". These sort the reader into a path rather than taking a position.

**Fix:** position-taking statement. "These are language-level commitments that inform our four-tier proof architecture, our most significant contribution to ML-family languages" beats "If you're arriving from the proof-architecture material, these are the language-level commitments the four-tier proof architecture depends on." Same content, different posture.

Source: [feedback_gestalt_over_enumeration.md](/home/hhh/.claude/projects/-home-hhh-repos-clef-lang-site/memory/feedback_gestalt_over_enumeration.md).

### Enumeration on orientation surfaces

**Banned in section _index summaries, post openings, anywhere the reader is being oriented:** four sentences in a row each previewing one of the section's articles in parallel structure ("X does this. Y does that. Z does the third thing.").

The article titles, table of contents, and headings already enumerate. The orientation surface should establish a gestalt and a stance.

**Fix:** one or two substantive position-taking sentences that give the reader the gestalt of the whole. The articles preview themselves once the reader clicks through.

### Academic communities described as siloed

**Banned phrasings:** "communities that rarely speak to one another", "fields that don't talk to each other", "siloed disciplines", "isolated subfields", "the gulf between X and Y", "researchers in separate towers", "communities that rarely intersect".

This is generative hand-wave that signals the writer does not know how academia works. Academic communities publish in shared journals, attend overlapping conferences, sit on shared review panels, cite across boundaries, trade graduate students.

**Fix:** name the *results* as the thing that lined up, not the *people* as the thing that did not communicate. "Results developed independently for different problems", "work that originated for different reasons", "results whose original motivations did not anticipate the connection", "papers that were never meant to address the same question".

Source: [feedback_prose_style.md](/home/hhh/.claude/projects/-home-hhh-repos-clef-lang-site/memory/feedback_prose_style.md) rule 14.

## Voice and posture

### Authorship: "our" vs "the"

The clef-lang-site has a position. It is the user's editorial voice sharing their own inventions against the broader context of external work. Readers need to distinguish "our claims about our work" from "our citations of others' work," and the possessive/generic contrast is what does that attribution.

**User's inventions — use "our" (or "my" in first-person):**
Fidelity framework, Clef (the language design), PSG (Program Semantic Graph), PHG (Program Hypergraph), DTS (Dimensional Type System), DMM (Dimensional Memory Model), BAREWire (the user's implementation; BARE itself is external), compilation sheaf, cellular sheaf framework (as applied here), coeffect system, Prospero, Olivier, CloudEdge library, and any framing or analysis original to the user.

**External — keep "the":**
MLIR, CIRCT, Q#, F#, IonQ, Google, Abramsky and Coecke's work, Cain et al., surface code, Gross code, bivariate bicycle codes, BP-OSD decoding, factory scheduling as a general concept, and any algorithm/protocol/result being cited from someone else's work.

**Discipline — the contrast lands once per paragraph, not on every reference:**
1. Use "our" on the first reference in a paragraph for a user-owned component, especially when the same sentence cites external work.
2. For follow-up references in the same paragraph, default to "the X" or "its X".
3. Never chain "our A... our B... our C" inside a single sentence if the shared antecedent is clear.
4. Position-taking verbs carry ownership without possessives: "we consider", "we treat", "we argue", "we built".
5. Section transitions ("For our Fidelity framework specifically:") re-establish ownership at the top of a block; subsequent references can rely on that.

Over-stamping is its own failure mode. The user has corrected both directions: "'our' 'mine' 'our' at every turn is as exhausting as 'the' 'some' and so forth that never determines what's the subject and focus of development and what's outside evidence."

Source: [feedback_our_vs_the_framing.md](/home/hhh/.claude/projects/-home-hhh-repos-clef-lang-site/memory/feedback_our_vs_the_framing.md).

### Verb choice: demonstrated vs architectural

When a paragraph shows one demonstrated companion (FPGA/CIRCT) and asserts an architectural property for a target the framework does not yet demonstrate end-to-end (quantum, neuromorphic), the verb attached to the architectural claim must not overstate.

**Wrong:** "Our multi-target compilation architecture handles this kind of lowering pipeline."
**Right:** "Our multi-target compilation architecture is designed for this kind of lowering pipeline."

**Architectural verbs (use for prospective targets):** is designed for, is architected for, accommodates, admits as a target, treats as a first-class target, generalizes to, extends to, would slot in as.

**Performance verbs (reserve for actually-running behavior):** handles, implements, performs, processes, does, compiles (unless literally describing compilation that has run), ships (reserve for actually-shipping components like BAREWire).

The whole "quantum-ready, not quantum-shipping" positioning of the site rests on being precise about what is demonstrated versus what is architectural. A single overclaiming verb undoes the credibility the rest of the paragraph spends to build.

Source: [feedback_demonstrated_vs_architectural.md](/home/hhh/.claude/projects/-home-hhh-repos-clef-lang-site/memory/feedback_demonstrated_vs_architectural.md).

### Value before mechanism in conclusions

In concluding sections, lead with the practical value, then the technical mechanism.

**Wrong order:** "Range propagation through the computation graph produces exact safety proofs for a well-defined class of computations. This approach satisfies proofs for a large portion of the physical computations safety-critical engineering depends on."

**Right order:** "This approach satisfies proofs for a large portion of the physical computations that safety-critical engineering depends on. Range propagation through the computation graph produces exact safety proofs for a well-defined class of computations."

The audience cares about what the proof delivers before how it works.

Source: [feedback_value_before_mechanism.md](/home/hhh/.claude/projects/-home-hhh-repos-clef-lang-site/memory/feedback_value_before_mechanism.md).

### Synthesis claim first, not last

When asked to synthesize across sources, do not write the source recaps and try to draw a synthesis out at the end as a closing tag. State the synthesis claim as the *opening* of the section, in the post's own voice and at a higher level of abstraction than any single source. Bring each source in as a *short illustration* of the synthesis claim, not as the subject of its own paragraph.

If a source's exposition takes more than two or three sentences, the section is in recap mode, not synthesis mode, and needs to be restructured.

### Audience mental models

When a paragraph makes two related claims that sit naturally together in the author's head but not in the reader's, separate them and signal the pivot.

Standing example: most readers who see "quantum" and "security" in the same paragraph are primed for the post-quantum-cryptography story (classical systems facing a CRQC). They are not primed for "verifying the integrity of a quantum computation itself." A paragraph that jams these together without signaling the shift asks the reader to share a conceptual bridge the author built but did not hand them.

**Default to the reader's primary mental model for the topic:**
- quantum + security → post-quantum cryptography
- compilation + performance → target-specific code generation
- types + safety → compile-time error prevention

**Corrective pattern:**
1. Lead with the framing the reader already has.
2. Separate the claims into distinct paragraphs.
3. Signal the pivot explicitly: "A related but distinct concern, and the one most readers are already thinking about when X appears in the same paragraph, is Y."
4. When a secondary claim does not earn its place in the current post, pull it entirely. Trust a cross-link elsewhere.

Source: [feedback_audience_mental_models.md](/home/hhh/.claude/projects/-home-hhh-repos-clef-lang-site/memory/feedback_audience_mental_models.md).

## Definitions and tool names

### Plain-language glosses lead with structural fact, then behavioral

When defining a technical term in plain language for a non-specialist reader, lead with the structural existence claim, not the behavioral description.

The user's standing example: an identity arrow on an object \(X\) is a morphism \(\mathrm{id}_X : X \to X\); it points from the object back to itself, and has the property that composing it with any other arrow leaves that arrow unchanged. Calling it an arrow "that does nothing" elides the self-loop and the unit law at once, and gets it wrong on both counts.

### If a definitional gloss needs more than one sentence, link out instead

A blog post is not a textbook. Use the term as a term, link it to its Wikipedia entry (or canonical reference image, or a specific section of an existing site doc) on first use, and trust the curious reader to click.

Multi-sentence textbook treatments of *category*, *groupoid*, *functor*, *poset*, *sheaf*, *cohomology*, or any other piece of standard mathematical vocabulary are out of scope and should be replaced with hyperlinks. Inline prose definitions belong only when they fit in one short clause and lead with the structural fact.

The user has been explicit: "Why are you describing category theory? This isn't a textbook. Either point to the wikipedia entry that has the image showing the classic diagram or build a mermaid diagram that shows it."

### Do not name a specialized tool the reader has not been introduced to

Naming Rocq, Lean, F*, EasyCrypt, hax, Why3, Coq, Isabelle, or similar specialized formal-methods tools by name in a post that has not previously introduced them within the same reading is a vocabulary budget violation, even when the named tool is the technically correct one.

**Fix:** use a generic descriptor for the structural role: "an external proof assistant", "a Rocq-class proof assistant", "an offline lemma library", "an SMT solver such as Z3". Name what the tool *does* without committing the reader to absorb new vocabulary they did not sign up for.

**Exceptions:** Z3 and MLIR are widely-known enough that they can appear without prior introduction in the framework's compiler-engineering register.

## Metaphor discipline

Land a metaphor once. Do not drive it through additional beats.

The temptation is to extend the metaphor through two or three additional sentences ("the shelves connect", "picking up the books", "carrying them to the desk", "Composer and Clef are the desk", "books open on it") because each beat feels like it underlines the previous one. Each beat does less work than the one before, and the cumulative effect is that the metaphor reads as belabored where one beat would have read as graceful.

**Right pattern:** one sentence (or one short phrase inside a sentence) that summons the image, paired with the substantive content the closing needs to deliver. Then end.

The reader who registered the opening image will catch the callback without having it pointed at; the reader who did not register it will get the substantive content from the same sentence and lose nothing. The user has been emphatic: "Don't overplay the metaphor. The callback is nice! I like it. But don't just drive over it with a truck."

## Quote-tweets

A quote-tweet is for the reader who has just seen the parent post, not for the reader who has already read the linked content. It is not a mini-blog-post, a synthesis claim, or an opportunity to demonstrate mastery of the parent post's subject.

The audience needs three things and only three:
1. Acknowledgment of the parent post in the user's voice (typically opens with "Love this take" or similar).
2. One sentence that creates curiosity about the link by drawing a plain-language connection to the user's own work.
3. The link itself.

The connection in (2) should be in language a novice in the user's field can understand. Inside-baseball jargon (specific theorem names, fragment notation, decision-procedure references) belongs in the linked post.

Test before sending: would a smart person who has never heard of this field understand the connection in one read? If not, the quote-tweet is in the wrong register.

## Verification claims

Any prose touching verification, decidability, type checking, Z3, Rocq, Hoare logic, the four-tier architecture, or the framework's compilation pipeline must be checked against the full verification claims checklist before saving.

Read [feedback_verification_claims_checklist.md](/home/hhh/.claude/projects/-home-hhh-repos-clef-lang-site/memory/feedback_verification_claims_checklist.md) in full before each such piece. The error rate goes up on short-form writing because the checklist feels like overkill for 280 characters; the error rate going up is exactly why the checklist applies anyway.

Common errors to scan for (this is a summary, not a substitute for the full checklist):
- "Dependent type checking is undecidable in general." Wrong. Type checking given a complete term is decidable; only *inference* and *proof search* are undecidable.
- "Z3 is intractable" or "Z3 is incomplete." Wrong without qualification. Z3 is sound and complete on QF_LIA and QF_BV.
- "Tier 4 discharges through Z3." Wrong. Tier 4 is type-checked by the Composer's pRHL type checker against a Rocq-proved rule library; Z3 handles arithmetic leaves only.
- "Rocq is in the TCB for Tier 1 through Tier 3." Wrong. Z3 alone is the TCB for Tiers 1–3; Rocq enters only at Tier 4.
- "Free theorems apply at Tier 2 or above." Wrong. Free theorems apply at Tier 1 only.
- "Tier 2 proves rejection-sampling termination." Wrong. Tier 2 proves what holds at loop exit given the loop exits; Tier 3 proves the loop exits with probability 1.

## Source-of-claim discipline

If a question has been flagged as "must verify against the paper before claiming" in the same conversation or in earlier work, do not turn around and assert the answer in prose. Generative intuitions about what an algorithm "probably exploits" (triangle inequality, monotone costs, specific structural assumptions) are not substitutes for having read Lemma N.M of the actual paper.

Defer the claim until the paper has been read. Describe the result at the level the abstract supports, and stop there.

This rule applies symmetrically to the framework's own publications: do not paraphrase what a section of the DTS/DMM paper or PHG paper says without rereading that section first. The easy paraphrase from memory is the one most likely to drift.

## Pre-save scan

Before saving any piece of prose written for clef-lang-site, run this scan. The discipline is to run it, not to trust that the content rules caught everything during drafting.

1. Search for `—` and `–`. Replace each with comma, colon, parenthetical, or sentence split.
2. Search for "it's worth noting", "interestingly", "fundamentally", "at its core", "notably", "importantly". Delete or rewrite.
3. For each sentence with three or more commas, count comma-separated noun phrases attached to the main verb. If three or more, split.
4. Search for "remarkable", "powerful", "elegant", "beautiful", "groundbreaking", "revolutionary". Replace with descriptive content.
5. Search for "is not", "cannot", "has no way", "fails to". For each, check whether the sentence is describing an absence when a presence is available.
6. Search for "the X" where X is a user-owned component (PSG, PHG, DTS, DMM, BAREWire, Clef, Fidelity framework, compilation sheaf, coeffect system). Evaluate whether ownership lands at least once per paragraph.
7. For prospective-target verbs (quantum, neuromorphic, future hardware), check the lead verb is architectural ("is designed for") not performance ("handles").
8. For section _index files and post openings, check whether the prose takes a position or enumerates contents.
9. Check the closing sentence of every section. Does it end on a substantive word, or trail off into a preposition or pronoun?
10. For any verification vocabulary ("undecidable", "Z3", "Rocq", "tier", "free theorem", "extraction", "conservative", "loop invariant", "termination", "indistinguishability"), re-check against the verification claims checklist in full.
11. Search for "move", "play", "trick", "lever", "knob", "dial", "lift", "win", "beat", "outflank". For each, check whether the placeholder is hiding imprecision; if so, write the precise structural content instead.
12. Search for "communities that rarely", "fields that don't talk", "siloed", "isolated subfields", "gulf between". Rewrite to name the results, not the communities.

## When this skill applies

- Editing any `.md` file under `hugo/content/`, `hugo/_vendor/`, or `site-design/`.
- Drafting blog posts, design docs, or publication drafts in conversation.
- Composing tweets, quote-tweets, email summaries, slack messages in the user's voice.
- Any prose that will be read by someone outside the project's commit history.

## When this skill does not apply

- Source-code comments and inline F# or JavaScript documentation.
- Commit messages (handled by the commit skill / git conventions).
- API docs internal to the Fidelity framework (the "the" rule is relaxed there because the reader already has the context).
- Strictly internal scratch notes the user has marked as such.
