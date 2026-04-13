---
name: suffix-resolution
description: How Turkish suffix resolution works in Gör# using ZemberekDotNet morphology analysis
---

# Suffix Resolution Skill — Gör#

## Architecture

```
Gör# Source / parser helper
    │
    ▼
[SuffixResolver] Uses ZemberekDotNet morphology to detect Turkish case markers
    │
    ├── DetectCase(word)               → dative / ablative / genitive / locative / accusative
    ├── Resolve(suffixedWord, verb)    → stem + case + mapped C# method
    └── DetectAblativeNumber(token)    → validates tokens like 90'dan for natural comparisons
```

## SuffixResolver Class

**Location**: `src/GorSharp.Morphology/SuffixResolver.cs`

```csharp
public class SuffixResolver
{
    public string? DetectCase(string word)
    {
        var normalized = word.Replace("'", "").Replace("\u2019", "");
        var analysis = _morphology.Analyze(normalized);

        if (analysis.AnalysisCount() > 0)
        {
            foreach (var result in analysis)
            {
                if (result.ContainsMorpheme(TurkishMorphotactics.dat)) return "dative";
                if (result.ContainsMorpheme(TurkishMorphotactics.abl)) return "ablative";
                if (result.ContainsMorpheme(TurkishMorphotactics.gen)) return "genitive";
                if (result.ContainsMorpheme(TurkishMorphotactics.loc)) return "locative";
                if (result.ContainsMorpheme(TurkishMorphotactics.acc)) return "accusative";
            }
        }

        return null;
    }
}
```

## Current Scope

- `SuffixResolver` is implemented and used by Gör# for morphology-driven suffix/case analysis.
- Natural comparison validation such as `90'dan büyük` now goes through Zemberek validation at runtime.
- Gör# in this repo uses **NuGet Zemberek packages only**.
- Zemberek resources such as `Resources/tr/lexicon.bin` must be copied from the NuGet package into build output.
- If Zemberek runtime/resources are misconfigured, Gör# should fail with an explicit diagnostic rather than silently falling back.
- Suffix-based method-call syntax like `liste'ye 10 ekle` is still a language-design area; do not assume full parser/codegen support unless verified in current source.

## Turkish Cases → C# Operations

The mapping is defined in `sozluk.json` under the `"suffixes"` section:

| Turkish Case | Suffixes (vowel harmony) | Example | C# Operation |
|-------------|--------------------------|---------|--------------|
| **Dative** (dat) | 'ye, 'ya, 'e, 'a | `liste'ye 10 ekle` | `.Add(10)` |
| **Ablative** (abl) | 'den, 'dan, 'ten, 'tan | `liste'den 10 çıkar` | `.Remove(10)` |
| **Genitive** (gen) | 'nin, 'nın, 'nun, 'nün, 'in, 'ın, 'un, 'ün | `liste'nin uzunluğu` | `.Count` |
| **Accusative** (acc) | 'i, 'ı, 'u, 'ü, 'yi, 'yı, 'yu, 'yü | `dosya'yı aç` | context-dependent |
| **Locative** (loc) | 'de, 'da, 'te, 'ta | `liste'de ara` | `.Contains()` |

## Verb Mapping (with suffix context)

The verb after the suffix determines the specific C# method:

| Turkish Verb | Required Case | C# Method |
|-------------|---------------|-----------|
| `ekle` | Dative | `.Add()` |
| `çıkar` | Ablative | `.Remove()` |
| `ara` | Locative | `.Contains()` / `.IndexOf()` |
| `al` | Ablative | `.Get()` / indexer |
| `sil` | Accusative | `.RemoveAt()` / `.Clear()` |
| `bul` | Locative | `.Find()` |

## Vowel Harmony Rules

Turkish suffixes change based on the last vowel of the base word:

| Last Vowel | Dative | Ablative | Genitive |
|-----------|--------|----------|----------|
| e, i | -ye | -den | -nin |
| a, ı | -ya | -dan | -nın |
| o, u | -ya | -dan | -nun |
| ö, ü | -ye | -den | -nün |

ZemberekDotNet handles these case distinctions. In current Gör# guidance, validation paths should rely on explicit Zemberek runtime checks rather than silent fallback.

## ZemberekDotNet API Quick Reference

```csharp
using ZemberekDotNet.Morphology;
using ZemberekDotNet.Morphology.Morphotactics;
using ZemberekDotNet.Morphology.Analysis;

// Initialize once
var morphology = TurkishMorphology.CreateWithDefaults();

// Analyze
WordAnalysis wa = morphology.Analyze("kitaplara");
SingleAnalysis sa = wa.GetAnalysisResults()[0];

// Check case
sa.ContainsMorpheme(TurkishMorphotactics.dat);  // true for dative
sa.ContainsMorpheme(TurkishMorphotactics.acc);  // true for accusative
sa.ContainsMorpheme(TurkishMorphotactics.gen);  // true for genitive
sa.ContainsMorpheme(TurkishMorphotactics.abl);  // true for ablative

// Debug output
sa.FormatLexical();  // "kitap+Noun+A3pl+Pnon+Dat"
sa.GetStem();        // "kitap"
sa.GetEnding();      // "lara"
```
