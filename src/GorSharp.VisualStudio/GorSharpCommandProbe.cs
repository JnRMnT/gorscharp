using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace GorSharp.VisualStudio;

[Export(typeof(IVsTextViewCreationListener))]
[Name("GorSharp Command Probe")]
[ContentType("gorsharp")]
[TextViewRole(PredefinedTextViewRoles.Document)]
internal sealed class GorSharpCommandProbe : IVsTextViewCreationListener
{
    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
        GorSharpVisualStudioLogger.Verbose("Command probe is disabled for this build.");
    }

    private sealed class GorSharpCommandFilter : IOleCommandTarget
    {
        private readonly IVsTextView _view;

        public GorSharpCommandFilter(IVsTextView view)
        {
            _view = view;
        }

        public IOleCommandTarget? Next { get; set; }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            try
            {
                if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 && IsDefinitionLike(nCmdID))
                {
                    var caret = GetCaret();
                    GorSharpVisualStudioLogger.Info($"Command Exec: group=VSStd97 cmd={ToCommandName(nCmdID)}({nCmdID}) caret={caret}");
                }
            }
            catch (Exception ex)
            {
                GorSharpVisualStudioLogger.Error("Command probe Exec logging failed.", ex);
            }

            if (Next is null)
            {
                GorSharpVisualStudioLogger.Warning($"Command Exec had no downstream target: cmd={ToCommandName(nCmdID)}({nCmdID}).");
                return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
            }

            return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            try
            {
                if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
                {
                    for (uint i = 0; i < cCmds; i++)
                    {
                        var cmdId = prgCmds[i].cmdID;
                        if (IsDefinitionLike(cmdId))
                        {
                            var caret = GetCaret();
                            GorSharpVisualStudioLogger.Verbose($"Command QueryStatus: group=VSStd97 cmd={ToCommandName(cmdId)}({cmdId}) caret={caret}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GorSharpVisualStudioLogger.Error("Command probe QueryStatus logging failed.", ex);
            }

            if (Next is null)
            {
                GorSharpVisualStudioLogger.Warning("Command QueryStatus had no downstream target.");
                return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
            }

            return Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private string GetCaret()
        {
            var hr = _view.GetCaretPos(out var line, out var column);
            return ErrorHandler.Succeeded(hr) ? $"{line}:{column}" : "<unknown>";
        }

        private static bool IsDefinitionLike(uint cmdId)
        {
            return cmdId == (uint)VSConstants.VSStd97CmdID.GotoDefn
                || cmdId == (uint)VSConstants.VSStd97CmdID.GotoDecl
                || cmdId == (uint)VSConstants.VSStd97CmdID.GotoRef;
        }

        private static string ToCommandName(uint cmdId)
        {
            if (cmdId == (uint)VSConstants.VSStd97CmdID.GotoDefn)
            {
                return "GotoDefn";
            }

            if (cmdId == (uint)VSConstants.VSStd97CmdID.GotoDecl)
            {
                return "GotoDecl";
            }

            if (cmdId == (uint)VSConstants.VSStd97CmdID.GotoRef)
            {
                return "GotoRef";
            }

            return "Unknown";
        }
    }
}
