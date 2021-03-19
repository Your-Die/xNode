using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Chinchillada.NodeGraph
{
    using XNode;

    [CreateAssetMenu(menuName = "Scrobs/Graphs/Generator Graph")]
    public class OutputGraph : NodeGraph
    {
        [SerializeField] [InlineEditor] private List<IOutputNode> outputs = new List<IOutputNode>();

        private Dictionary<Type, IOutputNode> outputLookup;

        private IInitializableNode[] initializableNodes;

        public bool TryGetOutput<T>(out T value)
        {
            Initialize();

            if (this.outputLookup.TryGetValue(typeof(T), out var node))
            {
                var outputNode = (IOutputNode<T>) node;
                value = outputNode.GetOutput();
                return true;
            }

            value = default;
            return false;
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