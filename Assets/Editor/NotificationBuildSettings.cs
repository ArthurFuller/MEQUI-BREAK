using Unity.Notifications;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

internal sealed class NotificationBuildSettings : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
            NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart = true;
    }
}
