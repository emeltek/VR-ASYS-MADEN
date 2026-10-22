using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ahşap destek direğinin yanındaki güvenli bölge. Sahnede çok sayıda olduğu için doluluk ortak bir
/// sayaçla tutulur: bir bölgeden komşu bölgeye geçerken oyuncu "dışarıda" sayılmaz. Bölge dışı cezası
/// her bölgede ayrı ayrı değil, ScoreManager'da tek yerden işler.
/// </summary>
public class SafeZone : MonoBehaviour
{
    private static int insideCount;

    /// <summary>Oyuncu herhangi bir güvenli bölgenin içinde mi.</summary>
    public static bool IsPlayerInside => insideCount > 0;
    public static bool isPlayerInSafeZone => IsPlayerInside;
    /// <summary>Göçük sırasında en az bir kez güvenli bölgeden çıkıldı mı.</summary>
    public static bool HasExitedDuringMining { get; private set; }

    public static void ResetState()
    {
        insideCount = 0;
        HasExitedDuringMining = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Doluluk göçükten önce de izlenir: göçük başladığında zaten içerideki oyuncu dışarıda sayılmasın
        bool wasInside = IsPlayerInside;
        insideCount++;
        if (!wasInside && MiningSequence.isMiningStarted)
            ScoreManager.Instance.EnterSafeZone(HasExitedDuringMining);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        insideCount = Math.Max(0, insideCount - 1);
        if (IsPlayerInside || !MiningSequence.isMiningStarted) return;

        HasExitedDuringMining = true; // sadece çökme sırasında çıkarsa
        ScoreManager.Instance.ExitSafeZone();
    }
}
