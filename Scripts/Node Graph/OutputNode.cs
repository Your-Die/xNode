using Sirenix.OdinInspector;
using UnityEngine;
using xNode;

namespace Chinchillada.NodeGraph
{
    using System;

    public interface IOutputNode
    {
        Type OutputType { get; }
    }

    public interface IOutputNode<T> : IOutputNode
    {
        T GetOutput();
    }

    public abstract class OutputNode<T> : Node, IOutputNode<T>
    {
        [SerializeField] [Output] private T output;


        protected string OutputFieldName => nameof(this.output);

        public T Output => this.output;

        public Type OutputType => typeof(T);

        public override object GetValue(NodePort port)
        {
            return port.fieldName == this.OutputFieldName
                ? this.GetOutput()
                : base.GetValue(port);
        }

        [Button]
        public T GetOutput()
        {
            this.output = this.UpdateOutput();
#if UNITY_EDITOR
            this.UpdatePreview(this.output);
#endif
            return this.output;
        }

        protected abstract T UpdateOutput();

        protected virtual void UpdatePreview(T value) { }
    }
}