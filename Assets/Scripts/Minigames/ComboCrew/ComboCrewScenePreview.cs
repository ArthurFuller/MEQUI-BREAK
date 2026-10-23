using UnityEngine;

/// <summary>Mostra no Editor os objetos reais nas posições usadas durante a partida.</summary>
[ExecuteAlways]
public sealed class ComboCrewScenePreview : MonoBehaviour
{
    [SerializeField] private ComboCrewStationView[] stations;
    [SerializeField] private ComboCrewWorkerView[] workers;
    [SerializeField] private bool showInEditor = true;
    [SerializeField] private bool showSpeechBubbles = true;
    [SerializeField] private bool showCarriedTrays;
    [SerializeField] private bool showPauseRings;

#if UNITY_EDITOR
    private Vector3[] previousAnchorPositions;
    private Vector3[] previousActorPositions;

    private void Update()
    {
        if (Application.isPlaying)
        {
            previousAnchorPositions = null;
            previousActorPositions = null;
            return;
        }
        if (stations == null || workers == null) return;
        if (!showInEditor)
        {
            foreach (ComboCrewWorkerView worker in workers)
                if (worker != null) worker.HideEditorPreview();
            foreach (ComboCrewStationView station in stations)
                if (station != null) station.SetSpeechPreview(false);
            previousAnchorPositions = null;
            previousActorPositions = null;
            return;
        }
        int count = Mathf.Min(stations.Length, workers.Length);
        bool tracked = previousAnchorPositions != null && previousAnchorPositions.Length == count;
        if (!tracked)
        {
            previousAnchorPositions = new Vector3[count];
            previousActorPositions = new Vector3[count];
        }
        for (int i = 0; i < count; i++)
        {
            if (stations[i] == null || workers[i] == null) continue;
            RectTransform anchor = stations[i].WorkAnchor;
            RectTransform actor = workers[i].PreviewActor;
            if (anchor == null || actor == null) continue;
            bool actorMoved = tracked
                && (actor.position - previousActorPositions[i]).sqrMagnitude > .001f;
            bool anchorMoved = tracked
                && (anchor.position - previousAnchorPositions[i]).sqrMagnitude > .001f;
            if (actorMoved && !anchorMoved)
            {
                UnityEditor.Undo.RecordObject(anchor, "Mover funcionário na bancada");
                anchor.position = actor.position;
            }
            workers[i].PreviewAt(anchor, showSpeechBubbles,
                showCarriedTrays, showPauseRings);
            previousAnchorPositions[i] = anchor.position;
            previousActorPositions[i] = actor.position;
        }
    }
#endif
}
