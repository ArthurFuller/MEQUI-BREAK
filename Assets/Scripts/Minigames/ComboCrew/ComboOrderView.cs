using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ComboOrderView : MonoBehaviour
{
    [SerializeField] private RectTransform card;
    [SerializeField] private Image deadlineFill;
    [SerializeField] private TMP_Text orderLabel;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private GameObject expiredOverlay;

    private Tween pulse;
    public bool Available { get; private set; } = true;
    public bool Configured => card != null && deadlineFill != null && orderLabel != null
        && stateLabel != null && expiredOverlay != null;
    public string ConfigurationError => card == null ? "card"
        : deadlineFill == null ? "deadlineFill"
        : orderLabel == null ? "orderLabel"
        : stateLabel == null ? "stateLabel"
        : expiredOverlay == null ? "expiredOverlay"
        : null;

    public void Initialize()
    {
        ConfigureHorizontalBar(deadlineFill);
        Hide(true);
    }

    public void Bind(int orderId, float deadline)
    {
        pulse?.Kill();
        Available = false;
        gameObject.SetActive(true);
        expiredOverlay.SetActive(false);
        deadlineFill.gameObject.SetActive(true);
        orderLabel.SetText("PEDIDO {0}", orderId + 1);
        stateLabel.text = "PREPARO";
        SetHorizontalProgress(deadlineFill, deadline > 0f ? 1f : 0f);
        card.localScale = Vector3.one;
        pulse = card.DOPunchScale(Vector3.one * .05f, .18f, 1, .3f);
    }

    public void Refresh(float remaining, float total, string state, float stageProgress)
    {
        if (Available) return;
        deadlineFill.gameObject.SetActive(true);
        SetHorizontalProgress(deadlineFill, total <= 0f ? 0f : Mathf.Clamp01(remaining / total));
        RefreshStage(state, stageProgress);
    }

    public void RefreshStage(string state, float stageProgress)
    {
        if (Available) return;
        stateLabel.text = state;
    }

    public void MarkDelivered()
    {
        if (Available) return;
        SetHorizontalProgress(deadlineFill, 0f);
        deadlineFill.gameObject.SetActive(false);
        stateLabel.text = "LOUÇA";
        pulse?.Kill();
        pulse = card.DOPunchScale(Vector3.one * .06f, .2f, 1, .25f);
    }

    public void MarkExpired()
    {
        if (Available) return;
        SetHorizontalProgress(deadlineFill, 0f);
        stateLabel.text = "TEMPO ESGOTADO";
        expiredOverlay.SetActive(true);
        pulse?.Kill();
        pulse = card.DOPunchScale(Vector3.one * .06f, .18f, 1, .25f)
            .OnComplete(() => Hide(false));
    }

    public void Hide(bool instant = false)
    {
        pulse?.Kill();
        if (card != null) card.localScale = Vector3.one;
        if (expiredOverlay != null) expiredOverlay.SetActive(false);
        if (deadlineFill != null) SetHorizontalProgress(deadlineFill, 0f);
        Available = true;
        gameObject.SetActive(false);
    }

    private static void ConfigureHorizontalBar(Image image)
    {
        if (image == null) return;

        // Estes fills do Combo Crew são imagens sólidas sem sprite. o modo Filled da Image
        // não recorta corretamente esse caso, então a largura é controlada pelo RectTransform.
        image.type = Image.Type.Simple;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
        rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetHorizontalProgress(Image image, float value)
    {
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        Vector2 max = rect.anchorMax;
        max.x = Mathf.Clamp01(value);
        rect.anchorMax = max;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private void OnDisable()
    {
        pulse?.Kill();
        if (card != null) card.localScale = Vector3.one;
    }
}
