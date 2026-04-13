# IDE Support: Gör# vs C# — Feature Comparison

**Last Updated:** April 11, 2026 | **Target Environments:** Visual Studio 2022+, VS Code 1.60+

---

## Executive Summary

Roadmap notu: Özellik geliştirme ve yayınlama sırası Visual Studio-first yaklaşımıyla yürütülür. VS Code tarafı, Visual Studio'da kararlılaştırılan eğitim deneyiminin eşlenmiş sürümü olarak ilerler.

| Category | C# | Gör# | Gap |
|----------|-----|------|-----|
| **Syntax Highlighting** | ✅ Full | ✅ Full (semantic tokens + grammar) | None |
| **IntelliSense / Completion** | ✅ Advanced | ✅ Scope-aware + inferred type detail | Minor polish only |
| **Go to Definition (F12)** | ✅ All symbols | ✅ User functions/vars | None |
| **Find All References** | ✅ All symbols | ✅ User functions/vars | None |
| **Rename (F2)** | ✅ All + validation | ✅ All + keyword guard | None |
| **Signature Help** | ✅ User + Framework | ✅ User + 35 built-ins | Dynamic .NET metadata missing |
| **Hover Documentation** | ✅ Full | ✅ Keywords + user symbols + inferred types | Framework docs incomplete |
| **Document Outline** | ✅ Rich | ✅ Functions, parameters, variables, type detail | Deep structural nesting limited |
| **Quick Fixes / Code Actions** | ✅ Extensive | ✅ 3 semantic quick fixes | Coverage limited |
| **Semantic Diagnostics** | ✅ Rich (100+) | ⚠️ Syntax + 6 semantic categories | Type system still shallow |
| **Code Lens** | ✅ References, tests | ✅ Function reference counts | Test/runner lenses missing |
| **Format Document** | ✅ Built-in | ✅ Full-document formatter | Formatting rules still basic |
| **Call Hierarchy** | ✅ Full | ✅ Incoming/outgoing for user functions | Built-ins/framework calls limited |
| **Type Inference Hints** | ✅ Inline | ❌ Not implemented | Missing |
| **Extract Refactoring** | ✅ Methods/variables | ❌ Not implemented | Missing |

---

## Executive Read

Gör# is no longer at the earlier "basic navigation only" stage. The language server now implements the core editing loop expected from a modern language: completion, hover, definition, references, rename, signature help, document symbols, code actions, semantic tokens, diagnostics, formatting, code lens, and call hierarchy.

The real remaining gaps are no longer the basics. They are now mostly in three areas: richer type checking, inline type hints, and advanced refactorings.

This document focuses on currently verified editor features. Planned language features should not be inferred from IDE support tables unless they are also confirmed in the language implementation.

---

## Implemented Features (16/20+)

### ✅ Core Navigation & Editing

| Feature | Status | Details |
|---------|--------|---------|
| **Syntax Highlighting** | ✅ Full | TextMate grammar + semantic tokens for keyword, type, function, variable, parameter, string, number, operator, comment, enum member |
| **Go to Definition (F12)** | ✅ Full | Works for user-defined functions and variables |
| **Find All References (Ctrl+F12)** | ✅ Full | Shows declaration + reference sites for user-defined symbols |
| **Rename (F2)** | ✅ Full | WorkspaceEdit-based rename with reserved-keyword protection |
| **Prepare Rename** | ✅ Full | Prevents invalid rename targets before rename starts |

### ✅ IntelliSense & Type Help

| Feature | Status | Details |
|---------|--------|---------|
| **IntelliSense / Autocomplete** | ✅ Scope-aware | Filters by visibility + declaration order; includes inferred type detail for variables/parameters |
| **Signature Help** | ✅ Dual-mode | Supports user-defined functions and built-ins from `sozluk.json` |
| **Built-in Signatures** | ✅ 35 functions | Educational built-ins now exceed the earlier 13-function subset |
| **Hover Documentation** | ✅ Full for current model | Keywords/types/operators/literals + user-defined variables/functions with inferred types and function signatures |
| **Completion Detail** | ✅ Improved | Function return-type detail and variable inferred-type detail shown in completion entries |

### ✅ Diagnostics & Quick Fixes

