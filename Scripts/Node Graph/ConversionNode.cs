using Chinchillada.NodeGraph;
using UnityEngine;

namespace Values.Conversions
{
    public abstract class ConversionNode<TInput, TOutput> : OutputNode<TOutput>
    {
        [SerializeField] [Input] private TInput input;
        
        protected override TOutput UpdateOutput()
        {
            this.UpdateInput(ref this.input, nameof(this.input));
            return this.Convert(this.input);
        }

        protected abstract TOutput Convert(TInput input);
    }
}