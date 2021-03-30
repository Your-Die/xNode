using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace xNode
{
    using System.Linq;

    /// <summary>
    /// Base class for all nodes
    /// </summary>
    /// <example>
    /// Classes extending this class will be considered as valid nodes by xNode.
    /// <code>
    /// [System.Serializable]
    /// public class Adder : Node {
    ///     [Input] public float a;
    ///     [Input] public float b;
    ///     [Output] public float result;
    ///
    ///     // GetValue should be overridden to return a value for any specified output port
    ///     public override object GetValue(NodePort port) {
    ///         return a + b;
    ///     }
    /// }
    /// </code>
    /// </example>
    [Serializable]
    public abstract class Node : SerializedScriptableObject
    {
        /// <summary>
        /// Used by <see cref="InputAttribute"/> and <see cref="OutputAttribute"/>
        /// to determine when to display the field value associated with a <see cref="NodePort"/>
        /// </summary>
        public enum ShowBackingValue
        {
            /// <summary> Never show the backing value </summary>
            Never,

            /// <summary> Show the backing value only when the port does not have any active connections </summary>
            Unconnected,

            /// <summary> Always show the backing value </summary>
            Always
        }

        public enum ConnectionType
        {
            /// <summary> Allow multiple connections</summary>
            Multiple,

            /// <summary> always override the current connection </summary>
            Override,
        }

        /// <summary> Tells which types of input to allow </summary>
        public enum TypeConstraint
        {
            /// <summary> Allow all types of input</summary>
            None,

            /// <summary>
            /// Allow connections where input value type is assignable from output value type
            /// (eg. ScriptableObject --> Object)
            /// </summary>
            Inherited,

            /// <summary> Allow only similar types </summary>
            Strict,

            /// <summary>
            /// Allow connections where output value type is assignable from input value type
            /// (eg. Object --> ScriptableObject)
            /// </summary>
            InheritedInverse,

            /// <summary>
            /// Allow connections where output value type is assignable from input value
            /// or input value type is assignable from output value type
            /// </summary>
            InheritedAny
        }

        /// <summary> Iterate over all ports on this node. </summary>
        public IEnumerable<NodePort> Ports => this.ports.Values;

        /// <summary> Iterate over all outputs on this node. </summary>
        public IEnumerable<NodePort> Outputs => this.Ports.Where(port => port.IsOutput);

        /// <summary> Iterate over all inputs on this node. </summary>
        public IEnumerable<NodePort> Inputs => this.Ports.Where(port => port.IsInput);

        /// <summary> Iterate over all dynamic ports on this node. </summary>
        public IEnumerable<NodePort> DynamicPorts => this.Ports.Where(port => port.IsDynamic);

        /// <summary> Iterate over all dynamic outputs on this node. </summary>
        public IEnumerable<NodePort> DynamicOutputs => this.Ports.Where(port => port.IsDynamic && port.IsOutput);

        /// <summary> Iterate over all dynamic inputs on this node. </summary>
        public IEnumerable<NodePort> DynamicInputs => this.Ports.Where(port => port.IsDynamic && port.IsInput);

        /// <summary> Parent <see cref="NodeGraph"/> </summary>
        [SerializeField] public NodeGraph graph;

        /// <summary> Position on the <see cref="NodeGraph"/> </summary>
        [SerializeField] public Vector2 position;

        /// <summary> It is recommended not to modify these at hand. Instead, see <see cref="InputAttribute"/> and <see cref="OutputAttribute"/> </summary>
        [SerializeField] private NodePortDictionary ports = new NodePortDictionary();

        /// <summary> Used during node instantiation to fix null/misconfigured graph during OnEnable/Init. Set it before instantiating a node. Will automatically be unset during OnEnable </summary>
        public static NodeGraph GraphHotfix;

        protected void OnEnable()
        {
            if (GraphHotfix != null)
                this.graph = GraphHotfix;

            GraphHotfix = null;

            this.UpdatePorts();
            this.Init();
        }

        /// <summary> Update static ports and dynamic ports managed by DynamicPortLists to reflect class fields. This happens automatically on enable or on redrawing a dynamic port list. </summary>
        public void UpdatePorts()
        {
            NodeDataCache.UpdatePorts(this, this.ports);
        }

        /// <summary> Initialize node. Called on enable. </summary>
        protected virtual void Init()
        {
        }

        /// <summary> Checks all connections for invalid references, and removes them. </summary>
        public void VerifyConnections()
        {
            foreach (var port in this.Ports)
                port.VerifyConnections();
        }

        #region Dynamic Ports

        /// <summary> Convenience function. </summary>
        public NodePort AddDynamicInput(Type           type, ConnectionType connectionType = ConnectionType.Multiple,
                                        TypeConstraint typeConstraint = TypeConstraint.None,
                                        string         fieldName      = null)
        {
            return this.AddDynamicPort(type, NodePort.IO.Input, connectionType, typeConstraint, fieldName);
        }

        /// <summary> Convenience function. </summary>
        public NodePort AddDynamicOutput(Type           type, ConnectionType connectionType = ConnectionType.Multiple,
                                         TypeConstraint typeConstraint = TypeConstraint.None,
                                         string         fieldName      = null)
        {
            return this.AddDynamicPort(type, NodePort.IO.Output, connectionType, typeConstraint, fieldName);
        }

        /// <summary> Add a dynamic, serialized port to this node. </summary>
        /// <seealso cref="AddDynamicInput"/>
        /// <seealso cref="AddDynamicOutput"/>
        private NodePort AddDynamicPort(Type           type, NodePort.IO direction,
                                        ConnectionType connectionType = ConnectionType.Multiple,
                                        TypeConstraint typeConstraint = TypeConstraint.None,
                                        string         fieldName      = null)
        {
            if (fieldName == null)
            {
                fieldName = "dynamicInput_0";
                var i = 0;

                while (this.HasPort(fieldName))
                    fieldName = "dynamicInput_" + (++i);
            }
            else if (this.HasPort(fieldName))
            {
                Debug.LogWarning("Port '" + fieldName + "' already exists in " + this.name, this);
                return this.ports[fieldName];
            }

            var port = new NodePort(fieldName, type, direction, connectionType, typeConstraint, this);
            this.ports.Add(fieldName, port);

            return port;
        }

        /// <summary> Remove an dynamic port from the node </summary>
        public void RemoveDynamicPort(string fieldName)
        {
            var dynamicPort = this.GetPort(fieldName);

            if (dynamicPort == null)
                throw new ArgumentException("port " + fieldName + " doesn't exist");

            this.RemoveDynamicPort(dynamicPort);
        }

        /// <summary> Remove an dynamic port from the node </summary>
        public void RemoveDynamicPort(NodePort port)
        {
            if (port == null)
                throw new ArgumentNullException(nameof(port));

            if (port.IsStatic)
                throw new ArgumentException("cannot remove static port");

            port.ClearConnections();
            this.ports.Remove(port.fieldName);
        }

        /// <summary> Removes all dynamic ports from the node </summary>
        [ContextMenu("Clear Dynamic Ports")]
        public void ClearDynamicPorts()
        {
            var dynamicPorts = new List<NodePort>(this.DynamicPorts);

            foreach (var port in dynamicPorts)
                this.RemoveDynamicPort(port);
        }

        #endregion

        #region Ports

        /// <summary> Returns output port which matches fieldName </summary>
        public NodePort GetOutputPort(string fieldName)
        {
            var port = this.GetPort(fieldName);
            return port == null || port.Direction != NodePort.IO.Output
                ? null
                : port;
        }

        /// <summary> Returns input port which matches fieldName </summary>
        public NodePort GetInputPort(string fieldName)
        {
            var port = this.GetPort(fieldName);
            return port == null || port.Direction != NodePort.IO.Input ? null : port;
        }

        /// <summary> Returns port which matches fieldName </summary>
        public NodePort GetPort(string fieldName)
        {
            return this.ports.TryGetValue(fieldName, out var port) ? port : null;
        }

        public bool HasPort(string fieldName) => this.ports.ContainsKey(fieldName);

        #endregion

        #region Inputs/Outputs

        /// <summary> Return input value for a specified port. Returns fallback value if no ports are connected </summary>
        /// <param name="fieldName">Field name of requested input port</param>
        /// <param name="fallback">If no ports are connected, this value will be returned</param>
        public T GetInputValue<T>(string fieldName, T fallback = default(T))
        {
            var port = this.GetPort(fieldName);

            return port != null && port.IsConnected
                ? port.GetInputValue<T>()
                : fallback;
        }

        /// <summary> Return all input values for a specified port. Returns fallback value if no ports are connected </summary>
        /// <param name="fieldName">Field name of requested input port</param>
        /// <param name="fallback">If no ports are connected, this value will be returned</param>
        public T[] GetInputValues<T>(string fieldName, params T[] fallback)
        {
            var port = this.GetPort(fieldName);

            return port != null && port.IsConnected
                ? port.GetInputValues<T>()
                : fallback;
        }

        /// <summary> Returns a value based on requested port output. Should be overridden in all derived nodes with outputs. </summary>
        /// <param name="port">The requested port.</param>
        public virtual object GetValue(NodePort port)
        {
            var message = $"No GetValue(NodePort port) override defined for {port.fieldName} in " + this.GetType();
            Debug.LogWarning(message);

            return null;
        }

        #endregion

        /// <summary> Called after a connection between two <see cref="NodePort"/>s is created </summary>
        /// <param name="from">Output</param> <param name="to">Input</param>
        public virtual void OnCreateConnection(NodePort from, NodePort to)
        {
        }

        /// <summary> Called after a connection is removed from this port </summary>
        /// <param name="port">Output or Input</param>
        public virtual void OnRemoveConnection(NodePort port)
        {
        }

        /// <summary> Disconnect everything from this node </summary>
        public void ClearConnections()
        {
            foreach (var port in this.Ports)
                port.ClearConnections();
        }

        protected TField UpdateInput<TField>(ref TField field, string fieldName)
        {
            return field = this.GetInputValue(fieldName, field);
        }

        protected void UpdateDynamicInputList<T>(List<T> list, string fieldName)
        {
            for (var index = 0; index < list.Count; index++)
            {
                var elementName = $"{fieldName} {index}";
                var element     = list[index];

                list[index] = this.GetInputValue(elementName, element);
            }
        }

        #region Attributes

        /// <summary> Mark a serializable field as an input port. You can access this through <see cref="GetInputPort(string)"/> </summary>
        [AttributeUsage(AttributeTargets.Field)]
        public class InputAttribute : Attribute
        {
            public ShowBackingValue backingValue;
            public ConnectionType   connectionType;

            public bool           dynamicPortList;
            public TypeConstraint typeConstraint;

            /// <summary> Mark a serializable field as an input port. You can access this through <see cref="GetInputPort(string)"/> </summary>
            /// <param name="backingValue">Should we display the backing value for this port as an editor field? </param>
            /// <param name="connectionType">Should we allow multiple connections? </param>
            /// <param name="typeConstraint">Constrains which input connections can be made to this port </param>
            /// <param name="dynamicPortList">If true, will display a reorderable list of inputs instead of a single port. Will automatically add and display values for lists and arrays </param>
            public InputAttribute(ShowBackingValue backingValue   = ShowBackingValue.Unconnected,
                                  ConnectionType   connectionType = ConnectionType.Multiple,
                                  TypeConstraint   typeConstraint = TypeConstraint.None, bool dynamicPortList = false)
            {
                this.backingValue    = backingValue;
                this.connectionType  = connectionType;
                this.dynamicPortList = dynamicPortList;
                this.typeConstraint  = typeConstraint;
            }
        }

        /// <summary> Mark a serializable field as an output port. You can access this through <see cref="GetOutputPort(string)"/> </summary>
        [AttributeUsage(AttributeTargets.Field)]
        public class OutputAttribute : Attribute
        {
            public ShowBackingValue backingValue;
            public ConnectionType   connectionType;

            public bool           dynamicPortList;
            public TypeConstraint typeConstraint;

            /// <summary> Mark a serializable field as an output port. You can access this through <see cref="GetOutputPort(string)"/> </summary>
            /// <param name="backingValue">Should we display the backing value for this port as an editor field? </param>
            /// <param name="connectionType">Should we allow multiple connections? </param>
            /// <param name="typeConstraint">Constrains which input connections can be made from this port </param>
            /// <param name="dynamicPortList">If true, will display a reorderable list of outputs instead of a single port. Will automatically add and display values for lists and arrays </param>
            public OutputAttribute(ShowBackingValue backingValue   = ShowBackingValue.Never,
                                   ConnectionType   connectionType = ConnectionType.Multiple,
                                   TypeConstraint   typeConstraint = TypeConstraint.None, bool dynamicPortList = false)
            {
                this.backingValue    = backingValue;
                this.connectionType  = connectionType;
                this.dynamicPortList = dynamicPortList;
                this.typeConstraint  = typeConstraint;
            }

            /// <summary> Mark a serializable field as an output port. You can access this through <see cref="GetOutputPort(string)"/> </summary>
            /// <param name="backingValue">Should we display the backing value for this port as an editor field? </param>
            /// <param name="connectionType">Should we allow multiple connections? </param>
            /// <param name="dynamicPortList">If true, will display a reorderable list of outputs instead of a single port. Will automatically add and display values for lists and arrays </param>
            [Obsolete("Use constructor with TypeConstraint")]
            public OutputAttribute(ShowBackingValue backingValue, ConnectionType connectionType, bool dynamicPortList) :
                this(backingValue, connectionType, TypeConstraint.None, dynamicPortList)
            {
            }
        }

        /// <summary> Manually supply node class with a context menu path </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class CreateNodeMenuAttribute : Attribute
        {
            public string menuName;
            public int    order;

            /// <summary> Manually supply node class with a context menu path </summary>
            /// <param name="menuName"> Path to this node in the context menu. Null or empty hides it. </param>
            public CreateNodeMenuAttribute(string menuName)
            {
                this.menuName = menuName;
                this.order    = 0;
            }

            /// <summary> Manually supply node class with a context menu path </summary>
            /// <param name="menuName"> Path to this node in the context menu. Null or empty hides it. </param>
            /// <param name="order"> The order by which the menu items are displayed. </param>
            public CreateNodeMenuAttribute(string menuName, int order)
            {
                this.menuName = menuName;
                this.order    = order;
            }
        }

        /// <summary> Prevents Node of the same type to be added more than once (configurable) to a NodeGraph </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class DisallowMultipleNodesAttribute : Attribute
        {
            // TODO: Make inheritance work in such a way that applying [DisallowMultipleNodes(1)] to type NodeBar : Node
            //       while type NodeFoo : NodeBar exists, will let you add *either one* of these nodes, but not both.
            public int max;

            /// <summary> Prevents Node of the same type to be added more than once (configurable) to a NodeGraph </summary>
            /// <param name="max"> How many nodes to allow. Defaults to 1. </param>
            public DisallowMultipleNodesAttribute(int max = 1)
            {
                this.max = max;
            }
        }

        /// <summary> Specify a color for this node type </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class NodeTintAttribute : Attribute
        {
            public Color color;

            /// <summary> Specify a color for this node type </summary>
            /// <param name="r"> Red [0.0f .. 1.0f] </param>
            /// <param name="g"> Green [0.0f .. 1.0f] </param>
            /// <param name="b"> Blue [0.0f .. 1.0f] </param>
            public NodeTintAttribute(float r, float g, float b)
            {
                this.color = new Color(r, g, b);
            }

            /// <summary> Specify a color for this node type </summary>
            /// <param name="hex"> HEX color value </param>
            public NodeTintAttribute(string hex)
            {
                ColorUtility.TryParseHtmlString(hex, out this.color);
            }

            /// <summary> Specify a color for this node type </summary>
            /// <param name="r"> Red [0 .. 255] </param>
            /// <param name="g"> Green [0 .. 255] </param>
            /// <param name="b"> Blue [0 .. 255] </param>
            public NodeTintAttribute(byte r, byte g, byte b)
            {
                this.color = new Color32(r, g, b, byte.MaxValue);
            }
        }

        /// <summary> Specify a width for this node type </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class NodeWidthAttribute : Attribute
        {
            public int width;

            /// <summary> Specify a width for this node type </summary>
            /// <param name="width"> Width </param>
            public NodeWidthAttribute(int width)
            {
                this.width = width;
            }
        }

        #endregion

        [Serializable]
        private class NodePortDictionary : Dictionary<string, NodePort>, ISerializationCallbackReceiver
        {
            [SerializeField] private List<string>   keys   = new List<string>();
            [SerializeField] private List<NodePort> values = new List<NodePort>();

            public void OnBeforeSerialize()
            {
                this.keys.Clear();
                this.values.Clear();
                foreach (KeyValuePair<string, NodePort> pair in this)
                {
                    this.keys.Add(pair.Key);
                    this.values.Add(pair.Value);
                }
            }

            public void OnAfterDeserialize()
            {
                this.Clear();

                if (this.keys.Count != this.values.Count)
                    throw new Exception("there are " + this.keys.Count + " keys and " + this.values.Count +
                                        " values after deserialization. Make sure that both key and value types are serializable.");

                for (int i = 0; i < this.keys.Count; i++)
                    this.Add(this.keys[i], this.values[i]);
            }
        }
    }
}