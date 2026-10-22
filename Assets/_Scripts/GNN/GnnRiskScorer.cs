using UnityEngine;

/// <summary>
/// Maden göçüğü sınavını GNN risk haritasıyla puanlar. Etkinken ScoreManager'ın eski bonus puanlaması
/// (ekipman +40, güvenli bölge +40, süre +20) kapanır; puan 100'den başlar ve şu kalemlerden düşer:
///
///   1) Kişisel koruyucu donanım: göçük başladığında biri bile eksikse ceza; eksikken göçük cezaları × çarpan
///   2) Göçük sırasında açıkta kalma:   bütçe × (1 − e^(−E_açık / τ_açık)),  E_açık = ∫ P(Açık) dt
///   3) Düşen taşların altına girme:    bütçe × [girişPayı × w_giriş + (1 − girişPayı) × (1 − e^(−E / τ))]
///   4) Çök &amp; tut: göçük bitene kadar güvenli bölgede yapılmadıysa ceza
///   5) Tahliye: göçük bitince toplanma alanına, tepki süresinden sonra gecikmeyle artan ceza
///
/// Oyuncu bir SafeZone kutusunun içindeyse (tasarımcının çizdiği güvenli bölge) model ne derse desin güvende sayılır.
/// Toplanma alanına girilince ya da süre dolunca puan kesinleşir ve standart risk etiketi gösterilir.
/// </summary>
public class GnnRiskScorer : MonoBehaviour
{
    private static GnnRiskScorer active;

    /// <summary>true iken eski bonus puanlaması kapalıdır. Model / graf yüklenemediyse false kalır.</summary>
    public static bool IsScoring => active != null && active.riskPredictor != null && active.riskPredictor.IsReady;

    [Header("Referanslar (boşsa otomatik bulunur)")]
    [SerializeField] private GnnRiskPredictor riskPredictor;
    [Tooltip("Konumu puanlanan obje. Boşsa ana kamera (oyuncunun başı)")]
    [SerializeField] private Transform trackedTarget;

    [Header("Başlangıç")]
    [SerializeField] private int startingScore = 100;

    [Header("1) Kişisel Koruyucu Donanım")]
    [Tooltip("Göçük başladığında 7 ekipmandan biri bile eksikse kesilen puan")]
    [SerializeField] private float missingPpePenalty = 30f;
    [Tooltip("Ekipman eksikken göçük cezaları bu katsayıyla çarpılır")]
    [SerializeField] private float missingPpeMultiplier = 1.5f;

    [Header("2) Göçük Sırasında Açıkta Kalma")]
    [SerializeField] private float exposedBudget = 30f;
    [SerializeField] private float exposedTimeConstant = 10f;

    [Header("3) Düşen Taşların Altı")]
    [SerializeField] private float dangerBudget = 40f;
    [Tooltip("Bütçenin ilk girişte hemen kesilen payı")]
    [SerializeField, Range(0f, 1f)] private float entryShare = 0.4f;
    [SerializeField] private float dangerTimeConstant = 5f;

    [Header("4) Çök & Tut")]
    [SerializeField] private float noPosturePenalty = 20f;

    [Header("5) Tahliye")]
    [SerializeField] private float evacuationBudget = 20f;
    [Tooltip("Göçük bittikten sonra bu süreye (s) kadar ceza yok. Boşsa ScoreManager > Optimal Time Duration")]
    [SerializeField] private float evacuationGracePeriod = -1f;
    [SerializeField] private float evacuationTimeConstant = 15f;
    [Tooltip("Toplanma alanına bu süre (s) içinde gidilmezse tam ceza kesilir ve puan kesinleşir")]
    [SerializeField] private float evacuationTimeout = 60f;

    [Header("Zamanlama")]
    [SerializeField] private float updateInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private enum Phase { BeforeCollapse, Collapse, Evacuation, Done }

    private Phase phase = Phase.BeforeCollapse;
    private float nextUpdate;
    private float lastTick = float.NegativeInfinity;
    private float hazardMultiplier = 1f;

    private float exposedExposure;
    private int exposedDeducted;

    private bool dangerEntered;
    private float dangerEntryWeight;
    private float dangerExposure;
    private int dangerDeducted;
    private bool wasInDanger;

    private float evacuationStart;

    private void Start()
    {
        if (riskPredictor == null) riskPredictor = GnnRiskPredictor.Instance;
        if (riskPredictor == null) riskPredictor = FindObjectOfType<GnnRiskPredictor>();
        if (riskPredictor == null) riskPredictor = gameObject.AddComponent<GnnRiskPredictor>();
        if (trackedTarget == null && Camera.main != null) trackedTarget = Camera.main.transform;
    }

    private void OnEnable() => active = this;

    private void OnDisable()
    {
        if (active == this) active = null;
    }

