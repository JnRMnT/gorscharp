import * as vscode from 'vscode';
import * as path from 'path';
import { execFile } from 'child_process';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;
let mirrorPanel: vscode.WebviewPanel | undefined;
let outputChannel: vscode.OutputChannel;
let mirrorLogoUri = '';
let extensionContext: vscode.ExtensionContext;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    extensionContext = context;
    outputChannel = vscode.window.createOutputChannel('Gör#');
    outputChannel.appendLine('Gör# uzantısı etkinleştiriliyor...');

    // Start the Language Server
    await startLanguageServer(context);

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('gorsharp.transpile', transpileCommand),
        vscode.commands.registerCommand('gorsharp.run', runCommand),
        vscode.commands.registerCommand('gorsharp.showMirror', () => showMirrorPanel(context)),
        vscode.commands.registerCommand('gorsharp.refreshMirror', refreshMirrorCommand),
        vscode.commands.registerCommand('gorsharp.importFromCSharp', importFromCSharpCommand)
    );

    outputChannel.appendLine('Gör# uzantısı etkin.');
}

export function deactivate(): Thenable<void> | undefined {
    return client?.stop();
}

// ── Language Server ──────────────────────────────────────

async function startLanguageServer(context: vscode.ExtensionContext): Promise<void> {
    const config = vscode.workspace.getConfiguration('gorsharp');
    let serverPath = config.get<string>('serverPath', '');
    const parsingMode = config.get<string>('parsingMode', 'natural') === 'strict' ? 'strict' : 'natural';

    if (!serverPath) {
        // Look for bundled server
        const bundled = path.join(context.extensionPath, 'server', 'gorsharp-lsp');
        const bundledExe = bundled + (process.platform === 'win32' ? '.exe' : '');

        if (await fileExists(bundledExe)) {
            serverPath = bundledExe;
        } else {
            // Try to find via dotnet tool
            serverPath = 'gorsharp-lsp';
        }
    }

    const serverOptions: ServerOptions = {
        run: { command: serverPath, transport: TransportKind.stdio },
        debug: { command: serverPath, transport: TransportKind.stdio }
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'gorsharp' }],
        outputChannel,
        initializationOptions: {
            gorsharp: {
                parsingMode
            }
        },
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.gör')
        }
    };

    client = new LanguageClient(
        'gorsharp',
        'Gör# Dil Sunucusu',
        serverOptions,
        clientOptions
    );

    // Listen for mirror updates from the server
    client.onNotification('gorsharp/mirror', (params: { uri: string; csharp: string }) => {
        updateMirror(params.uri, params.csharp);
    });

    await client.start();
    outputChannel.appendLine('Dil sunucusu başlatıldı.');
}

// ── Mirror Panel ──────────────────────────────────────────

function showMirrorPanel(context: vscode.ExtensionContext): void {
    if (mirrorPanel) {
        mirrorPanel.reveal(vscode.ViewColumn.Beside);
        return;
    }

    const logoUri = vscode.Uri.joinPath(context.extensionUri, 'icons', 'gorsharp-extension.png');

    mirrorPanel = vscode.window.createWebviewPanel(
        'gorsharpMirror',
        'Gör# → C# Ayna',
        vscode.ViewColumn.Beside,
        {
            enableScripts: true,
            retainContextWhenHidden: true,
            localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, 'icons')]
        }
    );

    mirrorLogoUri = mirrorPanel.webview.asWebviewUri(logoUri).toString();

    mirrorPanel.webview.html = getMirrorHtml('// C# kodu burada görünecek...', mirrorLogoUri);

    mirrorPanel.onDidDispose(() => {
        mirrorPanel = undefined;
    }, null, context.subscriptions);

    // Sync scroll with active editor
    const editor = vscode.window.activeTextEditor;
    if (editor && editor.document.languageId === 'gorsharp') {
        vscode.window.onDidChangeTextEditorVisibleRanges(e => {
            if (e.textEditor.document.languageId === 'gorsharp' && mirrorPanel) {
                const firstVisibleLine = e.visibleRanges[0]?.start.line ?? 0;
                mirrorPanel.webview.postMessage({
                    type: 'scrollTo',
                    line: firstVisibleLine
                });
            }
        }, null, context.subscriptions);
    }
}

