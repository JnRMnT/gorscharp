grammar GorSharp;

// ─── Parser Rules ─────────────────────────────────────────────────

program
    : statement* EOF
    ;

statement
    : assignmentStatement
    | printStatement
    | suffixMethodStatement
    | ifStatement
    | whileStatement
    | forStatement
    | functionDefinition
    | returnStatement
    | breakStatement
    | continueStatement
    | expressionStatement
    | block
    ;

// ── Assignment ──────────────────────────────────────────────────
assignmentStatement
    : IDENTIFIER COLON typeName declarationParticle? expression OLSUN SEMI               #TypedDeclaration
    | IDENTIFIER declarationParticle? expression OLSUN SEMI                               #InferredDeclaration
    | IDENTIFIER ASSIGN expression SEMI                             #EqualsAssignment
    ;

typeName
    : SAYI | METIN | MANTIK | ONDALIK | KARAKTER
    ;

// ── Print (SOV) ────────────────────────────────────────────────
// Operand can be followed by optional suffix: "Geçti"yi yeniSatıraYazdır;
printStatement
    : expression IDENTIFIER? YAZDIR SEMI                            #WriteStatement
    | expression IDENTIFIER? YENISATIRA_YAZDIR SEMI                 #WriteLineStatement
    ;

// ── Suffix-Based Natural Method Call ───────────────────────────
// liste'ye 10 ekle;
suffixMethodStatement
    : IDENTIFIER expression IDENTIFIER (SONRA expression IDENTIFIER)* (SONRA IDENTIFIER (YAZDIR | YENISATIRA_YAZDIR))? SEMI
    ;

// ── If / Else If / Else ────────────────────────────────────────
// eğer x büyüktür 5 { ... } yoksa eğer x eşittir 5 { ... } değilse { ... }
ifStatement
    : EGER expression conditionParticle? block (YOKSA_EGER expression conditionParticle? block)* (DEGILSE block)?
    ;

// ── While Loop ─────────────────────────────────────────────────
// döngü x büyüktür 0 { ... }
whileStatement
    : DONGU expression loopParticle? block
    ;

conditionParticle
    : ISE
    | OLURSA
    | MI
    | IKEN
    | SAYET
    ;

loopParticle
    : IKEN
    | BOYUNCA
    | SURECE
    ;

declarationParticle
    : DEGISKENI
    | OLARAK
    ;

// ── For Loop ───────────────────────────────────────────────────
// tekrarla (i 0 olsun; i küçüktür 10; i = i + 1) { ... }
forStatement
    : TEKRARLA LPAREN forInit? SEMI expression? SEMI forUpdate? RPAREN block
    ;

forInit
    : IDENTIFIER COLON typeName declarationParticle? expression OLSUN                    #ForTypedInit
    | IDENTIFIER declarationParticle? expression OLSUN                                   #ForInferredInit
    | IDENTIFIER ASSIGN expression                                  #ForEqualsInit
    ;

forUpdate
    : IDENTIFIER ASSIGN expression
    ;

// ── Function Definition ────────────────────────────────────────
// fonksiyon topla(a: sayı, b: sayı): sayı { döndür a + b; }
functionDefinition
    : FONKSIYON IDENTIFIER LPAREN paramList? RPAREN (COLON typeName)? block
    ;

paramList
    : param (COMMA param)*
    ;

param
    : IDENTIFIER COLON typeName
    ;

// ── Return ─────────────────────────────────────────────────────
returnStatement
    : DONDUR expression? SEMI
    ;

// ── Break / Continue ───────────────────────────────────────────
breakStatement
    : KIR SEMI
    ;

continueStatement
    : DEVAM SEMI
    ;

// ── Expression Statement ───────────────────────────────────────
expressionStatement
    : expression SEMI
    ;

// ── Block ──────────────────────────────────────────────────────
block
    : LBRACE statement* RBRACE
    ;

// ── Expressions (precedence climbing) ──────────────────────────
expression
    : orExpression
    ;

orExpression
    : andExpression ((VEYA | YA_DA) andExpression)*
    ;

andExpression
    : norExpression ((VE | HEM_DE) norExpression)*
    ;

norExpression
    : NE equalityExpression (NE | NE_DE) equalityExpression          #NeitherNorExpression
    | equalityExpression                                             #NorPrimaryExpression
    ;

equalityExpression
    : comparisonExpression ((ESITTIR | ESIT_DEGILDIR) comparisonExpression)*
    ;

comparisonExpression
    : additiveExpression ((BUYUKTUR | KUCUKTUR | BUYUK_ESITTIR | KUCUK_ESITTIR) additiveExpression)* #StandardComparisonExpr
    | additiveExpression ABLATIVE_NUMBER (BUYUK | KUCUK) (VEYA ESIT)? conditionParticle?              #AblativeComparisonExpr
    ;

additiveExpression
    : multiplicativeExpression ((PLUS | MINUS) multiplicativeExpression)*
    ;

multiplicativeExpression
    : unaryExpression ((STAR | SLASH | PERCENT) unaryExpression)*
    ;

unaryExpression
    : DEGIL unaryExpression                                         #NotExpression
    | MINUS unaryExpression                                         #NegateExpression
    | primary                                                       #PrimaryExpression
    ;

primary
    : INTEGER_LITERAL                                               #IntLiteralExpr
    | DOUBLE_LITERAL                                                #DoubleLiteralExpr
    | STRING_LITERAL                                                #StringLiteralExpr
    | DOGRU                                                         #TrueLiteralExpr
    | YANLIS                                                        #FalseLiteralExpr
    | EVET                                                          #TrueAliasLiteralExpr
    | HAYIR                                                         #FalseAliasLiteralExpr
    | BOS                                                           #NullLiteralExpr
    | IDENTIFIER LPAREN argList? RPAREN                             #FunctionCallExpr
    | IDENTIFIER IDENTIFIER                                          #SuffixPropertyExpr
    | IDENTIFIER                                                    #IdentifierExpr
    | LPAREN expression RPAREN                                      #ParenExpr
    ;

