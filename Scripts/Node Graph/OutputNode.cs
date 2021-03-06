﻿using Sirenix.OdinInspector;
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

        [SerializeField] [ OnValueChanged(nameof(UpdateGraphOutput))]
        private bool isGraphOutput;

        protected T Output => this.output;

        public Type OutputType => typeof(T);

        public override object GetValue(NodePort port)
        {
            return port.fieldName == nameof(this.output)
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

        protected virtual void UpdatePreview(T value)
        {
        }

        private void UpdateGraphOutput()
        {
            var outputGraph = this.graph as OutputGraph;

            if (this.isGraphOutput)
            {
                if (outputGraph != null)
                    outputGraph.AddOutput(this);
                else
                    this.isGraphOutput = false;
            }
            else if (outputGraph != null)
            {
                outputGraph.RemoveOutput(this);
            }
        }
    }
}