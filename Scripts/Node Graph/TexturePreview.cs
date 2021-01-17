using System;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Function
{
    [Serializable]
    public abstract class TexturePreview<T>
    {

        [SerializeField] private FilterMode filterMode = FilterMode.Point;

        [SerializeField, ReadOnly, UsedImplicitly, HideLabel, PropertyOrder(Int32.MaxValue),
         PreviewField(100, ObjectFieldAlignment.Center)]
        private Texture2D texture;
        
        public abstract Vector2Int Resolution { get; }

        public void Preview(T content)
        {
            this.texture = new Texture2D(this.Resolution.x, this.Resolution.y)
            {
                filterMode = this.filterMode
            };

            this.BuildTexture(this.texture, content);
            this.texture.Apply();
        }

        protected abstract void BuildTexture(Texture2D texture, T content);
    }
}