    private void Update()
    {
        ScoreManager score = ScoreManager.Instance;
        if (!IsScoring || score == null || score.IsFinalized || trackedTarget == null) return;

        if (!score.UsesDeductionModel) score.BeginDeductionModel(startingScore);

        switch (phase)
        {
            case Phase.BeforeCollapse:
                if (MiningSequence.isMiningStarted) BeginCollapse(score);
                break;

            case Phase.Collapse:
                if (!MiningSequence.isMiningStarted) { EndCollapse(score); break; }
                if (Time.time < nextUpdate) break;
                nextUpdate = Time.time + updateInterval;
                float dt = Mathf.Min(Time.time - lastTick, updateInterval * 2f);
                lastTick = Time.time;
                ScoreCollapse(score, dt);
                break;

            case Phase.Evacuation:
                UpdateEvacuation(score);
                break;
        }
    }

    private void BeginCollapse(ScoreManager score)
    {
        phase = Phase.Collapse;
        lastTick = Time.time;
        if (score.AllEquipmentWorn) return;

        hazardMultiplier = missingPpeMultiplier;
        Deduct(score, missingPpePenalty, "Kişisel koruyucu donanım eksik");
    }

    private void ScoreCollapse(ScoreManager score, float dt)
    {
        // Toplanma alanına göçük sürerken kaçmak: tahliye bütçesinin tamamı
        if (score.OptimalZoneEnteredDuringCollapse && phase == Phase.Collapse && evacuationBudget > 0f && score.OptimalZoneEnteredAt >= 0f)
        {
            Deduct(score, evacuationBudget, "Göçük sürerken toplanma alanına koşuldu");
            evacuationBudget = 0f;
        }

        if (SafeZone.IsPlayerInside) { wasInDanger = false; return; }

        GnnPrediction p = riskPredictor.GetPrediction(trackedTarget.position);
        float exposed = p.IsValid ? p.ExposedProbability : 1f;
        float danger = p.IsValid ? p.DangerProbability : 0f;

        // Açıkta kalma
        exposedExposure += exposed * dt;
        int exposedTarget = Mathf.RoundToInt(exposedBudget * hazardMultiplier * (1f - Mathf.Exp(-exposedExposure / exposedTimeConstant)));
        if (exposedTarget > exposedDeducted)
        {
            Deduct(score, exposedTarget - exposedDeducted, $"Açıkta kalma (p={exposed:F2}, maruziyet={exposedExposure:F1} s)");
            exposedDeducted = exposedTarget;
        }

        // Düşen taşların altı
        bool inDanger = p.IsValid && p.RiskClass == GnnRiskClass.Danger;
        bool entering = inDanger && !wasInDanger;
        wasInDanger = inDanger;
        if (!inDanger) return;

        if (!dangerEntered)
        {
            dangerEntered = true;
            dangerEntryWeight = danger;
        }
        else if (!entering)
        {
            dangerExposure += danger * dt;
        }

        int dangerTarget = Mathf.RoundToInt(dangerBudget * hazardMultiplier *
            (entryShare * dangerEntryWeight + (1f - entryShare) * (1f - Mathf.Exp(-dangerExposure / dangerTimeConstant))));
        if (dangerTarget > dangerDeducted)
        {
            Deduct(score, dangerTarget - dangerDeducted, $"Düşen taşların altı (p={danger:F2}, maruziyet={dangerExposure:F1} s)");
            dangerDeducted = dangerTarget;
        }
    }

    private void EndCollapse(ScoreManager score)
    {
        if (!score.SafePositionDone) Deduct(score, noPosturePenalty, "Güvenli bölgede çök & tut yapılmadı");

        if (score.OptimalZoneEnteredDuringCollapse)
        {
            // Tahliye zaten (yanlış zamanda) yapıldı: puan kesinleşir
            Finish(score);
            return;
        }

        phase = Phase.Evacuation;
        evacuationStart = Time.time;
    }

    private void UpdateEvacuation(ScoreManager score)
    {
        float grace = evacuationGracePeriod >= 0f ? evacuationGracePeriod : score.optimalTimeDuration;
        bool arrived = score.OptimalZoneEnteredAt >= 0f;
        float elapsed = (arrived ? score.OptimalZoneEnteredAt : Time.time) - evacuationStart;

        if (!arrived && elapsed < grace + evacuationTimeout) return;

        float penalty = arrived
            ? evacuationBudget * (1f - Mathf.Exp(-Mathf.Max(0f, elapsed - grace) / evacuationTimeConstant))
            : evacuationBudget;
        Deduct(score, Mathf.RoundToInt(penalty), arrived ? $"Tahliye {elapsed:F0} sn sürdü (tepki süresi {grace:F0} sn)" : "Toplanma alanına gidilmedi");
        Finish(score);
    }

    private void Finish(ScoreManager score)
    {
        phase = Phase.Done;
        score.FinalizeScore();
    }

    private void Deduct(ScoreManager score, float amount, string reason)
    {
        if (amount <= 0f) return;
        score.Deduct(amount);
        if (showDebugLogs)
            Debug.Log($"<color=red>[GNN] {reason}: -{amount:F0} → Puan: {Mathf.RoundToInt(score.GetTotalScore())}</color>");
    }
}
