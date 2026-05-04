using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    private float totalScore = 0f;

    // ─── EKİPMAN (toplam 40) ───────────────────────────────
    private bool helmetScored = false;
    private bool maskScored = false;
    private bool gogglesScored = false;
    private bool vestScored = false;
    private bool bootsScored = false;
    private bool leftGloveScored = false;
    private bool rightGloveScored = false;
    private const float scorePerEquipment = 40f / 7f;

    private bool isInAnySafeZone = false;
    private float safeZoneScore = 0f;
    private const float maxSafeZoneScore = 20f;
    private bool safeZoneEnterScored = false;
    private bool safePositionScored = false;
    private bool safeZoneFinalized = false;

    // ─── KONTROLLÜ HAREKET (toplam 20) ─────────────────────
    private float controlledMovementScore = 0f;
    private const float maxControlledMovementScore = 20f;
    private const float scorePerControlZone = 20f / 3f;
    private Dictionary<int, bool> controlZoneScored = new Dictionary<int, bool>();

    // ─── OPTİMAL SÜRE (toplam 20) ──────────────────────────
    private bool optimalTimeStarted = false;
    private float optimalTimeScore = 20f;
    private float optimalTimeElapsed = 0f;
    public float optimalTimeDuration = 10f;
    private float penaltyTimer = 0f;
    private bool isInOptimalZone = false;
    private bool optimalTimeScored = false;
    public bool IsInSafeZone => isInAnySafeZone;
    void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    void Update()
    {
        if (optimalTimeStarted && !optimalTimeScored)
        {
            optimalTimeElapsed += Time.deltaTime;

            if (isInOptimalZone)
            {
                if (optimalTimeElapsed >= optimalTimeDuration)
                    FinalizeOptimalScore();
            }
            else
            {
                penaltyTimer += Time.deltaTime;
                if (penaltyTimer >= 3f)
                {
                    penaltyTimer = 0f;
                    optimalTimeScore = Mathf.Max(0f, optimalTimeScore - 5f);
                    Debug.Log("Optimal süre cezası! Kalan puan: " + optimalTimeScore);

                    if (optimalTimeScore <= 0f)
                        FinalizeOptimalScore();
                }
            }
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

        totalScore += scorePerEquipment;
        Debug.Log(equipmentName + " giyildi: +" + scorePerEquipment.ToString("F1") + " | Toplam: " + totalScore.ToString("F1"));
    }

    public void EnterSafeZone()
    {
        if (safeZoneFinalized) return;
        isInAnySafeZone = true;

        if (!safeZoneEnterScored)
        {
            safeZoneEnterScored = true;
            safeZoneScore += 10f;
            Debug.Log("Safe zone'a girildi: +10 | Safe zone puanı: " + safeZoneScore);
        }
    }

    public void ExitSafeZone()
    {
        if (safeZoneFinalized) return;
        isInAnySafeZone = false;
        Debug.Log("Safe zone'dan çıkıldı! Ceza başlıyor.");
    }

    public void PenalizeSafeZone()
    {
        if (safeZoneFinalized) return;
        if (isInAnySafeZone) return;

        safeZoneScore = Mathf.Max(0f, safeZoneScore - 5f);
        Debug.Log("Safe zone cezası! Kalan puan: " + safeZoneScore);
    }

    public void AddSafePositionScore()
    {
        if (safeZoneFinalized) return;
        if (safePositionScored) return;

        safePositionScored = true;
        safeZoneScore = Mathf.Min(maxSafeZoneScore, safeZoneScore + 10f);
        Debug.Log("Çök & tut yapıldı: +10 | Safe zone puanı: " + safeZoneScore);
    }

    public void FinalizeSafeZoneScore()
    {
        if (safeZoneFinalized) return;
        safeZoneFinalized = true;
        totalScore += safeZoneScore;
        Debug.Log("Safe zone tamamlandı: +" + safeZoneScore + " | Toplam: " + totalScore.ToString("F1"));
    }
    // ─── KONTROLLÜ HAREKET ─────────────────────────────────

    public void AddControlZoneScore(int zoneId)
    {
        if (controlZoneScored.ContainsKey(zoneId)) return;
        if (controlledMovementScore >= maxControlledMovementScore) return;

        controlZoneScored[zoneId] = true;
        float earned = Mathf.Min(scorePerControlZone, maxControlledMovementScore - controlledMovementScore);
        controlledMovementScore += earned;
        totalScore += earned;
        Debug.Log("Kontrol Zone " + zoneId + ": +" + earned.ToString("F1") + " | Toplam: " + totalScore.ToString("F1"));
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
        isInOptimalZone = true;
        penaltyTimer = 0f;
    }

    public void ExitOptimalZone()
    {
        isInOptimalZone = false;
        penaltyTimer = 0f;
    }

    private void FinalizeOptimalScore()
    {
        optimalTimeScored = true;
        totalScore += optimalTimeScore;
        Debug.Log("Optimal süre tamamlandı: +" + optimalTimeScore.ToString("F1") + " | Toplam: " + totalScore.ToString("F1"));
    }

    // ─── TOPLAM PUAN ───────────────────────────────────────

    public float GetTotalScore()
    {
        return totalScore;
    }
}