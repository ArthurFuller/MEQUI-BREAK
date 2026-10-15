using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small pooled UI particles tied to the station's actual activity.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewStationParticles : MonoBehaviour
{
    private const int PoolSize = 3;
    private static Sprite dotSprite;
    private static Sprite ringSprite;
    private readonly Image[] images = new Image[PoolSize];
    private readonly Tween[] flights = new Tween[PoolSize];
    private RectTransform origin;
    private RectTransform layer;
    private Vector2 emissionOffset;
    private int station;
    private int nextSlot;
    private float nextEmit;
    private bool working;

    public void Bind(int stationIndex, RectTransform effectOrigin, RectTransform effectLayer,
        Vector2 effectOffset)
    {
        station = stationIndex;
        origin = effectOrigin;
        layer = effectLayer;
        emissionOffset = effectOffset;
    }

    public void SetOffset(Vector2 effectOffset) => emissionOffset = effectOffset;

    public void SetWorking(bool value)
    {
        if (!value || origin == null || layer == null)
        {
            working = false;
            return;
        }
        if (!working)
        {
            working = true;
            nextEmit = Time.time;
        }
        if (Time.time < nextEmit) return;
        nextEmit = Time.time + (station == 4 ? .24f : .38f);
        Emit();
    }

    private void Emit()
    {
        int slot = nextSlot++ % PoolSize;
        if (images[slot] == null) CreateParticle(slot);
        flights[slot]?.Kill();
        Image image = images[slot];
        RectTransform rect = image.rectTransform;
        float canvasScale = Mathf.Max(.01f, Mathf.Abs(layer.lossyScale.x));
        float spread = (slot - 1) * 19f;
        Vector3 start = origin.TransformPoint(new Vector3(
            emissionOffset.x + spread, emissionOffset.y + 7f, 0f));
        Vector3 end = start + new Vector3((slot - 1) * 8f * canvasScale,
            (station == 1 ? 48f : 36f) * canvasScale, 0f);
        rect.position = start;
        rect.localScale = Vector3.one * .7f;
        image.color = station switch
        {
            0 => new Color(1f, .87f, .49f, .62f),
            1 => new Color(.95f, .95f, .89f, .46f),
            2 => new Color(.69f, .94f, .55f, .58f),
            3 => new Color(1f, .87f, .35f, .58f),
            _ => new Color(.48f, .83f, 1f, .68f)
        };
        flights[slot] = DOTween.Sequence().SetTarget(this)
            .Append(rect.DOMove(end, .72f))
            .Join(image.DOFade(0f, .72f))
            .Join(rect.DOScale(1.15f, .72f))
            .SetEase(Ease.OutSine);
    }

    private void CreateParticle(int slot)
    {
        GameObject item = new GameObject("ParticulaAtividade", typeof(RectTransform), typeof(Image));
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(layer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        float size = station == 4 ? 18f : station == 1 ? 13f : 9f;
        rect.sizeDelta = new Vector2(size, size);
        Image image = item.GetComponent<Image>();
        image.sprite = station == 4 ? GetSprite(true) : GetSprite(false);
        image.raycastTarget = false;
        image.color = Color.clear;
        images[slot] = image;
    }

    private static Sprite GetSprite(bool ring)
    {
        if (ring && ringSprite != null) return ringSprite;
        if (!ring && dotSprite != null) return dotSprite;
        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f));
                float alpha = ring ? Mathf.Clamp01((distance - 4.5f) * 2f) * Mathf.Clamp01((7.5f - distance) * 2f)
                    : Mathf.Clamp01((7.5f - distance) * 2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        if (ring) ringSprite = sprite;
        else dotSprite = sprite;
        return sprite;
    }

    private void OnDisable()
    {
        working = false;
        for (int i = 0; i < PoolSize; i++)
        {
            flights[i]?.Kill();
            flights[i] = null;
            if (images[i] != null) images[i].color = Color.clear;
        }
    }
}
