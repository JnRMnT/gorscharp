---
name: mirror-panel
description: How the Gör# side-by-side mirror and diff feature works across the LSP, VS Code, and Visual Studio
---

# Mirror Panel Skill — Gör#

## Purpose

The mirror panel is Gör#'s core educational feature. As a student writes Turkish code, they see the equivalent C# code in real-time, side by side. This ensures Gör# teaches C#, not just hides it.

## Architecture

```
Student types .gör code
    → VS Code / Visual Studio editor
    → LSP client sends textDocument/didChange
    → GorSharp.LanguageServer receives change
    → Transpiler runs: .gör → AST → C# + SourceMap
    → LSP sends custom notification: gorsharp/mirror
    → Extension renders mirror panel with line mapping
```

## LSP Custom Notification

### Protocol: `gorsharp/mirror`

```typescript
// Server → Client notification
interface GorSharpMirrorNotification {
  method: 'gorsharp/mirror';
  params: MirrorParams;
}

interface MirrorParams {
  /** URI of the .gör file */
  uri: string;

  /** Whether transpilation succeeded */
  success: boolean;

  /** Error message if transpilation failed (in Turkish) */
  error?: string;

  /** Line-by-line mapping */
  mappings: MirrorMapping[];

  /** Full generated C# code */
  generatedCode: string;
}

interface MirrorMapping {
  /** 1-based line number in .gör source */
  gorLine: number;

  /** 1-based line number in generated .cs */
  csLine: number;

  /** Source code from .gör */
  gorCode: string;

  /** Generated C# code */
  csCode: string;

  /** Color group ID for diff mode (same ID = same color) */
  colorGroup: number;
}
```

## VS Code Implementation

### Mirror Webview Panel

**Location**: `src/GorSharp.VSCode/src/mirrorPanel.ts`

- Registered as a webview panel: `gorsharp.mirrorPanel`
- Opens to the right of the editor (ViewColumn.Beside)
- Content: two-column layout (Gör# left, C# right) with synchronized scrolling
- Updates on every `gorsharp/mirror` notification (debounced 300ms on server side)

### Webview HTML Structure

```html
<div class="mirror-container">
  <div class="mirror-column gor-column">
    <div class="mirror-header">Gör# Kaynak</div>
    <div class="mirror-lines" id="gor-lines">
      <!-- Line elements with data-line attribute -->
    </div>
  </div>
  <div class="mirror-column cs-column">
    <div class="mirror-header">C# Çıktısı</div>
    <div class="mirror-lines" id="cs-lines">
      <!-- Corresponding C# lines -->
    </div>
  </div>
</div>
```

### Diff Mode

Toggle via command `gorsharp.toggleDiffMode`:
- Each line pair gets a background color based on `colorGroup` ID
- Matching lines share the same hue (e.g., assignment = blue, print = green, control flow = orange)
- Hovering a .gör line highlights the corresponding .cs line(s) and vice versa
- Connection lines drawn between corresponding line numbers

### Color Groups

| Feature | Color | colorGroup |
|---------|-------|------------|
| Assignment | Blue (#E3F2FD) | 1 |
| Output (yazdır) | Green (#E8F5E9) | 2 |
| Input (oku) | Teal (#E0F2F1) | 3 |
| Conditional (eğer) | Orange (#FFF3E0) | 4 |
| Loop (döngü) | Purple (#F3E5F5) | 5 |
| Function | Indigo (#E8EAF6) | 6 |
| Class/OOP | Red (#FFEBEE) | 7 |
| Error handling | Yellow (#FFFDE7) | 8 |

## Visual Studio Implementation

### Mirror Tool Window

**Location**: `src/GorSharp.VisualStudio/MirrorToolWindow.cs`

- WPF-based tool window implementing `ToolWindowPane`
- Same two-column layout via XAML `Grid` with `ColumnDefinition`s
- Receives mirror data from the LSP client
- Supports same diff mode with color coding

## Debouncing Strategy

1. Student types in editor
2. LSP client sends `textDocument/didChange` immediately
3. Language server debounces: waits 300ms after last change before transpiling
4. Transpiles → generates mirror data → sends `gorsharp/mirror` notification
5. Extension updates webview/tool window

This prevents flicker during rapid typing while keeping the mirror feeling "live".

## Error State

When the .gör code has syntax errors:
- Mirror panel shows last successful transpilation (grayed out)
- Error message bar at top: "Sözdizimi hatası: satır 5 — beklenmeyen simge 'xyz'"
- Error lines highlighted in red in the .gör column
- No C# shown for error lines

## Run Button Integration

- VS Code: Editor title bar button "▶ Çalıştır"
- Command: `gorsharp.run`
- Action: Saves file → calls `gorsharp run <file.gör>` in integrated terminal
- Output appears in terminal panel below the editor

## Activation

Both extensions activate when:
- A `.gör` file is opened
- The `gorsharp` CLI is detected in PATH
- The Language Server is available

Mirror panel opens automatically on first `.gör` file open (can be configured off).
