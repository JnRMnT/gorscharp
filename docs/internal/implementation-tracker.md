# Gör# Implementation Tracker

Internal working document for the educational UX, C#→Gör, and Zemberek roadmap.

## Purpose

- Keep implementation scope stable while we work through the roadmap one item at a time.
- Track what is accepted, what is deferred, and what should be implemented next.
- Separate internal planning from public-facing docs and README messaging.

## Source Of Truth

- Beginner authoring source: `.gör`
- Generated C# role: teaching/build artifact
- C#→Gör role: explicit import/explanation workflow, not live bidirectional sync

## Core Architecture Rule

- Use Roslyn for C# structure, compiler feedback, and C#→Gör import.
- Use ZemberekDotNet for Turkish morphology, suffix explanation, ambiguity handling, and morphology-aware IDE help.
- Keep Turkish vocabulary and educational wording in `dictionaries/sozluk.json` and Sozluk models.

## Included Scope

- Mapped compiler diagnostics from generated C# back to Gör
- Reverse mapping service based on existing `/* gör:LINE */` comments
- Roslyn-backed C#→Gör importer for beginner-friendly constructs
- Narrative educational explanations during C#→Gör import
- Zemberek-backed morphology diagnostics for ambiguity, missing mappings, and resource/configuration failures
- Teacher-style hover explanations for suffix-based Turkish forms
- Morphology-aware completion for suffix/property suggestions
- Explicit IDE commands for import, refresh, and generated-C# inspection
- Constrained morphology normalization for already-supported suffix grammar
- Broader educational diagnostics after compiler feedback is in place

## Excluded Scope For This Roadmap

- True live bidirectional sync between `.gör` and `.cs`
- Full BCL translation
- Arbitrary LINQ, reflection, async/await, and advanced generics in first-pass C#→Gör import
- Perfect comment round-tripping
- Broad Zemberek usage outside Turkish morphology
- Freeform natural-language intent-to-code generation

## Priority Order

1. Roslyn-backed generated-C# diagnostics mapped back to Gör
2. Centralized reverse-mapping service
3. Zemberek ambiguity/resource diagnostics
4. Roslyn-backed C#→Gör importer
5. Narrative C#→Gör explanations
6. Teacher hover for suffix explanations
7. Morphology-aware completion
8. Explicit IDE workflows for import/refresh/mirror
9. Constrained morphology normalization
10. Broader semantic diagnostics
11. Optional Turkish naming-style lint
12. New language surface such as `dene/hata_varsa/sonunda` and OOP

## Immediate Queue

- [x] Define and implement the generated-C# compilation and reverse-mapping design in the language server
- [x] Add diagnostic code slots for generated-C# errors and improved morphology feedback
- [x] Design the Zemberek explanation model for suffix hover/completion
- [x] Replace the regex C#→Gör converter with a Roslyn visitor skeleton
- [x] Add a narrative explanation result model for C#→Gör import

## Newly Completed UX Slice

- [x] Add explicit VS Code workflows for C# import, mirror refresh, and generated-code inspection
- [x] Add explicit Visual Studio workflows for C# import, generated-code refresh, and generated-code inspection

## Completed

