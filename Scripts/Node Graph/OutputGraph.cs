using System.Linq;
using UnityEngine;
using XNode;

namespace Chinchillada.NodeGraph
{
    public class OutputGraph<TOutputNode> : XNode.NodeGraph where TOutputNode : Node
    {
        [SerializeField] private TOutputNode outputNode;

        protected TOutputNode OutputNode => this.outputNode;

        private void OnValidate()
        {
            if (this.OutputNode != null)
                return;

            var functionNodes = this.nodes.OfType<TOutputNode>();
            var leafNodes = functionNodes.Where(IsLeaf);


            var bestScore = float.MinValue;
            foreach (var node in leafNodes)
            {
                var score = node.position.x;
                if (score <= bestScore)
                    continue;
                
                this.outputNode = node;
                bestScore       = score;
            }

            static bool IsLeaf(TOutputNode node) => !node.Outputs.Any(port => port.IsConnected);
        }
    }
}