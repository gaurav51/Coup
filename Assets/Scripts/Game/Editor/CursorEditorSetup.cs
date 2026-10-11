using System.IO;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace Coup.Editor
{
    /// <summary>
    /// Points Unity at Cursor the first time a valid Cursor installation is available.
    /// Stops once the selection sticks, so a later manual editor choice is left alone.
    /// </summary>
    [InitializeOnLoad]
    static class CursorEditorSetup
    {
        const string CursorApp = "/Applications/Cursor.app";
        const string ConfiguredKey = "Coup.ExternalEditorIsCursor";

        static CursorEditorSetup()
        {
            EditorApplication.delayCall += SelectCursor;
        }

        static void SelectCursor()
        {
            if (EditorPrefs.GetBool(ConfiguredKey, false))
                return;

            if (!Directory.Exists(CursorApp))
                return;

            if (CodeEditor.CurrentEditorInstallation != CursorApp)
                CodeEditor.SetExternalScriptEditor(CursorApp);

            if (CodeEditor.CurrentEditorInstallation != CursorApp)
                return;

            EditorPrefs.SetBool(ConfiguredKey, true);
            Debug.Log("External script editor set to Cursor.");
        }
    }
}