function updateMirror(uri: string, csharp: string): void {
    if (!mirrorPanel) return;

    const activeUri = vscode.window.activeTextEditor?.document.uri.toString();
    if (activeUri === uri || !activeUri) {
        mirrorPanel.webview.html = getMirrorHtml(csharp, mirrorLogoUri);
    }
}

function getMirrorHtml(csharp: string, logoUri: string): string {
    const escaped = csharp.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    return `<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Gör# → C# Ayna</title>
    <style>
        body {
            font-family: var(--vscode-editor-font-family);
            font-size: var(--vscode-editor-font-size);
            line-height: var(--vscode-editor-lineHeight);
            color: var(--vscode-editor-foreground);
            background: var(--vscode-editor-background);
            margin: 0;
            padding: 8px 16px;
        }
        pre {
            white-space: pre-wrap;
            word-wrap: break-word;
            margin: 0;
        }
        .header {
            display: flex;
            align-items: center;
            gap: 10px;
            padding: 8px 0;
            margin-bottom: 12px;
            border-bottom: 1px solid var(--vscode-panel-border);
            font-weight: bold;
            opacity: 0.7;
        }
        .header img {
            width: 24px;
            height: 24px;
            border-radius: 6px;
        }
        .gor-line {
            color: var(--vscode-descriptionForeground);
            font-style: italic;
        }
    </style>
</head>
<body>
    <div class="header">
        <img src="${logoUri}" alt="Gör# logo" />
        <span>C# Çıktısı</span>
    </div>
    <pre id="code">${escaped}</pre>
    <script>
        const vscode = acquireVsCodeApi();
        window.addEventListener('message', event => {
            const msg = event.data;
            if (msg.type === 'scrollTo') {
                const lines = document.getElementById('code').innerText.split('\\n');
                const lineHeight = parseFloat(getComputedStyle(document.body).lineHeight) || 18;
                window.scrollTo(0, msg.line * lineHeight);
            }
        });
    </script>
</body>
</html>`;
}

// ── Commands ──────────────────────────────────────────────

async function transpileCommand(): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.languageId !== 'gorsharp') {
        vscode.window.showWarningMessage('Lütfen bir .gör dosyası açın.');
        return;
    }

    const filePath = editor.document.uri.fsPath;
    const mode = vscode.workspace.getConfiguration('gorsharp').get<string>('parsingMode', 'natural') === 'strict'
        ? 'strict'
        : 'natural';
    const terminal = vscode.window.createTerminal('Gör# Dönüştür');
    terminal.show();
    terminal.sendText(`${quoteForShell(await resolveCliCommand(extensionContext))} transpile ${quoteForShell(filePath)} --mode ${mode}`);
}

async function runCommand(): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.languageId !== 'gorsharp') {
        vscode.window.showWarningMessage('Lütfen bir .gör dosyası açın.');
        return;
    }

    // Save before running
    await editor.document.save();

    const filePath = editor.document.uri.fsPath;
    const mode = vscode.workspace.getConfiguration('gorsharp').get<string>('parsingMode', 'natural') === 'strict'
        ? 'strict'
        : 'natural';
    const terminal = vscode.window.createTerminal('Gör# Çalıştır');
    terminal.show();
    terminal.sendText(`${quoteForShell(await resolveCliCommand(extensionContext))} run ${quoteForShell(filePath)} --mode ${mode}`);
}