- 2026-04-13: Added Roslyn-backed generated-C# compilation in the language server.
- 2026-04-13: Added generated-C# source mapping based on `/* gör:LINE */` comments.
- 2026-04-13: Added `GOR2100` and `GOR2101` diagnostic codes for generated-C# compiler diagnostics.
- 2026-04-13: Added integration tests covering mapped generated-C# compiler errors, including multiline mapping.
- 2026-04-13: Verified with `dotnet build` and full test suite (`110` passing tests).
- 2026-04-13: Added `GOR3005` for Zemberek runtime/resource failures during morphology analysis.
- 2026-04-13: Turned silent suffix-analysis fallback into explicit diagnostics with fallback-aware warning/error severity.
- 2026-04-13: Improved morphology mapping-missing messages with case-specific wording and known verb/property suggestions.
- 2026-04-13: Added morphology tests covering runtime fallback and richer missing-mapping diagnostics.
- 2026-04-13: Added a shared suffix explanation service for hover/completion with dictionary-backed case descriptions and mapping suggestions.
- 2026-04-13: Extended hover and completion with suffix-aware educational help and case-specific suggestions.
- 2026-04-13: Hardened completion so partial/in-progress documents do not fail symbol analysis.
- 2026-04-13: Added integration tests for suffix hover and case-aware completion.
- 2026-04-13: Replaced the regex-based `fromcs` converter with a constrained Roslyn-based importer skeleton.
- 2026-04-13: Added importer support for top-level statements, simple methods, control flow, and `Program/Main` unwrapping.
- 2026-04-13: Added end-to-end integration tests for `fromcs` top-level conversion, method/control-flow import, and console-template import.
- 2026-04-13: Added a structured narrative explanation result model for C#→Gör import.
- 2026-04-13: Added optional `fromcs --explain` output for teacher-style import guidance.
- 2026-04-13: Added integration tests for narrative import explanations and CLI explain output.
- 2026-04-13: Added VS Code commands for C# import, mirror refresh, and explicit generated-code inspection entry points.
- 2026-04-13: Added VS Code CLI path resolution so editor workflows can use bundled or configured CLI binaries.
- 2026-04-13: Added Visual Studio commands for C# import explanation, generated C# refresh, and generated-code inspection entry points.
- 2026-04-13: Added constrained morphology normalization that infers unambiguous suffix case/mapping for already-supported suffix method/property forms.
- 2026-04-13: Added morphology tests for inferred-case normalization of suffix method/property expressions.
- 2026-04-13: Added broader semantic diagnostics for function call argument type mismatches (`GOR2007`) and educational undefined-function hints when a same-named variable exists.
- 2026-04-13: Added integration tests for semantic diagnostics around function calls and same-name variable/function confusion.
- 2026-04-13: Added operand-level semantic diagnostics (`GOR2008`) for invalid arithmetic, logical, and comparison operand combinations with educational guidance.
- 2026-04-13: Added integration tests for logical and arithmetic operand-type mismatch diagnostics.
- 2026-04-13: Added return-path completeness diagnostics (`GOR2009`) for typed functions that can end without returning a value.
- 2026-04-13: Added integration tests for typed-function return-path diagnostics, including all-branches-return coverage.
- 2026-04-13: Added unary operand diagnostics (`GOR2010`) for invalid `değil` and unary `-` usage.
- 2026-04-13: Added integration tests for unary operand-type mismatch diagnostics.
- 2026-04-13: Extended LSP quick-fix actions for semantic diagnostics (`GOR2007`, `GOR2008`, `GOR2010`) with educational guidance and safe unary-operator removal fixes.
- 2026-04-13: Added direct integration tests for `CodeActionHandler` quick-fix behavior for `GOR2007`, `GOR2008`, and `GOR2010`.
- 2026-04-13: Localized legacy quick-fix titles for `GOR2001`, `GOR2002`, and `GOR2003` to Turkish educational wording.
- 2026-04-13: Added explicit `CodeActionHandler` tests validating Turkish quick-fix titles for `GOR2001`, `GOR2002`, and `GOR2003`.
- 2026-04-13: Localized Visual Studio extension command menu items to Turkish (`src/GorSharp.VisualStudio/GorSharp.VisualStudio.vsct`): "Gor#'a Dönüştür", "Üretilen C# Yenile", "Üretilen C# Aç".
- 2026-04-13: Added variable shadowing diagnostics (`GOR2011`) for warning when a variable declares over an outer-scope variable or parameter.
- 2026-04-13: Added integration tests for variable shadowing diagnostics covering parameter shadowing, nested scope shadowing, and false-negative cases.
- 2026-04-13: Full test suite: 142 passed (up from 138 with 4 new GOR2011 tests).
- 2026-04-13: Added break/continue outside loop diagnostics (`GOR2012`) with loop context tracking for educational guidance.
- 2026-04-13: Added integration tests for break/continue validation covering out-of-loop errors and in-loop false-negatives.
- 2026-04-13: Full test suite: 146 passed (up from 142 with 4 new GOR2012 tests).
- 2026-04-13: Added unused-variable diagnostics (`GOR2013`) for declared-but-never-used local variables with warning severity.
- 2026-04-13: Added integration tests for unused-variable diagnostics covering positive and false-negative cases.
- 2026-04-13: Full test suite: 149 passed (up from 146 with 3 new GOR2013 tests).
- 2026-04-13: Added unreachable-code diagnostics (`GOR2014`) for statements after block-terminating `döndür`, `kır`, and `devam` statements.
- 2026-04-13: Added integration tests for unreachable-code diagnostics in function, `döngü`, and `tekrarla` blocks, plus no-terminator false-negative coverage.
- 2026-04-13: Full test suite: 153 passed (up from 149 with 4 new GOR2014 tests).
- 2026-04-13: Added conditionless `tekrarla` diagnostics (`GOR2015`) to warn about potentially infinite loops when for-loop condition is omitted.
- 2026-04-13: Added integration tests for `GOR2015` covering missing-condition warnings and false-negative cases for conditioned loops.
- 2026-04-13: Full test suite: 156 passed (up from 153 with 3 new GOR2015 tests).
- 2026-04-13: Added unused-function diagnostics (`GOR2016`) for user-defined functions that are never called.
- 2026-04-13: Added integration tests for `GOR2016` covering never-called and called function scenarios.
- 2026-04-13: Full test suite: 158 passed (up from 156 with 2 new GOR2016 tests).
- 2026-04-13: Added unused-parameter diagnostics (`GOR2017`) for function parameters that are never referenced.
- 2026-04-13: Added integration tests for `GOR2017` covering unused and used parameter scenarios.
- 2026-04-13: Full test suite: 160 passed (up from 158 with 2 new GOR2017 tests).
- 2026-04-13: Added assignment-target diagnostics (`GOR2018`) for reassignment to variables that were not declared in visible scope.
- 2026-04-13: Added no-op assignment diagnostics (`GOR2019`) for self-assignment and identity arithmetic updates.
- 2026-04-13: Added constant-condition diagnostics (`GOR2020`) for always-true/always-false boolean conditions.
- 2026-04-13: Added redundant-branch diagnostics (`GOR2021`) when `eğer`/`değilse` blocks produce equivalent outcomes.
- 2026-04-13: Added loop-progress mismatch diagnostics (`GOR2022`) for `tekrarla` step direction that likely prevents termination.
- 2026-04-13: Added integration tests covering GOR2018-GOR2022 positive and false-negative scenarios.
- 2026-04-13: Full test suite: 166 passed (up from 160 with 6 new tests).

