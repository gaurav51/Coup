using UnityEngine;
using UnityEngine.Rendering;

namespace Coup.Game
{
    public static class MobileFrameRate
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            OnDemandRendering.renderFrameInterval = 1;
        }
    }
}
