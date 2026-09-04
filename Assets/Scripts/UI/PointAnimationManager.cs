using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Anima moedas de Break Points viajando até o contador do HUB.
/// Todo o fluxo visual é controlado pelo DOTween, sem corrotinas de animação.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class PointAnimationManager : MonoBehaviour
{
    public static PointAnimationManager Instance { get; private set; }

    [Header("Referências")]
    [Tooltip("Prefab utilizado por cada moeda de Break Points.")]
    [SerializeField] private GameObject coinPrefab;

    [Tooltip("Texto TMP que exibe o saldo atual de Break Points.")]
    [SerializeField] private TMP_Text pointsLabel;

    public TMP_Text PointsLabel => pointsLabel;

    [Tooltip("RectTransform do Canvas usado como pai das moedas em movimento.")]
    [SerializeField] private RectTransform canvasRect;

    [Tooltip("Origem visual da recompensa no HUB (normalmente o card do minigame).")]
    [SerializeField] private RectTransform spawnAnchor;

    [Header("Animação das moedas")]
    [SerializeField, Min(0.1f)] private float duration = 0.8f;
    [SerializeField, Min(0f)] private float spawnDelay = 0.05f;
    [SerializeField] private Vector2 arcOffset = new Vector2(0f, 150f);
    [SerializeField, Min(0f)] private float spawnSpread = 18f;

    [Header("Pulso do contador")]
    [Tooltip("Multiplicador aplicado ao contador sempre que uma moeda chega.")]
    [SerializeField, Min(1f)] private float counterPulseScale = 1.12f;

    [Tooltip("Tempo usado para ampliar o contador.")]
    [SerializeField, Min(0.01f)] private float counterPulseUpDuration = 0.04f;

    [Tooltip("Tempo usado para devolver o contador à escala configurada.")]
    [SerializeField, Min(0.01f)] private float counterPulseDownDuration = 0.05f;

    [SerializeField] private Ease counterPulseUpEase = Ease.OutQuad;
    [SerializeField] private Ease counterPulseDownEase = Ease.InOutQuad;

    [Tooltip("Quantidade de moedas preparadas antes da primeira animação.")]
    [SerializeField, Min(0)] private int prewarmCoinCount = 20;

    private readonly List<GameObject> _activeCoins = new List<GameObject>(20);
    private readonly Queue<GameObject> _coinPool = new Queue<GameObject>(20);
    private Sequence _batchSequence;
    private Sequence _counterPulseSequence;
    private Vector3 _pointsLabelBaseScale = Vector3.one;
    private Vector3 _coinBaseScale = Vector3.one;
    private Quaternion _coinBaseRotation = Quaternion.identity;
    private int _coinsLanded;
    private int _expectedCoins;

    public bool IsAnimating { get; private set; }
    public event Action OnAnimationComplete;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (coinPrefab == null)
            Debug.LogError("[PointAnimationManager] O prefab da moeda não foi atribuído.", this);
        if (pointsLabel == null)
            Debug.LogError("[PointAnimationManager] O texto TMP dos pontos não foi atribuído.", this);
        if (canvasRect == null)
            Debug.LogError("[PointAnimationManager] O RectTransform do Canvas não foi atribuído.", this);

        if (pointsLabel != null)
            _pointsLabelBaseScale = pointsLabel.transform.localScale;

        if (coinPrefab != null)
        {
            _coinBaseScale = coinPrefab.transform.localScale;
            _coinBaseRotation = coinPrefab.transform.localRotation;
            PrewarmCoinPool();
        }
    }

    private void PrewarmCoinPool()
    {
        if (prewarmCoinCount <= 0 || canvasRect == null)
            return;

        for (int i = 0; i < prewarmCoinCount; i++)
        {
            GameObject coin = Instantiate(coinPrefab, canvasRect);
            coin.transform.localScale = _coinBaseScale;
            coin.transform.localRotation = _coinBaseRotation;
            coin.SetActive(false);
            _coinPool.Enqueue(coin);
        }
    }

    /// <summary>
    /// Anima um lote de pontos. Cada moeda aumenta o valor exibido em uma unidade
    /// e dispara um pulso completo no contador ao alcançar o texto.
    /// </summary>
    public void AnimatePoints(int baseValue, int amount)
    {
        if (amount <= 0 || coinPrefab == null || pointsLabel == null || canvasRect == null)
            return;

        StopCurrentAnimation(false);

        _coinsLanded = 0;
        _expectedCoins = amount;
        IsAnimating = true;
        pointsLabel.SetText("{0} PB", baseValue);
        pointsLabel.transform.localScale = _pointsLabelBaseScale;

        // O som acompanha o início do lote, evitando sobreposição do mesmo efeito
        // a cada moeda enquanto várias moedas chegam em sequência.
        AudioManager.Instance?.PlayReward();

        _batchSequence = DOTween.Sequence().SetTarget(this);

        for (int i = 0; i < amount; i++)
        {
            float spawnTime = i * spawnDelay;
            _batchSequence.InsertCallback(spawnTime, () => SpawnCoin(baseValue));
        }

    }

    private void SpawnCoin(int baseValue)
    {
        if (!IsAnimating || coinPrefab == null || pointsLabel == null || canvasRect == null)
            return;

        Vector2 spawnPos = GetCanvasLocalPoint(spawnAnchor != null ? spawnAnchor : canvasRect)
            + UnityEngine.Random.insideUnitCircle * spawnSpread;
        Vector2 targetPos = GetCanvasLocalPoint(pointsLabel.rectTransform);

        // O destino e a origem são convertidos para o mesmo espaço local do Canvas.
        // O movimento é aplicado por anchoredPosition (e não localPosition), evitando
        // o erro clássico de misturar coordenadas de RectTransform com Transform.
        Vector2 midPoint = (spawnPos + targetPos) * 0.5f + arcOffset;

        GameObject coin = GetCoin();
        _activeCoins.Add(coin);
        RectTransform coinRect = coin.transform as RectTransform;
        if (coinRect == null)
        {
            ReleaseCoin(coin);
            return;
        }

        coinRect.anchorMin = new Vector2(0.5f, 0.5f);
        coinRect.anchorMax = new Vector2(0.5f, 0.5f);
        coinRect.anchoredPosition = spawnPos;

        Sequence coinSequence = DOTween.Sequence().SetTarget(this);
        coinSequence.Append(
            DOTween.To(
                () => 0f,
                t => coinRect.anchoredPosition = EvaluateQuadraticBezier(spawnPos, midPoint, targetPos, t),
                1f,
                duration)
                .SetEase(Ease.InOutCubic));
        coinSequence.Join(coin.transform.DORotate(new Vector3(0f, 0f, 360f), duration, RotateMode.FastBeyond360));
        coinSequence.Join(
            coin.transform.DOScale(_coinBaseScale * 1.12f, duration * 0.3f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo));

        coinSequence.OnComplete(() =>
        {
            if (!IsAnimating)
            {
                ReleaseCoin(coin);
                return;
            }

            _coinsLanded++;
            pointsLabel.SetText("{0} PB", baseValue + _coinsLanded);
            bool isFinalCoin = _coinsLanded >= _expectedCoins;
            PlayCounterPulse(isFinalCoin);
            ReleaseCoin(coin);
        });
    }


    private static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        t = Mathf.Clamp01(t);
        float inverse = 1f - t;
        return inverse * inverse * start
            + 2f * inverse * t * control
            + t * t * end;
    }

    private Vector2 GetCanvasLocalPoint(RectTransform source)
    {
        if (source == null || canvasRect == null)
            return Vector2.zero;

        Canvas canvas = canvasRect.GetComponent<Canvas>();
        if (canvas == null)
            canvas = canvasRect.GetComponentInParent<Canvas>();

        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, source.position);

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            camera,
            out Vector2 localPoint)
            ? localPoint
            : Vector2.zero;
    }

    /// <summary>
    /// Executa um pulso DOTween para cada moeda recebida:
    /// escala original, escala ampliada e retorno à escala original.
    /// </summary>
    private void PlayCounterPulse(bool finishBatchAfterPulse)
    {
        if (pointsLabel == null)
            return;

        Transform labelTransform = pointsLabel.transform;

        _counterPulseSequence?.Kill();
        labelTransform.DOKill();
        labelTransform.localScale = _pointsLabelBaseScale;

        Vector3 enlargedScale = _pointsLabelBaseScale * counterPulseScale;

        _counterPulseSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(labelTransform.DOScale(enlargedScale, counterPulseUpDuration).SetEase(counterPulseUpEase))
            .Append(labelTransform.DOScale(_pointsLabelBaseScale, counterPulseDownDuration).SetEase(counterPulseDownEase))
            .OnComplete(() =>
            {
                if (pointsLabel != null)
                    pointsLabel.transform.localScale = _pointsLabelBaseScale;

                _counterPulseSequence = null;

                if (finishBatchAfterPulse)
                    FinishBatch();
            });
    }

    private void FinishBatch()
    {
        if (!IsAnimating)
            return;

        ResetCounterScale();
        IsAnimating = false;
        _batchSequence = null;
        OnAnimationComplete?.Invoke();
    }

    private void StopCurrentAnimation(bool notifyComplete)
    {
        bool wasAnimating = IsAnimating;
        IsAnimating = false;

        DOTween.Kill(this);
        _batchSequence = null;
        _counterPulseSequence = null;

        for (int i = _activeCoins.Count - 1; i >= 0; i--)
            ReturnCoinToPool(_activeCoins[i]);

        _activeCoins.Clear();
        ResetCounterScale();

        if (notifyComplete && wasAnimating)
            OnAnimationComplete?.Invoke();
    }

    private void ResetCounterScale()
    {
        if (pointsLabel == null)
            return;

        pointsLabel.transform.DOKill();
        pointsLabel.transform.localScale = _pointsLabelBaseScale;
    }

    private GameObject GetCoin()
    {
        GameObject coin = null;

        while (_coinPool.Count > 0 && coin == null)
            coin = _coinPool.Dequeue();

        if (coin == null)
        {
            coin = Instantiate(coinPrefab, canvasRect);
        }
        else
        {
            coin.transform.SetParent(canvasRect, false);
            coin.transform.localScale = _coinBaseScale;
            coin.transform.localRotation = _coinBaseRotation;
            coin.SetActive(true);
        }

        return coin;
    }

    private void ReleaseCoin(GameObject coin)
    {
        if (coin == null)
            return;

        _activeCoins.Remove(coin);
        ReturnCoinToPool(coin);
    }

    private void ReturnCoinToPool(GameObject coin)
    {
        if (coin == null)
            return;

        coin.transform.DOKill();
        coin.transform.SetParent(canvasRect, false);

        coin.transform.localScale = _coinBaseScale;
        coin.transform.localRotation = _coinBaseRotation;

        coin.SetActive(false);
        _coinPool.Enqueue(coin);
    }

    private void OnDisable()
    {
        StopCurrentAnimation(false);
    }

    private void OnDestroy()
    {
        StopCurrentAnimation(false);

        if (Instance == this)
            Instance = null;
    }
}