## Recommended First Implementation Task

Start with generated-C# compiler diagnostics and line mapping.

Why this first:

- It closes the biggest beginner trust gap.
- It improves every existing language feature immediately.
- It does not require grammar expansion.
- It gives the mirror panel and LSP a stronger teaching loop before we add richer import features.

Status: completed on 2026-04-13.

## Recommended Next Task

Advance broader semantic diagnostics on top of normalized morphology and generated-C# compiler feedback.

Why next:

- Morphology normalization now resolves safe, unambiguous suffix forms before mapping diagnostics fire.
- Generated-C# diagnostics are already mapped back to Gör#, so higher-level semantic guidance will reach students at the right source location.
- This increases educational signal quality without expanding grammar surface prematurely.

Status: in progress (five increments completed on 2026-04-13: function-call diagnostics, operand-type diagnostics, typed-function return-path diagnostics, unary operator diagnostics, and semantic quick-fix actions).

## Zemberek-Specific Work

### High-value

- Turn silent fallback in `SuffixResolver` into explicit user-facing diagnostics.
- Extend `HoverHandler` with suffix-aware explanations: stem, case, educational meaning, mapped verbs/properties.
- Extend `CompletionHandler` with case-aware suggestions after apostrophe or suffix context.
- Advance `ZemberekMorphologyNormalizationPass` from candidate-only diagnostics to constrained normalization.

### Lower priority

- Turkish naming-style lint for booleans and common identifier conventions.

### Out of scope for now

- General Turkish NLP intent parsing.
- Broad semantic or stylistic analysis that depends on full morphology being available.

## Relevant Files

- `src/GorSharp.LanguageServer/Services/TranspilationService.cs`
- `src/GorSharp.Transpiler/CSharpEmitter.cs`
- `src/GorSharp.Core/Diagnostics/DiagnosticCodes.cs`
- `src/GorSharp.CLI/CSharpToGorConverter.cs`
- `src/GorSharp.LanguageServer/Handlers/HoverHandler.cs`
- `src/GorSharp.LanguageServer/Handlers/CompletionHandler.cs`
- `src/GorSharp.Morphology/SuffixResolver.cs`
- `src/GorSharp.Morphology/Normalization/ZemberekMorphologyNormalizationPass.cs`
- `dictionaries/sozluk.json`
- `src/GorSharp.Core/Sozluk/SozlukService.cs`

## Working Notes

- Keep NuGet as the normal Zemberek source via `Directory.Build.props`.
- If Gör# needs a new Zemberek capability, publish it first and then update the pinned package version here.
- The repo currently has local uncommitted work in `README.md` and several parser/morphology files; avoid mixing tracker work with those changes unless intentionally continuing them.

## Change Log

- 2026-04-13: Created internal tracker from roadmap discussion.
- 2026-04-13: Added Zemberek-specific scope and explicit exclusions.
- 2026-04-13: Set generated-C# diagnostics and reverse mapping as the recommended first implementation task.
- 2026-04-13: Completed generated-C# diagnostics and reverse mapping implementation.
- 2026-04-13: Completed richer morphology diagnostics and fallback-aware Zemberek error reporting.
- 2026-04-13: Completed the shared Zemberek explanation model for suffix hover and completion.
- 2026-04-13: Completed the Roslyn-based constrained C#→Gör importer skeleton.
- 2026-04-13: Completed the narrative explanation result model for C#→Gör import.
- 2026-04-13: Completed the VS Code import, mirror refresh, and generated-code inspection workflows.
- 2026-04-13: Completed the Visual Studio import, generated-code refresh, and generated-code inspection workflows.
- 2026-04-13: Completed constrained morphology normalization for unambiguous suffix method/property forms.
- 2026-04-13: Started broader semantic diagnostics with `GOR2007` function argument type checks and same-name variable/function guidance.
- 2026-04-13: Extended broader semantic diagnostics with `GOR2008` operand-type checks for logical/arithmetic/comparison expressions.
- 2026-04-13: Extended broader semantic diagnostics with `GOR2009` typed-function return-path completeness checks.
- 2026-04-13: Extended broader semantic diagnostics with `GOR2010` unary operand-type checks for `değil` and unary `-`.
- 2026-04-13: Extended code actions to surface quick-fix guidance for `GOR2007` and `GOR2008`, and safe unary operator removal for `GOR2010`.
- 2026-04-13: Added explicit `CodeActionHandler` regression tests for semantic quick-fixes.
- 2026-04-13: Completed Turkish UX consistency for legacy quick-fix titles (`GOR2001`-`GOR2003`) with direct regression tests.