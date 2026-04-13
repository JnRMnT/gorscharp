import * as vscode from 'vscode';
import * as path from 'path';
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

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    outputChannel = vscode.window.createOutputChannel('Gör#');
    outputChannel.appendLine('Gör# uzantısı etkinleştiriliyor...');

    // Start the Language Server
    await startLanguageServer(context);

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('gorsharp.transpile', transpileCommand),
        vscode.commands.registerCommand('gorsharp.run', runCommand),
        vscode.commands.registerCommand('gorsharp.showMirror', () => showMirrorPanel(context))
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
    terminal.sendText(`gorsharp transpile "${filePath}" --mode ${mode}`);
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
    terminal.sendText(`gorsharp run "${filePath}" --mode ${mode}`);
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