| Feature | Status | Details |
|---------|--------|---------|
| **Parse / Transpile Diagnostics** | ✅ Full baseline | Syntax and transpilation diagnostics still flow through the server |
| **Semantic Diagnostics** | ✅ 6 categories | Undefined symbol, duplicate declaration, arity mismatch, assignment/declaration type mismatch, non-boolean condition, return-type mismatch |
| **Code Actions** | ✅ 3 semantic fixes | Create variable, rename suggestion, add/remove arguments |
| **Diagnostic Tooltips** | ✅ Full | Turkish messages surfaced in editor and Problems/Error List |

### ✅ Structure & Productivity

| Feature | Status | Details |
|---------|--------|---------|
| **Document Symbols / Outline** | ✅ Improved | Functions, parameters, and variable declarations with explicit or inferred type detail |
| **Breadcrumb Navigation** | ✅ Basic | Works through document symbols |
| **Code Lens** | ✅ Implemented | Shows `X referans` above function definitions |
| **Format Document** | ✅ Implemented | Full-document formatting via AST-backed formatter |
| **Call Hierarchy** | ✅ Implemented | Prepare/incoming/outgoing calls for user-defined functions |

---

## Remaining Gaps

### ⚠️ Inline Type Hints / Inlay Hints

**Status:** Not implemented  
**Current:** Inferred types appear in hover, completion, and outline  
**Missing:** Inline editor hints such as `x: sayı` rendered directly in code  
**Impact:** Medium. This is the most visible parity gap still left.

### ⚠️ Framework / Metadata Signature Help

**Status:** Partial  
**Current:** 35 educational built-ins are documented in `sozluk.json`  
**Missing:** Dynamic signature/hover data for broader .NET APIs and generic framework types  
**Impact:** Medium. Fine for education, not C#-level completeness.

### ⚠️ Rich Semantic Analysis

**Status:** Partial  
**Current:** Symbol existence, duplicates, and arity are checked  
**Missing:** Deep type mismatch analysis, control-flow diagnostics, missing-return analysis, data-flow checks  
**Impact:** High if the goal is true C#-like semantic parity.

### ⚠️ Advanced Refactoring

**Status:** Not implemented  
**Missing:** Extract variable, extract function, inline symbol, change signature  
**Impact:** Medium. Important for parity, not essential for beginner learning.

### ⚠️ Formatting Depth

**Status:** Implemented but basic  
**Current:** Full-document formatting works  
**Missing:** C#-level style configurability and edge-case formatting sophistication  
**Impact:** Low to medium.

---

## By IDE: VS Code vs Visual Studio

### VS Code Support (via LSP)

| Feature | Gör# | Notes |
|---------|------|-------|
| Syntax highlighting | ✅ | TextMate grammar + semantic tokens |
| IntelliSense | ✅ | Scope-aware completion |
| Hover | ✅ | Keywords and user symbols with inferred type info |
| F12 Go to Definition | ✅ | Works within `.gör` files |
| Ctrl+F12 Find References | ✅ | Supported via LSP references |
| F2 Rename | ✅ | Supported via LSP rename |
| Signature Help | ✅ | User-defined + built-in signatures |
| Diagnostics | ✅ | Squiggles + Problems panel |
| Quick Fixes | ✅ | Lightbulb actions for semantic diagnostics |
| Outline | ✅ | Functions, params, declarations |
| Code Lens | ✅ | Function reference counts |
| Formatting | ✅ | Full-document format supported |
| Call Hierarchy | ✅ | User-function incoming/outgoing calls |
| Inline type hints | ❌ | Not implemented |

### Visual Studio Support (via LSP + VSIX)

| Feature | Gör# | Notes |
|---------|------|-------|
| Syntax highlighting | ✅ | Same language-server semantic model |
| IntelliSense | ✅ | Completion and signature help available |
| Hover | ✅ | Same hover model as VS Code |
| F12 Go to Definition | ✅ | Supported |
| Ctrl+F12 Find All References | ✅ | Supported |
| F2 Rename | ✅ | Supported |
| Signature Help | ✅ | Supported |
| Diagnostics | ✅ | Error List + editor squiggles |
| Quick Fixes | ✅ | Supported via code actions |
| Outline | ✅ | Document symbols available |
| Code Lens | ✅ | Registered in the language server |
| Formatting | ✅ | Document formatting handler registered |
| Call Hierarchy | ✅ | Registered in the language server |
| Inline type hints | ❌ | Not implemented |

---

## Educational Relevance Mapping

### Tier 1: Essential for Learning (All ✅)
- **Syntax highlighting** — students see the language structure immediately.
- **Definition/references/rename** — supports understanding symbol flow.
- **Hover docs** — explains both Turkish language constructs and user-defined code.
- **Completion/signature help** — reduces syntax friction while preserving learning value.
- **Diagnostics** — catches mistakes early.

