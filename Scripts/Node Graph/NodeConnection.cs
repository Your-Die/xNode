using System;

namespace xNode
{
    using UnityEngine;

    [Serializable]
    public abstract class NodeConnection<T>
    {
        [SerializeField]
        private T source;

        public T Source
        {
            get => this.source != null ? this.source : this.Null;
            set => this.source = value;
        }

        protected abstract T Null { get; }
    }
}