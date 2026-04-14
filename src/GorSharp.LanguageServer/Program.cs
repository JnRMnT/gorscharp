using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using GorSharp.LanguageServer.Handlers;
using GorSharp.LanguageServer.Services;
using System.Linq;

namespace GorSharp.LanguageServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        LanguageServerTrace.Info("Language server process starting.");

        var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
        {
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .OnInitialize((languageServer, request, cancellationToken) =>
                {
                    var parsingModeService = languageServer.Services.GetService<ParsingModeService>();
                    parsingModeService?.UpdateFromInitializationOptions(request.InitializationOptions);

                    var staticRegistrationOverrides = ForceStaticRegistration(request.Capabilities);
                    var workspaceFolderCount = request.WorkspaceFolders?.Count() ?? 0;
                    LanguageServerTrace.Info($"Initialize request: client={request.ClientInfo?.Name ?? "unknown"} {request.ClientInfo?.Version ?? string.Empty}, rootUri={request.RootUri}, workspaceFolders={workspaceFolderCount}, trace={request.Trace}");
                    LanguageServerTrace.Info($"Parser mode: {(parsingModeService?.Current.NaturalMode == false ? "strict" : "natural")}");
                    LanguageServerTrace.Info($"Client capabilities: hover={DescribeClientCapability(request.Capabilities?.TextDocument?.Hover)}, completion={DescribeClientCapability(request.Capabilities?.TextDocument?.Completion)}, definition={DescribeClientCapability(request.Capabilities?.TextDocument?.Definition)}, semanticTokens={DescribeClientCapability(request.Capabilities?.TextDocument?.SemanticTokens)}, inlayHint={DescribeClientCapability(request.Capabilities?.TextDocument?.InlayHint)}, publishDiagnostics={DescribeClientCapability(request.Capabilities?.TextDocument?.PublishDiagnostics)}");
                    if (staticRegistrationOverrides > 0)
                    {
                        LanguageServerTrace.Warning($"Disabled dynamic registration for {staticRegistrationOverrides} client capabilities to force static server capability advertisement.");
                    }

                    return Task.CompletedTask;
                })
                .OnInitialized((languageServer, request, response, cancellationToken) =>
                {
                    var capabilities = response.Capabilities;
                    LanguageServerTrace.Info($"Server capabilities: hover={DescribeServerCapability(capabilities.HoverProvider)}, completion={DescribeServerCapability(capabilities.CompletionProvider)}, definition={DescribeServerCapability(capabilities.DefinitionProvider)}, references={DescribeServerCapability(capabilities.ReferencesProvider)}, rename={DescribeServerCapability(capabilities.RenameProvider)}, formatting={DescribeServerCapability(capabilities.DocumentFormattingProvider)}, signatureHelp={DescribeServerCapability(capabilities.SignatureHelpProvider)}, documentSymbol={DescribeServerCapability(capabilities.DocumentSymbolProvider)}, semanticTokens={DescribeServerCapability(capabilities.SemanticTokensProvider)}, inlayHint={DescribeServerCapability(capabilities.InlayHintProvider)}, diagnostics={DescribeServerCapability(capabilities.DiagnosticProvider)}");
                    LanguageServerTrace.Info($"▶ InlayHintProvider capability: {(capabilities.InlayHintProvider is null ? "NULL / NOT ADVERTISED" : "PRESENT: " + capabilities.InlayHintProvider.GetType().Name)}");
                    return Task.CompletedTask;
                })
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .WithServices(services =>
                {
                    services.AddSingleton<DocumentStore>();
                    services.AddSingleton<ParsingModeService>();
                    services.AddSingleton<TranspilationService>();
                    services.AddSingleton<SuffixExplanationService>();
                    services.AddSingleton<SymbolAnalysisService>();
                    services.AddSingleton<BuiltInSignaturesService>();
                    services.AddSingleton<FormatterService>();
                })
                .WithHandler<TextDocumentSyncHandler>()
                .WithHandler<CompletionHandler>()
                .WithHandler<HoverHandler>()
                .WithHandler<DocumentFormattingHandler>()
                .WithHandler<DiagnosticHandler>()
                .WithHandler<DefinitionHandler>()
                .WithHandler<ReferencesHandler>()
                .WithHandler<RenameHandler>()
                .WithHandler<PrepareRenameHandler>()
                .WithHandler<SignatureHelpHandler>()
                .WithHandler<DocumentSymbolHandler>()
                .WithHandler<CallHierarchyHandler>()
                .WithHandler<CodeLensHandler>()
                .WithHandler<CodeActionHandler>()
                .WithHandler<InlayHintsHandler>()
                .WithHandler<SemanticTokensHandler>();
        }).ConfigureAwait(false);

            LanguageServerTrace.Info("Language server initialized and waiting for exit.");
        await server.WaitForExit.ConfigureAwait(false);
    }

    private static string DescribeClientCapability(object? capability)
    {
        return capability is null ? "none" : capability.GetType().Name;
    }

    private static string DescribeServerCapability(object? capability)
    {
        if (capability is null)
        {
            return "none";
        }

        return capability switch
        {
            bool boolean => boolean ? "true" : "false",
            _ => capability.GetType().Name
        };
    }

    private static int ForceStaticRegistration(ClientCapabilities? capabilities)
    {
        var textDocument = capabilities?.TextDocument;
        if (textDocument is null)
        {
            return 0;
        }

        var overrideCount = 0;
        overrideCount += DisableDynamicRegistration(textDocument.Hover, value => textDocument.Hover = value, "textDocument/hover");
        overrideCount += DisableDynamicRegistration(textDocument.Completion, value => textDocument.Completion = value, "textDocument/completion");
        overrideCount += DisableDynamicRegistration(textDocument.Definition, value => textDocument.Definition = value, "textDocument/definition");
        overrideCount += DisableDynamicRegistration(textDocument.References, value => textDocument.References = value, "textDocument/references");
        overrideCount += DisableDynamicRegistration(textDocument.Rename, value => textDocument.Rename = value, "textDocument/rename");
        overrideCount += DisableDynamicRegistration(textDocument.Formatting, value => textDocument.Formatting = value, "textDocument/formatting");
        overrideCount += DisableDynamicRegistration(textDocument.SignatureHelp, value => textDocument.SignatureHelp = value, "textDocument/signatureHelp");
        overrideCount += DisableDynamicRegistration(textDocument.DocumentSymbol, value => textDocument.DocumentSymbol = value, "textDocument/documentSymbol");
        overrideCount += DisableDynamicRegistration(textDocument.SemanticTokens, value => textDocument.SemanticTokens = value, "textDocument/semanticTokens");
        overrideCount += DisableDynamicRegistration(textDocument.InlayHint, value => textDocument.InlayHint = value, "textDocument/inlayHint");
        overrideCount += DisableDynamicRegistration(textDocument.CodeAction, value => textDocument.CodeAction = value, "textDocument/codeAction");
        overrideCount += DisableDynamicRegistration(textDocument.CodeLens, value => textDocument.CodeLens = value, "textDocument/codeLens");
        overrideCount += DisableDynamicRegistration(textDocument.CallHierarchy, value => textDocument.CallHierarchy = value, "textDocument/callHierarchy");
        overrideCount += DisableDynamicRegistration(textDocument.Diagnostic, value => textDocument.Diagnostic = value, "textDocument/diagnostic");
        return overrideCount;
    }

    private static int DisableDynamicRegistration<TCapability>(
        Supports<TCapability> support,
        Action<Supports<TCapability>> assign,
        string capabilityName)
        where TCapability : class?
    {
        if (!support.IsSupported || support.Value is not IDynamicCapability dynamicCapability || !dynamicCapability.DynamicRegistration)
        {
            return 0;
        }

        dynamicCapability.DynamicRegistration = false;
        assign(support);
        LanguageServerTrace.Info($"Forcing static registration for {capabilityName}.");
        return 1;
    }
}
