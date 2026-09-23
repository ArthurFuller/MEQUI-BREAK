/// <summary>
/// Camada de compatibilidade para scripts antigos que ainda chamam HapticFeedback.
/// A implementação real fica em MequiHaptics, que usa feedback semântico,
/// respeita SettingsManager.VibrationEnabled e evita Handheld.Vibrate().
/// </summary>
public static class HapticFeedback
{
    public enum Type
    {
        Light,
        Medium,
        Heavy,
        Selection,
        Success,
        Warning,
        Error
    }

    public static void Play(Type type)
    {
        switch (type)
        {
            case Type.Light:
                MequiHaptics.LightImpact();
                break;
            case Type.Selection:
                MequiHaptics.Selection();
                break;
            case Type.Medium:
            case Type.Heavy:
                MequiHaptics.Confirm();
                break;
            case Type.Success:
                MequiHaptics.Success();
                break;
            case Type.Warning:
            case Type.Error:
                MequiHaptics.Reject();
                break;
        }
    }

    // Mantido para não quebrar chamadas antigas. A intensidade continua sendo
    // resolvida semanticamente pelo sistema nativo disponível em cada plataforma.
    public static void PlayAdvanced(Type type) => Play(type);

    public static int GetIosImpactStyle(Type type)
    {
        switch (type)
        {
            case Type.Light:
            case Type.Selection:
                return 0;
            case Type.Heavy:
                return 2;
            default:
                return 1;
        }
    }

    public static int GetAndroidEffectType(Type type)
    {
        switch (type)
        {
            case Type.Light:
            case Type.Selection:
                return 0;
            case Type.Heavy:
                return 5;
            case Type.Success:
                return 2;
            case Type.Warning:
                return 3;
            case Type.Error:
                return 4;
            default:
                return 1;
        }
    }
}
