using System;
using System.Collections.Generic;
using UnityEngine;

namespace xNode
{
    using Sirenix.OdinInspector;

    /// <summary> Base class for all node graphs </summary>
    [Serializable]
    public abstract class NodeGraph : SerializedScriptableObject
    {
        /// <summary> All nodes in the graph. <para/>
        /// See: <see cref="AddNode{T}"/> </summary>
        [SerializeField] public List<Node> nodes = new List<Node>();

        /// <summary> Add a node to the graph by type (convenience method - will call the System.Type version) </summary>
        public T AddNode<T>() where T : Node
        {
            return this.AddNode(typeof(T)) as T;
        }

        /// <summary> Add a node to the graph by type </summary>
        public virtual Node AddNode(Type type)
        {
            Node.GraphHotfix = this;

            var node = (Node) CreateInstance(type);
            node.graph = this;

            this.nodes.Add(node);
            return node;
        }

        /// <summary> Creates a copy of the original node in the graph </summary>
        public virtual Node CopyNode(Node original)
        {
            Node.GraphHotfix = this;

            var node = Instantiate(original);
            node.graph = this;

            node.ClearConnections();
            this.nodes.Add(node);

            return node;
        }

        /// <summary> Safely remove a node and all its connections </summary>
        /// <param name="node"> The node to remove </param>
        public virtual void RemoveNode(Node node)
        {
            node.ClearConnections();
            this.nodes.Remove(node);

            if (Application.isPlaying)
                Destroy(node);
        }

        /// <summary> Remove all nodes and connections from the graph </summary>
        public virtual void Clear()
        {
            if (Application.isPlaying)
            {
                for (var i = 0; i < this.nodes.Count; i++)
                    Destroy(this.nodes[i]);
            }

            this.nodes.Clear();
        }

        /// <summary> Create a new deep copy of this graph </summary>
        public virtual NodeGraph Copy()
        {
            // Instantiate a new nodegraph instance
            var graph = Instantiate(this);
            // Instantiate all nodes inside the graph
            for (var i = 0; i < this.nodes.Count; i++)
            {
                if (this.nodes[i] == null)
                    continue;

                Node.GraphHotfix = graph;

                var node = Instantiate(this.nodes[i]);

                node.graph     = graph;
                graph.nodes[i] = node;
            }

            // Redirect all connections
            for (var i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] == null)
                    continue;

                foreach (var port in graph.nodes[i].Ports)
                    port.Redirect(this.nodes, graph.nodes);
            }

            return graph;
        }

        protected virtual void OnDestroy()
        {
            // Remove all nodes prior to graph destruction
            this.Clear();
        }

        #region Attributes

        /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted. </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        public class RequireNodeAttribute : Attribute
        {
            public Type type0;
            public Type type1;
            public Type type2;

            /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted </summary>
            public RequireNodeAttribute(Type type)
            {
                this.type0 = type;
                this.type1 = null;
                this.type2 = null;
            }

            /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted </summary>
            public RequireNodeAttribute(Type type, Type type2)
            {
                this.type0 = type;
                this.type1 = type2;
                this.type2 = null;
            }

            /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted </summary>
            public RequireNodeAttribute(Type type, Type type2, Type type3)
            {
                this.type0 = type;
                this.type1 = type2;
                this.type2 = type3;
            }

            public bool Requires(Type type)
            {
                if (type == null) return false;
                if (type == this.type0) return true;
                if (type == this.type1) return true;
                if (type == this.type2) return true;

                return false;
            }
        }

        #endregion
    }
}