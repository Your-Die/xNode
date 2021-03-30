using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace xNode
{
    using System.Linq;

    [Serializable]
    public class NodePort
    {
        public enum IO
        {
            Input,
            Output
        }

        public int ConnectionCount => this.connections.Count;

        /// <summary> Return the first non-null connection </summary>
        public NodePort Connection
        {
            get
            {
                for (var i = 0; i < this.connections.Count; i++)
                {
                    if (this.connections[i] != null)
                        return this.connections[i].Port;
                }

                return null;
            }
        }

        public IO Direction
        {
            get => this._direction;
            internal set => this._direction = value;
        }

        public Node.ConnectionType ConnectionType
        {
            get => this._connectionType;
            internal set => this._connectionType = value;
        }

        public Node.TypeConstraint TypeConstraint
        {
            get => this._typeConstraint;
            internal set => this._typeConstraint = value;
        }

        /// <summary> Is this port connected to anytihng? </summary>
        public bool IsConnected => this.connections.Count != 0;

        public bool IsInput => this.Direction == IO.Input;

        public bool IsOutput => this.Direction == IO.Output;

        public string fieldName => this._fieldName;

        public Node node => this._node;

        public bool IsDynamic => this._dynamic;

        public bool IsStatic => !this._dynamic;

        public Type ValueType
        {
            get
            {
                if (this.valueType == null && !string.IsNullOrEmpty(this._typeQualifiedName))
                    this.valueType = Type.GetType(this._typeQualifiedName, false);

                return this.valueType;
            }
            set
            {
                this.valueType = value;
                if (value != null)
                    this._typeQualifiedName = value.AssemblyQualifiedName;
            }
        }

        private Type valueType;

        [SerializeField] private string               _fieldName;
        [SerializeField] private Node                 _node;
        [SerializeField] private string               _typeQualifiedName;
        [SerializeField] private List<PortConnection> connections = new List<PortConnection>();
        [SerializeField] private IO                   _direction;
        [SerializeField] private Node.ConnectionType  _connectionType;
        [SerializeField] private Node.TypeConstraint  _typeConstraint;
        [SerializeField] private bool                 _dynamic;

        /// <summary> Construct a static targetless nodeport. Used as a template. </summary>
        public NodePort(FieldInfo fieldInfo)
        {
            this._fieldName = fieldInfo.Name;
            this.ValueType  = fieldInfo.FieldType;
            this._dynamic   = false;

            var attributes = fieldInfo.GetCustomAttributes(false);

            for (var i = 0; i < attributes.Length; i++)
            {
                switch (attributes[i])
                {
                    case Node.InputAttribute inputAttribute:
                        this._direction      = IO.Input;
                        this._connectionType = inputAttribute.connectionType;
                        this._typeConstraint = inputAttribute.typeConstraint;
                        break;
                    case Node.OutputAttribute outputAttribute:
                        this._direction      = IO.Output;
                        this._connectionType = outputAttribute.connectionType;
                        this._typeConstraint = outputAttribute.typeConstraint;
                        break;
                }
            }
        }

        /// <summary> Copy a nodePort but assign it to another node. </summary>
        public NodePort(NodePort nodePort, Node node)
        {
            this._fieldName      = nodePort._fieldName;
            this.ValueType       = nodePort.valueType;
            this._direction      = nodePort.Direction;
            this._dynamic        = nodePort._dynamic;
            this._connectionType = nodePort._connectionType;
            this._typeConstraint = nodePort._typeConstraint;
            this._node           = node;
        }

        /// <summary> Construct a dynamic port. Dynamic ports are not forgotten on reimport, and is ideal for runtime-created ports. </summary>
        public NodePort(string              fieldName,      Type type, IO direction, Node.ConnectionType connectionType,
                        Node.TypeConstraint typeConstraint, Node node)
        {
            this._fieldName      = fieldName;
            this.ValueType       = type;
            this._direction      = direction;
            this._node           = node;
            this._dynamic        = true;
            this._connectionType = connectionType;
            this._typeConstraint = typeConstraint;
        }

        /// <summary> Checks all connections for invalid references, and removes them. </summary>
        public void VerifyConnections()
        {
            for (var i = this.connections.Count - 1; i >= 0; i--)
            {
                if (this.connections[i].node == null || string.IsNullOrEmpty(this.connections[i].fieldName) ||
                    this.connections[i].node.GetPort(this.connections[i].fieldName) == null)
                    this.connections.RemoveAt(i);
            }
        }

        /// <summary> Return the output value of this node through its parent nodes GetValue override method. </summary>
        /// <returns> <see cref="Node.GetValue(NodePort)"/> </returns>
        public object GetOutputValue()
        {
            return this.Direction == IO.Input ? null : this.node.GetValue(this);
        }

        /// <summary> Return the output value of the first connected port. Returns null if none found or invalid.</summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public object GetInputValue()
        {
            return this.Connection?.GetOutputValue();
        }

        /// <summary> Return the output values of all connected ports. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public object[] GetInputValues()
        {
            var objects = new object[this.ConnectionCount];

            for (var i = 0; i < this.ConnectionCount; i++)
            {
                var connectedPort = this.connections[i].Port;
                if (connectedPort == null)
                {
                    // if we happen to find a null port, remove it and look again
                    this.connections.RemoveAt(i);
                    i--;
                }
                else
                {
                    objects[i] = connectedPort.GetOutputValue();
                }
            }

            return objects;
        }

        /// <summary> Return the output value of the first connected port. Returns null if none found or invalid. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public T GetInputValue<T>()
        {
            var obj = this.GetInputValue();
            return obj is T value ? value : default;
        }

        /// <summary> Return the output values of all connected ports. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public T[] GetInputValues<T>()
        {
            var inputValues = this.GetInputValues();

            var castValues = new T[inputValues.Length];

            for (var i = 0; i < inputValues.Length; i++)
            {
                if (inputValues[i] is T castValue)
                    castValues[i] = castValue;
            }

            return castValues;
        }

        /// <summary> Return true if port is connected and has a valid input. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public bool TryGetInputValue<T>(out T value)
        {
            var obj = this.GetInputValue();
            if (obj is T castValue)
            {
                value = castValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary> Return the sum of all inputs. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public float GetInputSum(float fallback)
        {
            var inputValues = this.GetInputValues();

            return inputValues.Length != 0
                ? inputValues.OfType<float>().Sum()
                : fallback;
        }

        /// <summary> Return the sum of all inputs. </summary>
        /// <returns> <see cref="NodePort.GetOutputValue"/> </returns>
        public int GetInputSum(int fallback)
        {
            var inputValues = this.GetInputValues();

            return inputValues.Length != 0
                ? inputValues.OfType<int>().Sum()
                : fallback;
        }

        /// <summary> Connect this <see cref="NodePort"/> to another </summary>
        /// <param name="port">The <see cref="NodePort"/> to connect to</param>
        public void Connect(NodePort port)
        {
            this.connections ??= new List<PortConnection>();

            if (port == null)
            {
                Debug.LogWarning("Cannot connect to null port");
                return;
            }

            if (port == this)
            {
                Debug.LogWarning("Cannot connect port to self.");
                return;
            }

            if (this.IsConnectedTo(port))
            {
                Debug.LogWarning("Port already connected. ");
                return;
            }

            if (this.Direction == port.Direction)
            {
                Debug.LogWarning("Cannot connect two " + (this.Direction == IO.Input ? "input" : "output") +
                                 " connections");
                return;
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this.node, "Connect Port");
            UnityEditor.Undo.RecordObject(port.node, "Connect Port");
#endif

            if (port.ConnectionType == Node.ConnectionType.Override && port.ConnectionCount != 0)
                port.ClearConnections();

            if (this.ConnectionType == Node.ConnectionType.Override && this.ConnectionCount != 0)
                this.ClearConnections();

            this.connections.Add(new PortConnection(port));

            port.connections ??= new List<PortConnection>();

            if (!port.IsConnectedTo(this))
                port.connections.Add(new PortConnection(this));

            this.node.OnCreateConnection(this, port);
            port.node.OnCreateConnection(this, port);
        }

        public List<NodePort> GetConnections()
        {
            var result = new List<NodePort>();
            for (var i = 0; i < this.connections.Count; i++)
            {
                var port = this.GetConnection(i);
                if (port != null) result.Add(port);
            }

            return result;
        }

        public NodePort GetConnection(int i)
        {
            //If the connection is broken for some reason, remove it.
            if (this.connections[i].node == null || string.IsNullOrEmpty(this.connections[i].fieldName))
            {
                this.connections.RemoveAt(i);
                return null;
            }

            var port = this.connections[i].node.GetPort(this.connections[i].fieldName);
            if (port != null)
                return port;

            this.connections.RemoveAt(i);
            return null;
        }

        /// <summary> Get index of the connection connecting this and specified ports </summary>
        public int GetConnectionIndex(NodePort port)
        {
            for (var i = 0; i < this.ConnectionCount; i++)
            {
                if (this.connections[i].Port == port)
                    return i;
            }

            return -1;
        }

        public bool IsConnectedTo(NodePort port)
        {
            return this.connections.Any(connection => connection.Port == port);
        }

        /// <summary> Returns true if this port can connect to specified port </summary>
        public bool CanConnectTo(NodePort port)
        {
            // Figure out which is input and which is output
            NodePort input = null, output = null;

            if (this.IsInput)
                input = this;
            else
                output = this;

            if (port.IsInput)
                input = port;
            else
                output = port;

            // If there isn't one of each, they can't connect
            if (input == null || output == null)
                return false;

            switch (input.TypeConstraint)
            {
                // Check input type constraints
                case Node.TypeConstraint.Inherited when !input.ValueType.IsAssignableFrom(output.ValueType):
                case Node.TypeConstraint.Strict when input.ValueType != output.ValueType:
                case Node.TypeConstraint.InheritedInverse when !output.ValueType.IsAssignableFrom(input.ValueType):
                case Node.TypeConstraint.InheritedAny when !input.ValueType.IsAssignableFrom(output.ValueType) &&
                                                           !output.ValueType.IsAssignableFrom(input.ValueType):
                    return false;
            }

            switch (output.TypeConstraint)
            {
                // Check output type constraints
                case Node.TypeConstraint.Inherited when !input.ValueType.IsAssignableFrom(output.ValueType):
                case Node.TypeConstraint.Strict when input.ValueType != output.ValueType:
                case Node.TypeConstraint.InheritedInverse when !output.ValueType.IsAssignableFrom(input.ValueType):
                case Node.TypeConstraint.InheritedAny when !input.ValueType.IsAssignableFrom(output.ValueType) &&
                                                           !output.ValueType.IsAssignableFrom(input.ValueType):
                    return false;
                default:
                    // Success
                    return true;
            }
        }

        /// <summary> Disconnect this port from another port </summary>
        public void Disconnect(NodePort port)
        {
            // Remove this ports connection to the other
            for (var i = this.connections.Count - 1; i >= 0; i--)
            {
                if (this.connections[i].Port == port)
                {
                    this.connections.RemoveAt(i);
                }
            }

            if (port != null)
            {
                // Remove the other ports connection to this port
                for (var i = 0; i < port.connections.Count; i++)
                {
                    if (port.connections[i].Port == this)
                    {
                        port.connections.RemoveAt(i);
                    }
                }
            }

            // Trigger OnRemoveConnection
            this.node.OnRemoveConnection(this);
            port?.node.OnRemoveConnection(port);
        }

        /// <summary> Disconnect this port from another port </summary>
        public void Disconnect(int i)
        {
            // Remove the other ports connection to this port
            var otherPort = this.connections[i].Port;
            otherPort?.connections.RemoveAll(connection => connection.Port == this);

            // Remove this ports connection to the other
            this.connections.RemoveAt(i);

            // Trigger OnRemoveConnection
            this.node.OnRemoveConnection(this);
            otherPort?.node.OnRemoveConnection(otherPort);
        }

        public void ClearConnections()
        {
            while (this.connections.Count > 0)
                this.Disconnect(this.connections[0].Port);
        }

        /// <summary> Get reroute points for a given connection. This is used for organization </summary>
        public List<Vector2> GetReroutePoints(int index)
        {
            return this.connections[index].reroutePoints;
        }

        /// <summary> Swap connections with another node </summary>
        public void SwapConnections(NodePort targetPort)
        {
            var aConnectionCount = this.connections.Count;
            var bConnectionCount = targetPort.connections.Count;

            var portConnections       = new List<NodePort>();
            var targetPortConnections = new List<NodePort>();

            // Cache port connections
            for (var i = 0; i < aConnectionCount; i++)
                portConnections.Add(this.connections[i].Port);

            // Cache target port connections
            for (var i = 0; i < bConnectionCount; i++)
                targetPortConnections.Add(targetPort.connections[i].Port);

            this.ClearConnections();
            targetPort.ClearConnections();

            // Add port connections to targetPort
            for (var i = 0; i < portConnections.Count; i++)
                targetPort.Connect(portConnections[i]);

            // Add target port connections to this one
            for (var i = 0; i < targetPortConnections.Count; i++)
                this.Connect(targetPortConnections[i]);
        }

        /// <summary> Copy all connections pointing to a node and add them to this one </summary>
        public void AddConnections(NodePort targetPort)
        {
            var connectionCount = targetPort.ConnectionCount;
            for (var i = 0; i < connectionCount; i++)
            {
                var connection = targetPort.connections[i];
                var otherPort  = connection.Port;
                this.Connect(otherPort);
            }
        }

        /// <summary> Move all connections pointing to this node, to another node </summary>
        public void MoveConnections(NodePort targetPort)
        {
            var connectionCount = this.connections.Count;

            // Add connections to target port
            for (var i = 0; i < connectionCount; i++)
            {
                var connection = targetPort.connections[i];
                var otherPort  = connection.Port;
                this.Connect(otherPort);
            }

            this.ClearConnections();
        }

        /// <summary> Swap connected nodes from the old list with nodes from the new list </summary>
        public void Redirect(List<Node> oldNodes, List<Node> newNodes)
        {
            foreach (var connection in this.connections)
            {
                var index = oldNodes.IndexOf(connection.node);

                if (index >= 0)
                    connection.node = newNodes[index];
            }
        }

        [Serializable]
        private class PortConnection
        {
            [SerializeField] public string fieldName;
            [SerializeField] public Node   node;

            public NodePort Port => this.port ??= this.GetPort();

            [NonSerialized] private NodePort port;

            /// <summary> Extra connection path points for organization </summary>
            [SerializeField] public List<Vector2> reroutePoints = new List<Vector2>();

            public PortConnection(NodePort port)
            {
                this.port      = port;
                this.node      = port.node;
                this.fieldName = port.fieldName;
            }

            /// <summary> Returns the port that this <see cref="PortConnection"/> points to </summary>
            private NodePort GetPort()
            {
                return this.node != null && !string.IsNullOrEmpty(this.fieldName)
                    ? this.node.GetPort(this.fieldName)
                    : null;
            }
        }
    }
}