### Tier 2: Strong Productivity Features (Mostly ✅)
- **Code actions** — common semantic mistakes can be corrected quickly.
- **Formatting** — reduces manual cleanup burden.
- **Code lens** — reference counts make code navigation easier.
- **Call hierarchy** — useful once students start writing multiple functions.

### Tier 3: Nice-to-Have Polish (Partial)
- **Inline type hints** — still missing.
- **Richer .NET metadata docs** — still limited to educational built-ins.
- **Advanced style formatting** — basic formatter exists, but not deeply configurable.

### Tier 4: Advanced Professional Tooling (Still Missing)
- **Extract refactoring**
- **Inline refactoring**
- **Change signature**
- **Deep type/data-flow analysis**

---

## Completed Handlers (15 total)

```text
✅ TextDocumentSyncHandler       — document open/change/save + push diagnostics
✅ CompletionHandler             — scope-aware completion
✅ HoverHandler                  — keywords + user symbols + inferred types
✅ DocumentFormattingHandler     — full-document formatting
✅ DiagnosticHandler             — pull diagnostics
✅ DefinitionHandler             — go to definition
✅ ReferencesHandler             — find references
✅ RenameHandler                 — coordinated rename
✅ PrepareRenameHandler          — rename validation
✅ SignatureHelpHandler          — function signatures
✅ DocumentSymbolHandler         — outline and breadcrumbs
✅ CallHierarchyHandler          — incoming/outgoing call hierarchy
✅ CodeLensHandler               — function reference counts
✅ CodeActionHandler             — quick fixes
✅ SemanticTokensHandler         — advanced syntax coloring
```

---

## Diagnostic Coverage

### Implemented Semantic Categories
- **GOR2001**: Undefined symbol
- **GOR2002**: Duplicate declaration
- **GOR2003**: Function arity mismatch
- **GOR2004**: Declaration/assignment type mismatch
- **GOR2005**: Non-boolean condition expression
- **GOR2006**: Return-type mismatch

### Still Missing vs C# Baseline
- **Type mismatch diagnostics**
- **Unreachable code analysis**
- **Uninitialized variable analysis**
- **Missing return path analysis**
- **Operator/type compatibility checks**
- **Data-flow driven warnings**

---

## Verified Current State

- `src/GorSharp.LanguageServer/GorSharp.LanguageServer.csproj` builds successfully on April 11, 2026.
- `Program.cs` currently registers formatting, code lens, and call hierarchy handlers.
- `HoverHandler` now returns inferred-type and function-signature hover for user-defined symbols.
- `sozluk.json` currently contains **35 built-in signature entries**.

---

## Recommendation: Next Phase Priorities

### Release Strategy
1. **Visual Studio first**: İlk üretim kalitesi hedefi Visual Studio + VSIX akışının tamamlanması.
2. **VS Code parity second**: Aynı özelliklerin VS Code uzantısına davranış eşdeğerliği ile taşınması.
3. **Shared documentation**: İki IDE için tek bir davranış sözleşmesi ve örnek seti.

### Phase 1: Highest Value
1. **Inline type hints**
2. **Richer semantic/type diagnostics**
3. **Broader built-in/framework signature coverage**

### Phase 2: Strong Parity Features
4. **Extract variable**
5. **Extract function**
6. **Change signature**

### Phase 3: Polish
7. **Formatting configuration**
8. **Richer call hierarchy for built-ins / chains**
9. **More code actions beyond the current 3 semantic fixes**

---

## Summary Table: "How close is Gör# to C# now?"

| Dimension | C# | Gör# | Parity |
|-----------|-----|------|--------|
| Navigation | ✅✅✅ | ✅✅✅ | **100%** |
| Editing workflow | ✅✅✅ | ✅✅ | **67%** |
| IntelliSense & hover | ✅✅✅ | ✅✅ | **67%** |
| Formatting & code insight | ✅✅✅ | ✅✅ | **67%** |
| Semantic analysis | ✅✅✅ | ✅ | **33%** |
| Refactoring | ✅✅✅ | ❌ | **0%** |
| **Overall** | — | — | **~75–80%** |

---

**Bottom line:** Gör# now has solid parity with C# on the core day-to-day IDE loop: navigate, inspect, complete, rename, format, and understand function relationships. The remaining gap is no longer about basic editor support; it is about deeper semantics, inline hints, and professional-grade refactoring.
