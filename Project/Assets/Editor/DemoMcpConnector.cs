using UnityEditor;
using MCPForUnity.Editor.Services;

[InitializeOnLoad]
public static class DemoMcpConnector
{
    static DemoMcpConnector()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Connect;
        };
        EditorApplication.delayCall += Connect;
    }
    static void Connect()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Connect;
            return;
        }
        if (!MCPServiceLocator.Bridge.IsRunning) LabSetup.Connect();
    }
}
