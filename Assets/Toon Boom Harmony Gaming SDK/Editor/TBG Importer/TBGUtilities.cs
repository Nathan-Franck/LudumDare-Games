#if ENABLE_UNITY_2D_ANIMATION && ENABLE_UNITY_COLLECTIONS

using UnityEngine;
using System.Collections.Generic;
using System;

namespace ToonBoom.TBGImporter
{
    public static class TBGUtilities
    {
        public static void DecomposeMatrixTo2D(Matrix4x4 matrix, out Vector2 translation, out float rotation, out Vector2 scale, out float skew)
        {
            var a = matrix.m00;
            var b = matrix.m10;
            var c = matrix.m01;
            var d = matrix.m11;
            var e = matrix.m03;
            var f = matrix.m13;

            var delta = a * d - b * c;

            translation = new Vector2(e, f);
            rotation = 0;
            scale = new Vector2(0, 0);
            skew = 0;

            if (a != 0 || b != 0)
            {
                var r = Mathf.Sqrt(a * a + b * b);
                rotation = (b > 0 ? Mathf.Acos(a / r) : -Mathf.Acos(a / r)) * Mathf.Rad2Deg;
                skew = Mathf.Atan((a * c + b * d) / (r * r));
                scale = new Vector2(r, delta / r * Mathf.Cos(skew));
            }
        }

        public class DisabledInitBlock<T> : IDisposable where T : Component
        {
            readonly bool wasActive;
            readonly GameObject gameObject;
            readonly public T component;
            public DisabledInitBlock(GameObject gameObject)
            {
                this.gameObject = gameObject;
                wasActive = gameObject.activeSelf;
                gameObject.SetActive(false);
                component = gameObject.AddComponent<T>();
            }
            public void Dispose()
            {
                gameObject.SetActive(wasActive);
            }
        }
        
        /// <summary>
        /// Initialize the contents of a component while it's parent gameObject is disabled. GameObject is re-enabled after initialization block is disposed (end of a 'using' block).
        /// </summary>
        public static DisabledInitBlock<T> AddComponentAndInit<T>(this GameObject gameObject) where T : Component
        {
            return new DisabledInitBlock<T>(gameObject);
        }
        public static TValue GetValueOrDefault<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key)
        {
            return dictionary.TryGetValue(key, out var value) ? value : default(TValue);
        }
    }
}

#endif