argList
    : expression (COMMA expression)*
    ;

// ─── Lexer Rules ──────────────────────────────────────────────────

// ── Keywords (Must come before IDENTIFIER) ─────────────────────
OLSUN               : 'olsun';
YAZDIR               : 'yazdır' | 'yazd\u0131r';
YENISATIRA_YAZDIR    : 'yeniSatıraYazdır' | 'yeniSat\u0131raYazd\u0131r';
EGER                 : 'eğer' | 'e\u011fer';
YOKSA_EGER           : 'yoksa eğer' | 'yoksa e\u011fer';
DEGILSE              : 'değilse' | 'de\u011filse';
DONGU                : 'döngü' | 'd\u00f6ng\u00fc';
TEKRARLA             : 'tekrarla';
HER                  : 'her';
FONKSIYON            : 'fonksiyon';
DONDUR               : 'döndür' | 'd\u00f6nd\u00fcr';
DENE                 : 'dene';
HATA_VARSA           : 'hata_varsa';
SONUNDA              : 'sonunda';
SINIF                : 'sınıf' | 's\u0131n\u0131f';
KURUCU               : 'kurucu';
BU                   : 'bu';
MIRAS                : 'miras';
BASLA                : 'başla' | 'ba\u015fla';
OKU                  : 'oku';
OKU_SATIR            : 'okuSatır' | 'okuSat\u0131r';
KIR                  : 'kır' | 'k\u0131r';
DEVAM                : 'devam';
SONRA                : 'sonra';
ISE                  : 'ise';
OLURSA               : 'olursa';
IKEN                 : 'iken';
MI                   : 'mı' | 'mi' | 'mu' | 'mü' | 'm\u0131' | 'm\u00fc';
SAYET                : 'şayet' | 's\u015fayet';
BOYUNCA              : 'boyunca';
SURECE               : 'sürece' | 's\u00fcrece';
DEGISKENI            : 'değişkeni' | 'de\u011fi\u015fkeni';
OLARAK               : 'olarak';

// ── Turkish Literals ───────────────────────────────────────────
DOGRU                : 'doğru' | 'do\u011fru';
YANLIS               : 'yanlış' | 'yanl\u0131\u015f';
EVET                 : 'evet';
HAYIR                : 'hayır' | 'hay\u0131r';
BOS                  : 'boş' | 'bo\u015f';

// ── Types ──────────────────────────────────────────────────────
SAYI                 : 'sayı' | 'say\u0131';
METIN                : 'metin';
MANTIK               : 'mantık' | 'mant\u0131k';
ONDALIK              : 'ondalık' | 'ondal\u0131k';
KARAKTER             : 'karakter';

// ── Turkish Operators ──────────────────────────────────────────
VE                   : 've';
VEYA                 : 'veya';
YA_DA                : 'ya da';
HEM_DE               : 'hem de';
NE_DE                : 'ne de';
NE                   : 'ne';
DEGIL                : 'değil' | 'de\u011fil';
ESITTIR              : 'eşittir' | 'e\u015fittir';
ESIT_DEGILDIR        : 'eşitDeğildir' | 'e\u015fitDe\u011fildir';
BUYUKTUR             : 'büyüktür' | 'b\u00fcy\u00fckt\u00fcr';
KUCUKTUR             : 'küçüktür' | 'k\u00fc\u00e7\u00fckt\u00fcr';
BUYUK_ESITTIR        : 'büyükEşittir' | 'b\u00fcy\u00fckE\u015fittir';
KUCUK_ESITTIR        : 'küçükEşittir' | 'k\u00fc\u00e7\u00fckE\u015fittir';
BUYUK                : 'büyük' | 'b\u00fcy\u00fck';
KUCUK                : 'küçük' | 'k\u00fc\u00e7\u00fck';
ESIT                 : 'eşit' | 'e\u015fit';

// ── Symbols ────────────────────────────────────────────────────
ASSIGN               : '=';
PLUS                 : '+';
MINUS                : '-';
STAR                 : '*';
SLASH                : '/';
PERCENT              : '%';
LPAREN               : '(';
RPAREN               : ')';
LBRACE               : '{';
RBRACE               : '}';
SEMI                 : ';';
COLON                : ':';
COMMA                : ',';
DOT                  : '.';

// ── Literals ───────────────────────────────────────────────────
INTEGER_LITERAL      : [0-9]+;
DOUBLE_LITERAL       : [0-9]+ '.' [0-9]+;
STRING_LITERAL       : '"' (~["\\\r\n] | '\\' .)* '"';
ABLATIVE_NUMBER      : [0-9]+ '\'' ('d' [ae] 'n' | 't' [ae] 'n');

// ── Identifier (Turkish character support) ─────────────────────
// Covers standard Latin + Turkish special chars: çÇğĞıİöÖşŞüÜ
IDENTIFIER
    : [a-zA-Z_\u00C7\u00E7\u011E\u011F\u0130\u0131\u00D6\u00F6\u015E\u015F\u00DC\u00FC]
            [a-zA-Z0-9_'\u2019\u00C7\u00E7\u011E\u011F\u0130\u0131\u00D6\u00F6\u015E\u015F\u00DC\u00FC]*
    ;

// ── Whitespace & Comments ──────────────────────────────────────
WS                   : [ \t\r\n]+ -> skip;
LINE_COMMENT         : '//' ~[\r\n]* -> skip;
BLOCK_COMMENT        : '/*' .*? '*/' -> skip;
