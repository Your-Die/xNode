using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace Chinchillada.NodeGraph
{
    public abstract class OutputNode<T> : Node
    {
        [SerializeField] [Output] private T output;

        
        protected string OutputFieldName => nameof(this.output);

        public T Output => this.output;


        public override object GetValue(NodePort port)
        {
            return port.fieldName == this.OutputFieldName
                ? this.GetOutput() 
                : base.GetValue(port);
        }

        public T GetOutput()
        {
            this.output = this.UpdateOutput();
#if UNITY_EDITOR
            this.UpdatePreview(this.output);
#endif
            return this.output;
        }

        [Button]
        protected abstract T UpdateOutput();

        protected virtual void UpdatePreview(T value) { }
    }
}