using UnityEngine;

/// <summary>
/// Script'lerdeki statik durumlar sahne değişince kendiliğinden sıfırlanmaz: eğitim sahnesinden sınava
/// geçince "başla butonuna basıldı", "göçük sürüyor" ve ekipman bilgileri taşınıyordu. Her sahne
/// yüklendiğinde (sahne objelerinin Start'ından önce) hepsi burada sıfırlanır.
/// </summary>
public static class StaticStateReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => ResetAll();
    }

    public static void ResetAll()
    {
        MiningSequence.isMiningStarted = false;
        MiningSequence.isEducationSceneFinish = false;
        MiningSequence.isButtonPressed = false;
        TakeEquipment.ResetState();
        SafeZone.ResetState();
    }
}
