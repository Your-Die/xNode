using Chinchillada.NodeGraph;
using UnityEngine;

namespace Values
{
    public abstract class ReferenceNode<T> : OutputNode<T>
    {
        [SerializeField] [Input] private T input;
        
        [SerializeField] [Input] private bool copyInstance;
        
        protected override T UpdateOutput()
        {
            this.UpdateInput(ref this.input, nameof(this.input));

            return this.copyInstance
                ? this.Copy(this.input)
                : this.input;
        }

        protected abstract T Copy(T item);
    }
}