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



    // ─── OPTİMAL SÜRE (toplam 20) ──────────────────────────
    private bool optimalTimeStarted = false;
    private float optimalTimeScore = 20f;
    private float optimalTimeElapsed = 0f;
    public float optimalTimeDuration = 10f;
    private float penaltyTimer = 0f;
    private bool isInOptimalZone = false;
    private bool optimalTimeScored = false;
    
    //score text 
    public TextMeshProUGUI scoreText;
    public bool IsInSafeZone => isInAnySafeZone;
    void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    void Update()
    {
        if (!optimalTimeStarted || optimalTimeScored) return;

        optimalTimeElapsed += Time.deltaTime;
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

        totalScore += scorePerEquipment;
        Debug.Log(equipmentName + " giyildi: +" + scorePerEquipment.ToString("F1") + " | Toplam: " + totalScore.ToString("F1"));
        GetScoreText(); // totalScore güncellendikten sonra çağır
    }
    // ─── Güvenli Bölge (toplam 40) ─────────────────────
    public void EnterSafeZone(bool hasExitedOnce)
    {
        if (safeZoneFinalized) return;
        isInAnySafeZone = true;

        if (!safeZoneEnterScored)
        {
            safeZoneEnterScored = true;
            safeZoneScore += 20f;
            Debug.Log("Safe zone'a girildi: +20 | Safe zone puanı: " + safeZoneScore);
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
        if (safeZoneFinalized) return;
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
        safeZoneScore = Mathf.Min(maxSafeZoneScore, safeZoneScore + 20f);
        Debug.Log("Çök & tut yapıldı: +20 | Safe zone puanı: " + safeZoneScore);
        GetScoreText();

    }

    public void FinalizeSafeZoneScore()
    {
        if (safeZoneFinalized) return;
        safeZoneFinalized = true;
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
        isInOptimalZone = true;
        FinalizeOptimalScore();
    }

 

    private void FinalizeOptimalScore()
    {
        Debug.Log("finalaze optimal score");
        if (optimalTimeScored) return;
        optimalTimeScored = true;

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
        Debug.Log("Maden çökerken girildi! Optimal süre puanı: 0");
    }

    // ─── TOPLAM PUAN ───────────────────────────────────────

    public float GetTotalScore()
    {
        return totalScore;

    }

    public void GetScoreText()
    {
        scoreText.text = "Puan: " + totalScore.ToString("F1");
    }
}