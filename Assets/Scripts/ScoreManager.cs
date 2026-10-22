using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    private float totalScore = 0f;

    // ─── EKİPMAN (toplam 40) ─────────────────────────────── doğru
    private bool helmetScored = false;
    private bool maskScored = false;
    private bool gogglesScored = false;
    private bool vestScored = false;
    private bool bootsScored = false;
    private bool leftGloveScored = false;
    private bool rightGloveScored = false;
    private const float scorePerEquipment = 40f / 7f;
    // ─── Güvenli Bölge (toplam 40) ─────────────────────

    private bool isInAnySafeZone = false;
    private float safeZoneScore = 0f;
    private const float maxSafeZoneScore = 40f;
    private bool safeZoneEnterScored = false;
    private bool safePositionScored = false;
    private bool safeZoneFinalized = false;
    private float safeZonePenaltyTimer = 0f;
    private const float safeZonePenaltyInterval = 3f;



    // ─── OPTİMAL SÜRE (toplam 20) ──────────────────────────
    private bool optimalTimeStarted = false;
    private float optimalTimeScore = 20f;
    private float optimalTimeElapsed = 0f;
    public float optimalTimeDuration = 10f;
    private bool isInOptimalZone = false;
    private bool optimalTimeScored = false;

    // ─── GNN PUANLAMASI (100'den düşen model) ──────────────
    [Header("Risk Etiketi (sonuçta puana göre gösterilir)")]
    [SerializeField] private int guvenliEsigi = 75;
    [SerializeField] private int ortaRiskEsigi = 50;
    [SerializeField] private int riskliEsigi = 25;

    /// <summary>true iken eski bonus puanlaması kapalıdır; puan GnnRiskScorer tarafından 100'den düşülür.</summary>
    public bool UsesDeductionModel { get; private set; }
    /// <summary>Sonuç kesinleşti: bundan sonra puan değişmez, risk etiketi gösterilir.</summary>
    public bool IsFinalized { get; private set; }
    /// <summary>Güvenli bölgede çök &amp; tut yapıldı mı (her iki modelde de kaydedilir).</summary>
    public bool SafePositionDone => safePositionScored;
    /// <summary>Toplanma alanına girildiği an (Time.time); girilmediyse -1.</summary>
    public float OptimalZoneEnteredAt { get; private set; } = -1f;
    /// <summary>Toplanma alanına göçük sürerken mi girildi.</summary>
    public bool OptimalZoneEnteredDuringCollapse { get; private set; }

    //score text
    public TextMeshProUGUI scoreText;
    public bool IsInSafeZone => SafeZone.IsPlayerInside;
    void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    void Update()
    {
        UpdateSafeZonePenalty();

        if (!optimalTimeStarted || optimalTimeScored) return;

        optimalTimeElapsed += Time.deltaTime;
    }

    /// <summary>
    /// Güvenli bölge dışı cezası tek yerden işler (önceden her SafeZone kendi sayacını çalıştırıyordu:
    /// 18 bölge = her 3 sn'de -90). Göçük başladığında oyuncu zaten bölgedeyse giriş de burada sayılır.
    /// </summary>
    private void UpdateSafeZonePenalty()
    {
        if (UsesDeductionModel || safeZoneFinalized || !MiningSequence.isMiningStarted) return;

        if (SafeZone.IsPlayerInside)
        {
            if (!safeZoneEnterScored) EnterSafeZone(SafeZone.HasExitedDuringMining);
            safeZonePenaltyTimer = 0f;
            return;
        }

        safeZonePenaltyTimer += Time.deltaTime;
        if (safeZonePenaltyTimer >= safeZonePenaltyInterval)
        {
            safeZonePenaltyTimer = 0f;
            PenalizeSafeZone();
        }
    }

    // ─── EKİPMAN ───────────────────────────────────────────

    public void AddEquipmentScore(string equipmentName)
    {
        switch (equipmentName)
        {
            case "Helmet":     if (helmetScored)     return; helmetScored     = true; break;
            case "Mask":       if (maskScored)       return; maskScored       = true; break;
            case "Goggles":    if (gogglesScored)    return; gogglesScored    = true; break;
            case "Vest":       if (vestScored)       return; vestScored       = true; break;
            case "Boot":       if (bootsScored)      return; bootsScored      = true; break;
            case "LeftGlove":  if (leftGloveScored)  return; leftGloveScored  = true; break;
            case "RightGlove": if (rightGloveScored) return; rightGloveScored = true; break;
            default: return;
        }

        if (UsesDeductionModel) return; // GNN modelinde ekipman, göçük başlarken bir kez değerlendirilir

        totalScore += scorePerEquipment;
        Debug.Log(equipmentName + " giyildi: +" + scorePerEquipment.ToString("F1") + " | Toplam: " + totalScore.ToString("F1"));
        GetScoreText(); // totalScore güncellendikten sonra çağır
    }

    /// <summary>7 kişisel koruyucu donanımın hepsi giyildi mi.</summary>
    public bool AllEquipmentWorn =>
        helmetScored && maskScored && gogglesScored && vestScored && bootsScored && leftGloveScored && rightGloveScored;

    // ─── Güvenli Bölge (toplam 40) ─────────────────────
    public void EnterSafeZone(bool hasExitedOnce)
    {
        if (safeZoneFinalized) return;
        isInAnySafeZone = true;

        if (!safeZoneEnterScored && !UsesDeductionModel)
        {
            safeZoneEnterScored = true;
            float earned = hasExitedOnce ? 10f : 20f;
            safeZoneScore += earned;
            Debug.Log("Safe zone'a girildi: +" + earned + " | Safe zone puanı: " + safeZoneScore);
            GetScoreText();
        }
    }

    public void ExitSafeZone()
    {
        if (safeZoneFinalized) return;
        isInAnySafeZone = false;
        Debug.Log("Safe zone'dan çıkıldı! Ceza başlıyor.");
        GetScoreText();

    }

    public void PenalizeSafeZone()
    {
        if (safeZoneFinalized || UsesDeductionModel) return;
        if (isInAnySafeZone) return;

        safeZoneScore = Mathf.Max(0f, safeZoneScore - 5f);
        Debug.Log("Safe zone cezası! Kalan puan: " + safeZoneScore);
        GetScoreText();

    }

    public void AddSafePositionScore()
    {
        if (safeZoneFinalized) return;
        if (safePositionScored) return;

        safePositionScored = true;
        if (UsesDeductionModel) return; // GNN modelinde yapılmazsa göçük sonunda ceza kesilir

        safeZoneScore = Mathf.Min(maxSafeZoneScore, safeZoneScore + 20f);
        Debug.Log("Çök & tut yapıldı: +20 | Safe zone puanı: " + safeZoneScore);
        GetScoreText();

    }

    public void FinalizeSafeZoneScore()
    {
        if (safeZoneFinalized) return;
        safeZoneFinalized = true;
        if (UsesDeductionModel) return;

        totalScore += safeZoneScore;
        Debug.Log("Safe zone tamamlandı: +" + safeZoneScore + " | Toplam: " + totalScore.ToString("F1"));
        GetScoreText();

    }

    // ─── OPTİMAL SÜRE ──────────────────────────────────────

    public void StartOptimalTimer()
    {
        if (optimalTimeStarted) return;
        optimalTimeStarted = true;
        Debug.Log("Optimal süre başladı.");
    }

    public void EnterOptimalZone()
    {
        if (optimalTimeScored) return;
        // Göçük bitmeden (süre başlamadan) toplanma alanına girmek puan vermez
        if (!optimalTimeStarted) return;

        isInOptimalZone = true;
        OptimalZoneEnteredAt = Time.time;
        FinalizeOptimalScore();
    }



    private void FinalizeOptimalScore()
    {
        Debug.Log("finalaze optimal score");
        if (optimalTimeScored) return;
        optimalTimeScored = true;
        if (UsesDeductionModel) return; // GNN modelinde tahliye süresi GnnRiskScorer'da değerlendirilir

        if (optimalTimeElapsed <= optimalTimeDuration)
        {
            // Zamanında girdi, tam puan
            optimalTimeScore = 20f;
        }
        else
        {
            // Geç girdi, her 3 saniye -5
            float extraTime = optimalTimeElapsed - optimalTimeDuration;
            int penalties = Mathf.FloorToInt(extraTime / 3f);
            optimalTimeScore = Mathf.Max(0f, 20f - (penalties * 5f));
        }

        totalScore += optimalTimeScore;
        Debug.Log("Optimal süre: " + optimalTimeElapsed.ToString("F1") + "sn | Puan: +" + optimalTimeScore + " | Toplam: " + totalScore.ToString("F1"));
        GetScoreText();
    }
    public void ZeroOptimalScore()
    {
        Debug.Log("zero optimal score girildi");

        if (optimalTimeScored) return;
        optimalTimeScored = true;
        optimalTimeScore = 0f;
        OptimalZoneEnteredAt = Time.time;
        OptimalZoneEnteredDuringCollapse = true;
        Debug.Log("Maden çökerken girildi! Optimal süre puanı: 0");
        GetScoreText();
    }

    // ─── GNN PUANLAMASI ────────────────────────────────────

    /// <summary>GnnRiskScorer çağırır: eski bonusları kapatır, puanı başlangıç değerine (100) ayarlar.</summary>
    public void BeginDeductionModel(float startingScore)
    {
        if (UsesDeductionModel) return;
        UsesDeductionModel = true;
        totalScore = startingScore;
        GetScoreText();
    }

    public void Deduct(float amount)
    {
        if (IsFinalized || amount <= 0f) return;
        totalScore = Mathf.Max(0f, totalScore - amount);
        GetScoreText();
    }

    /// <summary>Sonucu kesinleştirir: puan kilitlenir ve risk etiketi gösterilir.</summary>
    public void FinalizeScore()
    {
        if (IsFinalized) return;
        IsFinalized = true;
        GetScoreText();
        Debug.Log($"[Puan] Sonuç: {Mathf.RoundToInt(totalScore)} → {GetRiskLabel()}");
    }

    /// <summary>Puana göre risk etiketi: yüksek puan = doğru davranış = düşük risk.</summary>
    public string GetRiskLabel()
    {
        int s = Mathf.RoundToInt(totalScore);
        if (s >= guvenliEsigi) return "Güvenli";
        if (s >= ortaRiskEsigi) return "Orta Riskli";
        if (s >= riskliEsigi) return "Riskli";
        return "Yüksek Riskli";
    }

    private string GetRiskColor()
    {
        int s = Mathf.RoundToInt(totalScore);
        if (s >= guvenliEsigi) return "#3CC864";
        if (s >= ortaRiskEsigi) return "#F0B43C";
        if (s >= riskliEsigi) return "#F07832";
        return "#E04646";
    }

    // ─── TOPLAM PUAN ───────────────────────────────────────

    public float GetTotalScore()
    {
        return totalScore;

    }

    public void GetScoreText()
    {
        if (scoreText == null) return;
        scoreText.text = "Puan: " + Mathf.RoundToInt(totalScore);
        if (IsFinalized)
            scoreText.text += $"\n<size=75%><color={GetRiskColor()}>{GetRiskLabel()}</color></size>";
    }
}
