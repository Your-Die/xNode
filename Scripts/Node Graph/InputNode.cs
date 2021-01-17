using Sirenix.OdinInspector;
using UnityEngine;

namespace Chinchillada.NodeGraph
{
    public class InputNode<T> : OutputNode<T>
    {
        [SerializeField] [Input] private T fallback;

        [ShowInInspector, ReadOnly] private T value;

        [ShowInInspector, ReadOnly] private bool hasValue;

        public T Value
        {
            get => this.value;
            set
            {
                this.value = value;
                this.hasValue = true;
            }
        }
        
        public void ResetValue()
        {
            this.value = default;
            this.hasValue = false;
        }
        
        protected override T UpdateOutput()
        {
            return this.hasValue
                ? this.value
                : this.UpdateFallBack();
        }

        private T UpdateFallBack() => this.UpdateInput(ref this.fallback, nameof(this.fallback));
    }
}