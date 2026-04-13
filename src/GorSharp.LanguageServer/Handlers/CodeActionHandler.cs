using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using GorSharp.LanguageServer.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GorSharp.LanguageServer.Handlers;

public class CodeActionHandler : ICodeActionHandler
{
    private readonly DocumentStore _documentStore;
    private readonly TranspilationService _transpilationService;
    private readonly SymbolAnalysisService _symbolAnalysis;

    public CodeActionHandler(DocumentStore documentStore, TranspilationService transpilationService, SymbolAnalysisService symbolAnalysis)
    {
        _documentStore = documentStore;
        _transpilationService = transpilationService;
        _symbolAnalysis = symbolAnalysis;
    }

    public Task<CommandOrCodeActionContainer?> Handle(CodeActionParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri;
        var content = _documentStore.Get(uri.ToString());
        if (content?.Text == null) return Task.FromResult<CommandOrCodeActionContainer?>(null);

        var filePath = uri.GetFileSystemPath() ?? uri.ToString();
        var result = _transpilationService.Transpile(content.Text, filePath);
        if (result.Ast == null) return Task.FromResult<CommandOrCodeActionContainer?>(null);

        var actions = new List<CommandOrCodeAction>();
        var rawDiagnostics = result.Diagnostics;
        if (rawDiagnostics == null || rawDiagnostics.Count == 0) 
            return Task.FromResult<CommandOrCodeActionContainer?>(new CommandOrCodeActionContainer(actions));

        // Convert raw diagnostics to LSP Diagnostics and filter by range
        var diagnosticsInRange = new List<(Core.Diagnostics.Diagnostic raw, Diagnostic lsp)>();
        foreach (var rawDiag in rawDiagnostics)
        {
            var lspRange = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(rawDiag.Line - 1, rawDiag.Column),
                new Position(rawDiag.Line - 1, rawDiag.Column + Math.Max(1, rawDiag.Message.Length)));
            
            // Check if diagnostic overlaps with request range
            if (!(lspRange.End.Line < request.Range.Start.Line || lspRange.Start.Line > request.Range.End.Line))
            {
                var lspDiag = new Diagnostic
                {
                    Range = lspRange,
                    Severity = rawDiag.Severity switch
                    {
                        Core.Diagnostics.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
                        Core.Diagnostics.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                        _ => DiagnosticSeverity.Information
                    },
                    Code = rawDiag.Code,
                    Message = rawDiag.Message
                };
                diagnosticsInRange.Add((rawDiag, lspDiag));
            }
        }

        foreach (var (rawDiag, lspDiag) in diagnosticsInRange)
        {
            string? code = rawDiag.Code;

            if (code == "GOR2001") // Undefined symbol
            {
                var action = CreateGOR2001Action(lspDiag, content.Text, uri);
                if (action != null) actions.Add(action);
            }
            else if (code == "GOR2002") // Duplicate declaration
            {
                var action = CreateGOR2002Action(lspDiag, content.Text, uri);
                if (action != null) actions.Add(action);
            }
            else if (code == "GOR2003") // Arity mismatch
            {
                var action = CreateGOR2003Action(lspDiag, content.Text, uri);
                if (action != null) actions.Add(action);
            }
        }

        return Task.FromResult<CommandOrCodeActionContainer?>(new CommandOrCodeActionContainer(actions));
    }

    private CommandOrCodeAction? CreateGOR2001Action(Diagnostic diagnostic, string content, DocumentUri uri)
    {
        // GOR2001: undefined symbol - suggest creating a variable
        // Extract the undefined symbol name from the error message
        var match = System.Text.RegularExpressions.Regex.Match(diagnostic.Message, @"'([^']+)'");
        if (!match.Success) return null;

        var symbolName = match.Groups[1].Value;
        var lines = content.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        
        // Find a good place to insert the variable declaration (at the start)
        // For simplicity, insert after the last variable declaration or at line 1
        var insertLine = 0;
        var lastVarLine = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(" olsun;") || lines[i].Contains(" ="))
            {
                lastVarLine = i + 1;
            }
        }
        insertLine = lastVarLine > 0 ? lastVarLine : 1;

        var edit = new TextEdit
        {
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(new Position(insertLine, 0), new Position(insertLine, 0)),
            NewText = $"{symbolName} 0 olsun;\n"
        };

        var codeAction = new CodeAction
        {
            Title = $"Create variable '{symbolName}'",
            Kind = CodeActionKind.QuickFix,
            Diagnostics = new Container<Diagnostic>(diagnostic),
            Edit = new WorkspaceEdit
            {
                Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
                {
                    { uri, new[] { edit } }
                }
            }
        };

        return codeAction;
    }

    private CommandOrCodeAction? CreateGOR2002Action(Diagnostic diagnostic, string content, DocumentUri uri)
    {
        // GOR2002: duplicate declaration - suggest renaming the duplicate
        // Extract the duplicate symbol name from the error message
        // Message format: "Symbol 'x' is already declared in this scope"
        var match = System.Text.RegularExpressions.Regex.Match(diagnostic.Message, @"'([^']+)'");
        if (!match.Success) return null;

        var symbolName = match.Groups[1].Value;
        var newName = $"{symbolName}_1";

        // Create a TextEdit that renames the symbol at the diagnostic location
        var edit = new TextEdit
        {
            Range = diagnostic.Range,
            NewText = newName
        };

        var codeAction = new CodeAction
        {
            Title = $"Rename to '{newName}'",
            Kind = CodeActionKind.QuickFix,
            Diagnostics = new Container<Diagnostic>(diagnostic),
            Edit = new WorkspaceEdit
            {
                Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
                {
                    { uri, new[] { edit } }
                }
            },
            IsPreferred = true
        };

        return codeAction;
    }

    private CommandOrCodeAction? CreateGOR2003Action(Diagnostic diagnostic, string content, DocumentUri uri)
    {
        // GOR2003: arity mismatch
        // Message format (Turkish): "Fonksiyon parametre sayısı uyuşmuyor: '{funcName}' {expected} bekliyor, {actual} verildi."
        var funcMatch = System.Text.RegularExpressions.Regex.Match(diagnostic.Message, @"'([^']+)'");
        if (!funcMatch.Success) return null;

        var functionName = funcMatch.Groups[1].Value;
        
        // Extract expected and actual parameter counts from the Turkish message
        var countsMatch = System.Text.RegularExpressions.Regex.Match(
            diagnostic.Message, 
            @"(\d+) bekliyor, (\d+) verildi");
        
        string title;
        if (countsMatch.Success)
        {
            var expected = int.Parse(countsMatch.Groups[1].Value);
            var actual = int.Parse(countsMatch.Groups[2].Value);
            var diff = expected - actual;
            
            if (diff > 0)
            {
                title = $"Add {diff} argument(s) to '{functionName}' (expects {expected})";
            }
            else
            {
                title = $"Remove {-diff} argument(s) from '{functionName}' (expects {expected})";
            }
        }
        else
        {
            title = $"Check parameter count for '{functionName}'";
        }

        // This is an informational action; user can see the issue and fix manually
        var codeAction = new CodeAction
        {
            Title = title,
            Kind = CodeActionKind.QuickFix,
            Diagnostics = new Container<Diagnostic>(diagnostic),
            IsPreferred = false
        };

        return codeAction;
    }

    public CodeActionRegistrationOptions GetRegistrationOptions(CodeActionCapability? capability, ClientCapabilities clientCapabilities)
    {
        return new CodeActionRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp"),
            CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix),
            ResolveProvider = false
        };
    }
}

