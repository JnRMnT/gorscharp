---
name: antlr-grammar
description: Domain knowledge for working with the GorSharp ANTLR4 grammar, Turkish character support, and SOV/SVO parsing patterns
---

# ANTLR Grammar Skill — Gör#

## Grammar File
- Location: `grammar/GorSharp.g4`
- Generated output: `src/GorSharp.Parser/Generated/`

## NuGet Packages
```xml
<PackageReference Include="Antlr4.Runtime.Standard" Version="4.13.2" />
<PackageReference Include="Antlr4BuildTasks" Version="12.8.0" />
```

## Turkish Character Ranges for Lexer

```antlr
fragment TURKISH_LOWER : [a-zçğıöşü] ;
fragment TURKISH_UPPER : [A-ZÇĞİÖŞÜ] ;
fragment TURKISH_LETTER : TURKISH_LOWER | TURKISH_UPPER ;
fragment DIGIT : [0-9] ;

IDENTIFIER : TURKISH_LETTER (TURKISH_LETTER | DIGIT | '_')* ;
```

Important: Turkish `ı` (dotless i) and `İ` (dotted I) are distinct from ASCII `i` and `I`.

## Keyword Tokens

All keywords come from `dictionaries/sozluk.json`. They must be declared BEFORE the IDENTIFIER rule:

```antlr
// Assignment
OLSUN : 'olsun' ;

// Output (SOV: object comes before verb)
YAZDIR : 'yazdır' ;
YENI_SATIRA_YAZDIR : 'yeniSatıraYazdır' ;
YAZIYA_DONUSTUR : 'yazıyaDönüştür' ;

// Input
OKU : 'oku' ;
OKU_SATIR : 'okuSatır' ;

// Control flow
EGER : 'eğer' ;
YOKSA_EGER : 'yoksa eğer' ;  // two-word keyword — handle in parser, not lexer
DEGILSE : 'değilse' ;

// Loops
DONGU : 'döngü' ;
TEKRARLA : 'tekrarla' ;
HER : 'her' ;

// Functions
FONKSIYON : 'fonksiyon' ;
DONDUR : 'döndür' ;
ILE : 'ile' ;

// Error handling
DENE : 'dene' ;
HATA_VARSA : 'hata_varsa' ;
SONUNDA : 'sonunda' ;

// OOP
SINIF : 'sınıf' ;
KURUCU : 'kurucu' ;
BU : 'bu' ;
MIRAS : 'miras' ;

// Logical operators (Turkish only)
VE : 've' ;
VEYA : 'veya' ;
DEGIL : 'değil' ;
ESITTIR : 'eşittir' ;
ESIT_DEGILDIR : 'eşitDeğildir' ;
BUYUKTUR : 'büyüktür' ;
KUCUKTUR : 'küçüktür' ;
BUYUK_ESITTIR : 'büyükEşittir' ;
KUCUK_ESITTIR : 'küçükEşittir' ;

// Compound operators
ARTTIR : 'arttır' ;
AZALT : 'azalt' ;
CARP : 'çarp' ;
BOL : 'böl' ;
ARTSIN : 'artsın' ;
AZALSIN : 'azalsın' ;

// Boolean literals
DOGRU : 'doğru' ;
YANLIS : 'yanlış' ;

// Chaining
SONRA : 'sonra' ;

// Entry point
BASLA : 'başla' ;

// Flow control
KIR : 'kır' ;
DEVAM : 'devam' ;

// Misc
BOS : 'boş' ;
```

## SOV Pattern Rules

```antlr
// The fundamental Gör# patterns:

// Assignment: name [: type] value olsun;
assignmentStatement
    : IDENTIFIER (COLON typeAnnotation)? expression OLSUN SEMI
    | IDENTIFIER ASSIGN expression SEMI
    ;

// Print (SOV): object verb;
printStatement
    : expression YAZDIR SEMI
    | expression YENI_SATIRA_YAZDIR SEMI
    ;

// Read (SOV): target verb;
readStatement
    : IDENTIFIER OKU SEMI
    | IDENTIFIER OKU_SATIR SEMI
    ;

// Function call (SVO): name(args)
svoCall : IDENTIFIER LPAREN argumentList? RPAREN ;

// Function call (SOV): arg1 ile arg2 name
sovCall : expression (ILE expression)+ IDENTIFIER ;

// Suffix method (SOV): target'suffix [arg] verb
suffixMethodCall
    : suffixExpression expression? IDENTIFIER SEMI
    ;

// Chaining: operation sonra operation sonra ...
chainedExpression
    : suffixMethodCall (SONRA suffixMethodCall)*
    ;
```

## Suffix Token Handling

The apostrophe in `liste'ye` needs special lexer treatment:

```antlr
// Option A: Single compound token
SUFFIX_EXPR : IDENTIFIER APOSTROPHE TURKISH_SUFFIX ;
fragment TURKISH_SUFFIX : [a-zçğıöşüA-ZÇĞİÖŞÜ]+ ;

// Option B: Separate tokens (parser combines)
APOSTROPHE : '\'' ;
// Parser rule:
suffixExpression : IDENTIFIER APOSTROPHE IDENTIFIER ;
```

Option B is recommended — it gives the parser more flexibility and the `SuffixResolver` can analyze the suffix part independently.

## Grammar Debugging

```bash
# Build and test grammar
cd src/GorSharp.Parser
dotnet build

# Run ANTLR test rig (if antlr4 CLI installed)
antlr4 -Dlanguage=CSharp grammar/GorSharp.g4
```

## Common Pitfalls

1. **Keyword vs identifier conflict**: Keywords must be lexer rules declared BEFORE `IDENTIFIER`
2. **`yoksa eğer`**: This is two tokens — handle in parser as `YOKSA EGER` not as a single lexer token
3. **Turkish `İ`/`i` case sensitivity**: ANTLR is case-sensitive. `İşlem` ≠ `işlem`
4. **Semicolons**: Every statement needs `SEMI` at the end. Block statements (`eğer`, `döngü`) don't need semicolons after `}`
