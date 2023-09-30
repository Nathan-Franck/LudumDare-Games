using System;
using UnityEngine;
using UnityEngine.U2D;

namespace ToonBoom.TBGImporter
{
    [ExecuteInEditMode]
    public class TBGUserSettings : ScriptableObject
    {
        [Header("TBG Importer Defaults")]
        public Shader Shader;
        public bool CreateSpriteAtlas = true;
        public SpriteAtlas SpriteAtlas;
        [Header("Package Window")]
        public bool PackageWindowDontAskAgain;
        public long LastDismisalTime = 0;
        public static long TimeBetweenPackageRequests = 1000 * 30; // 1/2 minute :D
        public void OnEnable()
        {
            if (PackageWindowDontAskAgain)
                return;
            if (CurrentTime - LastDismisalTime > TimeBetweenPackageRequests)
            {
                TBGPackageWindow.CheckAndInit();
            }
        }
        long CurrentTime { get { return new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds(); } }
        public void Dismiss()
        {
            LastDismisalTime = CurrentTime;
        }
    }
}