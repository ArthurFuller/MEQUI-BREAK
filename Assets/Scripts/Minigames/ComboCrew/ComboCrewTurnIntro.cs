using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Aviso curto de turno em uma camada que cobre todo o Canvas, fora da Safe Area.</summary>
public sealed class ComboCrewTurnIntro : MonoBehaviour
{
    private GameObject overlay;
    private RectTransform card;
    private CanvasGroup group;
    private Sequence introSequence;

    public void Build(Canvas canvas, TMP_Text fontSource)
    {
        overlay = new GameObject("AvisoDoTurno", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(CanvasGroup));
        RectTransform full = overlay.GetComponent<RectTransform>();
        full.SetParent(canvas.transform, false);
        full.anchorMin = Vector2.zero;
        full.anchorMax = Vector2.one;
        full.offsetMin = Vector2.zero;
        full.offsetMax = Vector2.zero;
        Image shade = overlay.GetComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, .82f);
        Button skip = overlay.GetComponent<Button>();
        skip.transition = Selectable.Transition.None;
        skip.onClick.AddListener(Hide);
        group = overlay.GetComponent<CanvasGroup>();

        GameObject panel = new GameObject("CardDoTurno", typeof(RectTransform), typeof(Image));
        card = panel.GetComponent<RectTransform>();
        card.SetParent(full, false);
        card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(780f, 170f);
        card.anchoredPosition = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(.14f, .14f, .14f, 1f);
        panelImage.raycastTarget = false;

        GameObject stripe = new GameObject("Acento", typeof(RectTransform), typeof(Image));
        RectTransform stripeRect = stripe.GetComponent<RectTransform>();
        stripeRect.SetParent(card, false);
        stripeRect.anchorMin = new Vector2(0f, 1f);
        stripeRect.anchorMax = Vector2.one;
        stripeRect.pivot = new Vector2(.5f, 1f);
        stripeRect.sizeDelta = new Vector2(0f, 9f);
        stripeRect.anchoredPosition = Vector2.zero;
        Image stripeImage = stripe.GetComponent<Image>();
        stripeImage.color = new Color(1f, .78f, .12f);
        stripeImage.raycastTarget = false;

        TMP_Text title = Instantiate(fontSource, card);
        title.name = "TituloDoTurno";
        title.alignment = TextAlignmentOptions.Center;
        title.fontSize = 48f;
        title.enableAutoSizing = true;
        title.fontSizeMin = 28f;
        title.fontSizeMax = 48f;
        title.color = Color.white;
        title.raycastTarget = false;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(28f, 12f);
        titleRect.offsetMax = new Vector2(-28f, -12f);
        overlay.SetActive(false);
        this.title = title;
    }

    private TMP_Text title;

    public void Show(string text)
    {
        if (overlay == null) return;
        introSequence?.Kill();
        title.text = text;
        overlay.transform.SetAsLastSibling();
        overlay.SetActive(true);
        group.alpha = 1f;
        card.localScale = Vector3.one * .82f;
        introSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this)
            .Append(card.DOScale(1.04f, .25f).SetEase(Ease.OutCubic))
            .Append(card.DOScale(1f, .16f).SetEase(Ease.OutSine))
            .AppendInterval(.9f)
            .Append(group.DOFade(0f, .2f))
            .OnComplete(Hide);
    }

    public void Hide()
    {
        introSequence?.Kill();
        introSequence = null;
        if (overlay != null) overlay.SetActive(false);
    }

    private void OnDestroy()
    {
        introSequence?.Kill();
        if (overlay != null) Destroy(overlay);
    }
}
