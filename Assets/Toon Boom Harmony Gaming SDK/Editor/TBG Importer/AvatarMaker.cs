using UnityEditor;
using UnityEngine;
 
namespace ToonBoom
{
    public class AvatarMaker
    {
        [MenuItem("Assets/Harmony/Make AvatarMask from Selected")]
        private static void MakeAvatarMask()
        {
            GameObject activeGameObject = Selection.activeGameObject;
 
            if (activeGameObject != null)
            {
                AvatarMask avatarMask = new AvatarMask();
 
                avatarMask.AddTransformPath(activeGameObject.transform);
 
                var path = string.Format("Assets/{0}.mask", activeGameObject.name.Replace(':', '_'));
                AssetDatabase.CreateAsset(avatarMask, path);
            }
        }
    }
}
 