async function refreshMirrorCommand(): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.languageId !== 'gorsharp') {
        vscode.window.showWarningMessage('Lütfen bir .gör dosyası açın.');
        return;
    }

    await editor.document.save();
    showMirrorPanel(extensionContext);

    const filePath = editor.document.uri.fsPath;
    const mode = vscode.workspace.getConfiguration('gorsharp').get<string>('parsingMode', 'natural') === 'strict'
        ? 'strict'
        : 'natural';

    try {
        const result = await runCliCommand(extensionContext, ['transpile', filePath, '--mode', mode], editor.document.uri);
        updateMirror(editor.document.uri.toString(), result.stdout);
        vscode.window.showInformationMessage('Gör# aynası güncellendi.');
    } catch (error) {
        const message = getErrorMessage(error);
        outputChannel.appendLine(`Ayna yenileme hatası: ${message}`);
        outputChannel.show(true);
        vscode.window.showErrorMessage(`Ayna yenilenemedi: ${message}`);
    }
}

async function importFromCSharpCommand(): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.languageId !== 'csharp') {
        vscode.window.showWarningMessage('Lütfen bir C# dosyası açın.');
        return;
    }

    await editor.document.save();

    const inputPath = editor.document.uri.fsPath;
    const defaultOutput = editor.document.uri.with({
        path: editor.document.uri.path.replace(/\.cs$/i, '.gör')
    });

    const outputUri = await vscode.window.showSaveDialog({
        defaultUri: defaultOutput,
        filters: {
            'Gör# Dosyası': ['gör']
        },
        saveLabel: 'Gör# Olarak İçe Aktar'
    });

    if (!outputUri) {
        return;
    }

    try {
        const result = await runCliCommand(extensionContext, ['fromcs', inputPath, '-o', outputUri.fsPath, '--explain'], editor.document.uri);

        const importedDocument = await vscode.workspace.openTextDocument(outputUri);
        await vscode.window.showTextDocument(importedDocument, vscode.ViewColumn.Beside);

        outputChannel.clear();
        outputChannel.appendLine('C# → Gör# içe aktarma tamamlandı.');
        if (result.stdout.trim().length > 0) {
            outputChannel.appendLine('');
            outputChannel.append(result.stdout);
        }
        if (result.stderr.trim().length > 0) {
            outputChannel.appendLine('');
            outputChannel.append(result.stderr);
        }
        outputChannel.show(true);

        vscode.window.showInformationMessage('C# dosyası Gör# olarak içe aktarıldı.');
    } catch (error) {
        const message = getErrorMessage(error);
        outputChannel.appendLine(`İçe aktarma hatası: ${message}`);
        outputChannel.show(true);
        vscode.window.showErrorMessage(`C# içe aktarma başarısız: ${message}`);
    }
}

// ── Helpers ───────────────────────────────────────────────

async function fileExists(filePath: string): Promise<boolean> {
    try {
        await vscode.workspace.fs.stat(vscode.Uri.file(filePath));
        return true;
    } catch {
        return false;
    }
}

async function resolveCliCommand(context: vscode.ExtensionContext): Promise<string> {
    const config = vscode.workspace.getConfiguration('gorsharp');
    const configuredPath = config.get<string>('cliPath', '').trim();
    if (configuredPath) {
        return configuredPath;
    }

    const bundled = path.join(context.extensionPath, 'tools', process.platform === 'win32' ? 'gorsharp.exe' : 'gorsharp');
    if (await fileExists(bundled)) {
        return bundled;
    }

    return 'gorsharp';
}

async function runCliCommand(
    context: vscode.ExtensionContext,
    args: string[],
    documentUri?: vscode.Uri
): Promise<{ stdout: string; stderr: string }> {
    const command = await resolveCliCommand(context);
    const workspaceFolder = documentUri ? vscode.workspace.getWorkspaceFolder(documentUri) : undefined;

    return new Promise((resolve, reject) => {
        execFile(
            command,
            args,
            {
                cwd: workspaceFolder?.uri.fsPath
            },
            (error, stdout, stderr) => {
                if (error) {
                    reject(new Error([error.message, stderr].filter(Boolean).join('\n')));
                    return;
                }

                resolve({ stdout, stderr });
            }
        );
    });
}

function quoteForShell(value: string): string {
    if (!/[\s"]/u.test(value)) {
        return value;
    }

    return `"${value.replace(/"/g, '\\"')}"`;
}

function getErrorMessage(error: unknown): string {
    if (error instanceof Error) {
        return error.message;
    }

    return String(error);
}
