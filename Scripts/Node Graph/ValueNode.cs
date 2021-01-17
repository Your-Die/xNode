using Sirenix.OdinInspector;
using UnityEngine;

namespace Chinchillada.NodeGraph
{
    public class ValueNode<T> : OutputNode<T>
    {
        [SerializeField] [Input] private T input;

        [ShowInInspector, ReadOnly] private string preview;

        protected override T UpdateOutput()
        {
            this.UpdateInput(ref this.input, nameof(this.input));
            return this.input;
        }

        protected override void UpdatePreview(T value) => this.preview = value.ToString();
    }
}