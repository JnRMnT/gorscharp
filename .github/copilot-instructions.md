# Gör# — Copilot Instructions

## Project Identity

Gör# (GorSharp) is a **Turkish-to-C# educational transpiler** designed for Turkish university students.
It maps Turkish grammatical structures (SOV) to C# (SVO), removing the English barrier for learning programming.
The tool is **educational first** — every feature must help students understand C# better, not just hide it.

## Repository Layout

```
src/
  GorSharp.Core/           — AST nodes, types, Sözlük loader, diagnostics
  GorSharp.Parser/         — ANTLR-generated lexer/parser + visitor → AST
  GorSharp.Morphology/     — ZemberekDotNet integration, SuffixResolver
  GorSharp.Transpiler/     — AST → C# code generator + source mapper
  GorSharp.CLI/            — Console app (gorsharp transpile/run/diff)
  GorSharp.LanguageServer/ — LSP server (shared by IDE extensions)
  GorSharp.VSCode/         — VS Code extension (TypeScript)
  GorSharp.VisualStudio/   — Visual Studio VSIX extension
grammar/
  GorSharp.g4              — ANTLR4 grammar (the language specification)
dictionaries/
  sozluk.json              — Single source of truth for keyword/type/method mappings + tooltip data
tests/                     — xUnit test projects mirroring src/ structure
samples/                   — Numbered .gör sample files (01-08)
docs/                      — Language spec, getting started, keyword reference
```

## ZemberekDotNet (Sibling Project)

- Location: `C:\Personal\Genel\Projeler\ZemberekDotNet`
- Same owner. MIT licensed. Published on NuGet as independent ZemberekDotNet modules; do not assume a single version across all packages.
- **Gör# policy**: consume Zemberek via NuGet packages in this repo, not local sibling project references.
- Used for Turkish suffix/case detection (dative, accusative, genitive, ablative)
- Key API: `TurkishMorphology.Analyze()` → `SingleAnalysis.ContainsMorpheme(TurkishMorphotactics.dat)`

## Language Rules

### Word Order
- **SOV (Subject-Object-Verb) is primary**: `"Merhaba" yazdır;` not `yazdır("Merhaba")`
- **SVO fallback for user function calls**: `topla(3, 5)` is preferred (educational — closer to C#)
- **SOV alternative for function calls**: `3 ile 5 topla` also valid, both produce identical C#

### Assignment
- `x 5 olsun;` → `var x = 5;` (name-first, type inferred)
- `x: sayı 5 olsun;` → `int x = 5;` (explicit type with colon)
- `x = 10;` → `x = 10;` (equals sign also valid)
- Both `olsun` and `=` accepted everywhere. Tooltips guide: `olsun` for declaration, `=` for reassignment.

### Operators
- **Preferred Turkish forms** in Gör# source: `ve` (&&), `veya` (||), `değil` (!), `eşittir` (==), `büyüktür` (>), `küçüktür` (<), `büyükEşittir` (>=), `küçükEşittir` (<=), `eşitDeğildir` (!=)
- **Natural aliases also supported**: `ya da` (||), `hem de` (&&), `evet` (true), `hayır` (false)
- **Condition particles can be ignored when natural in Turkish**: `ise`, `olursa`, `iken`, `mı/mi/mu/mü`
- **Natural comparison form supported**: `puan 90'dan büyük veya eşit ise`
- Compound operators: both Turkish SOV (`x 5 arttır;` → `x += 5`) and symbols (`x += 5;`) accepted
- Tooltips guide students toward Turkish forms

### Keywords (from sozluk.json)
- `yazdır` = Console.Write, `yeniSatıraYazdır` = Console.WriteLine
- `oku` = Console.Read, `okuSatır` = Console.ReadLine
- `eğer`/`yoksa eğer`/`değilse` = if/else if/else
- `döngü` = while, `tekrarla` = for, `her` = foreach
- `fonksiyon` = method, `döndür` = return
- `dene`/`hata_varsa`/`sonunda` = try/catch/finally
- `sınıf` = class, `kurucu` = constructor, `bu` = this, `miras` = inheritance

### Suffix-Based Method Calls
- `liste'ye 10 ekle;` → `liste.Add(10);` (dative suffix → Add)
- `liste'den 10 çıkar;` → `liste.Remove(10);` (ablative → Remove)
- `liste'nin uzunluğu` → `liste.Count` (genitive → property access)
- `sonra` keyword for chaining: `liste'ye 10 ekle sonra 20 ekle;`

### Entry Point
- Top-level statements by default (transpiler wraps in Main)
- Optional `başla { }` block for explicit entry point

## Coding Conventions

- **Gör# syntax**: Turkish keywords and identifiers
- **C# internals**: English for class names, method names, variable names in the transpiler codebase itself
- **Turkish characters**: `ö`, `ü`, `ş`, `ç`, `ı`, `ğ`, `İ` must work in identifiers and keywords everywhere
- **Error messages**: Always in Turkish
- **sozluk.json**: The single source of truth — never hardcode keyword mappings in code
- **Tests**: Every grammar rule needs a transpile test + compile test. Use xUnit with Theory/InlineData.
- **AST nodes**: Must carry source location (line, column) for the mirror feature
- **Generated C#**: Must include source location comments (`/* gör:LINE */`) for mirror mapping

## File Extension
- Gör# source files: `.gör`
- Generated output: `.cs`
- Source maps: `.gör.map` (future)
