---
name: transpilation
description: How the Gör# AST-to-C# code generation pipeline works, including source mapping and operator translation
---

# Transpilation Skill — Gör#

## Pipeline Overview

```
.gör source
    → [ANTLR Lexer]    tokens
    → [ANTLR Parser]   parse tree
    → [AstVisitor]     AST (GorSharp.Core.Ast nodes)
    → [CodeGenerator]  C# source string + source comments
    → [SourceMapper]   line-level .gör ↔ .cs mapping
    → .cs output file
```

## CodeGenerator (Visitor Pattern)

**Location**: `src/GorSharp.Transpiler/CodeGenerator.cs`

Implements `IAstVisitor<string>` — each `Visit` method returns a C# code fragment.

```csharp
public class CodeGenerator : IAstVisitor<string>
{
    public string Visit(AssignmentNode node)
    {
        // x 5 olsun; → var x = 5; /* gör:1 */
        var type = node.ExplicitType ?? "var";
        return $"{type} {node.Name} = {Visit(node.Value)}; /* gör:{node.Location.Line} */";
    }

    public string Visit(PrintNode node)
    {
        // "Merhaba" yazdır; → Console.Write("Merhaba"); /* gör:2 */
        var method = node.IsWriteLine ? "WriteLine" : "Write";
        return $"Console.{method}({Visit(node.Expression)}); /* gör:{node.Location.Line} */";
    }
}
```

## Source Location Comments

Every significant C# line includes `/* gör:LINE */` at the end:
```csharp
var x = 5; /* gör:1 */
Console.WriteLine(x); /* gör:2 */
if (x > 3) /* gör:3 */
{
    Console.WriteLine("Büyük"); /* gör:4 */
}
```

This enables the mirror panel to draw line-by-line correspondence.

## Operator Translation Table

All loaded from `sozluk.json`, never hardcoded:

| Gör# | C# | Category |
|------|----|----------|
| `ve` | `&&` | Logical |
| `veya` | `\|\|` | Logical |
| `değil` | `!` | Logical (unary) |
| `eşittir` | `==` | Comparison |
| `eşitDeğildir` | `!=` | Comparison |
| `büyüktür` | `>` | Comparison |
| `küçüktür` | `<` | Comparison |
| `büyükEşittir` | `>=` | Comparison |
| `küçükEşittir` | `<=` | Comparison |
| `arttır` | `+=` | Compound (SOV: `x 5 arttır;` → `x += 5;`) |
| `azalt` | `-=` | Compound |
| `çarp` | `*=` | Compound |
| `böl` | `/=` | Compound |
| `artsın` | `++` | Unary (SOV: `x artsın;` → `x++;`) |
| `azalsın` | `--` | Unary |

## Type Inference

```
Literal          → C# Type
─────────────────────────
5, 42, -1        → int
3.14, 0.5        → double
"Ali", "Merhaba" → string
doğru, yanlış    → bool
boş              → null (contextual)
```

## Dual Function Call Handling

Both syntaxes produce identical C#:

```
// SVO (preferred):
sonuç = topla(3, 5);       → var sonuç = topla(3, 5);

// SOV (alternative):
sonuç = 3 ile 5 topla;     → var sonuç = topla(3, 5);
```

The AST normalizes both into a single `FunctionCallNode`:
```csharp
// Both produce:
new FunctionCallNode("topla", new[] {
    new LiteralNode(3),
    new LiteralNode(5)
})
```

## `sonra` Chain Flattening

```
liste'ye 10 ekle sonra 20 ekle sonra yeniSatıraYazdır;
```

Becomes sequential statements sharing the same target:
```csharp
liste.Add(10); /* gör:1 */
liste.Add(20); /* gör:1 */
Console.WriteLine(liste); /* gör:1 */
```

Note: All statements reference the same `.gör` line — the mirror panel groups them.

## Entry Point Handling

```csharp
// Top-level statements (default) — modern C# supports this directly:
var x = 5;
Console.WriteLine(x);

// başla block — wrap in Main:
class Program
{
    static void Main(string[] args)
    {
        var x = 5;
        Console.WriteLine(x);
    }
}
```

## SourceMapper Output

```json
{
  "version": 1,
  "file": "program.gör",
  "mappings": [
    { "gorLine": 1, "csLine": 1, "gorCode": "x 5 olsun;", "csCode": "var x = 5;" },
    { "gorLine": 2, "csLine": 2, "gorCode": "x yeniSatıraYazdır;", "csCode": "Console.WriteLine(x);" }
  ]
}
```

## Generated Code Quality Rules

1. **Idiomatic C#**: Use `var` for inferred types, proper indentation, modern C# patterns
2. **Readable**: The generated code should be something a student can read and learn from
3. **Compilable**: Every generated `.cs` file must compile with `dotnet build` without errors
4. **Consistent**: Same Gör# input always produces the same C# output (deterministic)
