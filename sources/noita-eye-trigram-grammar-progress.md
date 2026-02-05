# Noita Eye Messages — Trigram Grammar Analysis (Progress Report)

**Status:** Active research  
**Scope:** Structural / grammatical analysis of Eye Messages  
**Engine status:** Message generation engine reimplemented in C# (base-7 pipeline confirmed)

---

## 1. Problem Statement

The Eye Messages in *Noita* are non-sprite, engine-rendered glyph messages that appear only in unmodded runs and only after entering parallel worlds. Despite years of community effort, no public, reproducible decoding exists.

This document summarizes **current confirmed knowledge**, **invalidated hypotheses**, and **the most promising analytical direction**: **trigram grammar analysis**.

---

## 2. Confirmed Engine-Level Facts

### 2.1 Message generation pipeline

1. Two 32-bit hex strings (`a`, `b`) are concatenated into a single 64-bit unsigned integer.
2. The integer is repeatedly divided by **7**, collecting remainders.
3. The resulting base-7 digit stream is post-processed:
   - Trailing zero dropped
   - Each digit decremented by 1
4. Resulting values:
   - `0–4` → eye directions
   - `5` → **end-of-line marker** (no eye rendered)

---

## 3. Spatial & Structural Observations

### 3.1 East–West pairing

- 4 West ↔ 4 East messages spawn in paired coordinates
- 1 East-only message has no West counterpart
- Eye orientation is **not mirrored**

---

## 4. Trigram Discovery

- **83 unique trigrams** observed out of 125 possible
- Strong evidence of grammar-like constraints

---

## 5. Trigram Grammar Hypothesis

Trigrams are treated as atomic tokens (words / morphemes / particles).  
Grammar emerges from **token transitions** and **line breaks**, not direct substitution.

---

## 6. Grammar Signals

- Punctuation-like tokens
- Affix-like tokens
- Closed-class particles

---

## 7. Transition Graph Model

Directed weighted graph:
- Node = trigram
- Edge = observed transition

Used to detect hubs, clusters, and repeated paths.

---

## 8. East–West Validation

- Structural invariance checks
- Parallel-text hypothesis testing

---

## 9. Invalidated Hypotheses

- Simple substitution
- Vigenère
- Visual mirroring
- Pure randomness

---

## 10. Recommended Next Steps

1. Generate canonical eye streams
2. Test trigram alignments
3. Build transition graphs
4. Compare East–West structure

---

## 11. Current Best Hypothesis

The Eye Messages encode a **structured symbolic language** using trigrams as atomic units.
