using UnityEngine;

/// <summary>Controls the Particle Systems placed under each station in the scene.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewStationParticles : MonoBehaviour
{
    [SerializeField] private ParticleSystem working;
    [SerializeField] private ParticleSystem broken;
    [SerializeField] private ParticleSystem repairing;

    private bool isWorking;
    private int alert;

    public void SetWorking(bool value)
    {
        isWorking = value;
        Refresh();
    }

    public void SetAlert(int mode)
    {
        alert = mode;
        Refresh();
    }

    private void Refresh()
    {
        SetPlaying(working, alert == 0 && isWorking);
        SetPlaying(broken, alert == 1);
        SetPlaying(repairing, alert == 2);
    }

    private static void SetPlaying(ParticleSystem effect, bool play)
    {
        if (effect == null) return;
        if (play)
        {
            if (!effect.isPlaying) effect.Play();
        }
        else if (effect.isPlaying)
        {
            effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void OnDisable()
    {
        isWorking = false;
        alert = 0;
        Stop(working);
        Stop(broken);
        Stop(repairing);
    }

    private static void Stop(ParticleSystem effect)
    {
        if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
