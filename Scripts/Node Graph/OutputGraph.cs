using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Chinchillada.NodeGraph
{
    using xNode;

    [CreateAssetMenu(menuName = "Scrobs/Graphs/Generator Graph")]
    public class OutputGraph : NodeGraph
    {
        [SerializeField] [InlineEditor] private List<IOutputNode> outputs = new List<IOutputNode>();

        private Dictionary<Type, IOutputNode> outputLookup;

        private IInitializableNode[] initializableNodes;

        public bool TryGetOutput<T>(out T value)
        {
            this.Initialize();

            if (this.outputLookup.TryGetValue(typeof(T), out var node))
            {
                var outputNode = (IOutputNode<T>) node;
                value = outputNode.GetOutput();
                return true;
            }

            value = default;
            return false;
        }

        public void AddOutput<T>(IOutputNode<T> node)
        {
            if (this.outputs.Contains(node))
                return;

            this.outputs.Add(node);

            if (this.outputLookup != null)
                this.outputLookup[typeof(T)] = node;
        }

        public void RemoveOutput<T>(OutputNode<T> node)
        {
            if (!this.outputs.Remove(node) || this.outputLookup == null)
                return;

            var type = typeof(T);

            // Remove the node form the lookup as well.
            if (this.outputLookup.TryGetValue(type, out var outputNode) && ReferenceEquals(outputNode, node))
                this.outputLookup.Remove(type);
        }

        private void Initialize()
        {
            foreach (var node in this.initializableNodes)
                node.Initialize();
        }

        private void OnEnable()
        {
            this.outputLookup       = this.outputs.ToDictionary(output => output.OutputType);
            this.initializableNodes = this.nodes.OfType<IInitializableNode>().ToArray();
        }


    }
}