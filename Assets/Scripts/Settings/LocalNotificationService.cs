using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using Unity.Notifications;
#endif

/// <summary>Agenda lembretes locais apenas quando o usuário os habilitou.</summary>
public static class LocalNotificationService
{
    [Serializable] private sealed class ActionRecord
    {
        public string date = string.Empty;
        public string reminder = string.Empty;
    }

    [Serializable] private sealed class ActionFile
    {
        public List<ActionRecord> items = new List<ActionRecord>();
    }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private const string ChannelId = "macbreak_reminders";
    private static bool initialized;

    private static void Initialize()
    {
        if (initialized) return;
        NotificationCenterArgs args = NotificationCenterArgs.Default;
        args.AndroidChannelId = ChannelId;
        args.AndroidChannelName = "Lembretes do MacBreak";
        args.AndroidChannelDescription = "Fim de turno e acompanhamentos agendados";
        args.PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Sound;
        NotificationCenter.Initialize(args);
        initialized = true;
    }
#endif

    public static IEnumerator RequestPermission(Action<bool> completed)
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        Initialize();
        NotificationsPermissionRequest request = NotificationCenter.RequestPermission();
        yield return request;
        completed?.Invoke(request.Status == NotificationsPermissionStatus.Granted);
#else
        completed?.Invoke(true);
        yield break;
#endif
    }

    public static void Refresh()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        Initialize();
        NotificationCenter.CancelAllScheduledNotifications();

        SettingsManager settings = SettingsManager.Instance;
        if (settings == null || !settings.NotificationsEnabled)
            return;

        DateTime now = DateTime.Now;
        int hour = ShiftEndHour(PlayerManager.Instance?.Shift);
        if (hour < 0)
            return;

        if (settings.EndOfShiftReminderEnabled)
            Schedule(1000, "Fim de turno", "Seu turno terminou. Como foi sua pausa hoje?",
                new DateTime(now.Year, now.Month, now.Day, hour, 0, 0),
                NotificationRepeatInterval.Daily);

        List<DateTime> actionDates = ReadActionReminderDates(hour);
        actionDates.Sort();
        for (int i = 0; i < actionDates.Count && i < 50; i++)
        {
            if (actionDates[i] <= now) continue;
            Schedule(1001 + i, "Acompanhamento", "Você tem um acompanhamento para revisar no MacBreak.",
                actionDates[i], NotificationRepeatInterval.OneTime);
        }
#endif
    }

    private static int ShiftEndHour(string shift)
    {
        if (string.Equals(shift, "Manhã", StringComparison.OrdinalIgnoreCase)) return 10;
        if (string.Equals(shift, "Tarde", StringComparison.OrdinalIgnoreCase)) return 16;
        if (string.Equals(shift, "Noite", StringComparison.OrdinalIgnoreCase)) return 22;
        return -1;
    }

    private static List<DateTime> ReadActionReminderDates(int hour)
    {
        var dates = new List<DateTime>();
        string path = Path.Combine(Application.persistentDataPath, "dashboard_actions.json");
        ActionFile file = ReadActions(path) ?? ReadActions(path + ".bak") ?? ReadActions(path + ".tmp");
        if (file?.items == null) return dates;
        try
        {
            foreach (ActionRecord action in file.items)
            {
                if (action == null || action.reminder == "none" ||
                    !DateTime.TryParseExact(action.date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime date))
                    continue;
                if (action.reminder == "before") date = date.AddDays(-1);
                else if (action.reminder != "day") continue;
                DateTime fireTime = date.AddHours(hour);
                if (fireTime > DateTime.Now && !dates.Contains(fireTime))
                    dates.Add(fireTime);
            }
        }
        catch (Exception) { }
        return dates;
    }

    private static ActionFile ReadActions(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            ActionFile file = JsonUtility.FromJson<ActionFile>(File.ReadAllText(path));
            return file?.items != null ? file : null;
        }
        catch (Exception) { return null; }
    }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private static void Schedule(int id, string title, string body, DateTime fireTime,
        NotificationRepeatInterval repeat)
    {
        var notification = new Notification
        {
            Identifier = id,
            Title = title,
            Text = body,
            ShowInForeground = false
        };
        NotificationCenter.ScheduleNotification(notification,
            new NotificationDateTimeSchedule(fireTime, repeat));
    }
#endif
}
