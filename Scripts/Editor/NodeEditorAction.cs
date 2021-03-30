using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNodeEditor.Internal;

namespace XNodeEditor
{
    public partial class NodeEditorWindow
    {
        public enum NodeActivity
        {
            Idle,
            HoldNode,
            DragNode,
            HoldGrid,
            DragGrid
        }

        public static NodeActivity currentActivity = NodeActivity.Idle;
        public static bool         isPanning { get; private set; }
        public static Vector2[]    dragOffset;

        public static xNode.Node[] copyBuffer = null;

        public bool IsDraggingPort => this.draggedOutput != null;

        public bool IsHoveringPort => this.hoveredPort != null;

        public bool IsHoveringNode => this.hoveredNode != null;

        public bool IsHoveringReroute => this.hoveredReroute.port != null;

        /// <summary> Return the dragged port or null if not exist </summary>
        public xNode.NodePort DraggedOutputPort
        {
            get
            {
                xNode.NodePort result = this.draggedOutput;
                return result;
            }
        }

        /// <summary> Return the Hovered port or null if not exist </summary>
        public xNode.NodePort HoveredPort
        {
            get
            {
                var result = this.hoveredPort;
                return result;
            }
        }

        /// <summary> Return the Hovered node or null if not exist </summary>
        public xNode.Node HoveredNode
        {
            get
            {
                xNode.Node result = this.hoveredNode;
                return result;
            }
        }

        private                 xNode.Node     hoveredNode           = null;
        [NonSerialized] public  xNode.NodePort hoveredPort           = null;
        [NonSerialized] private xNode.NodePort draggedOutput         = null;
        [NonSerialized] private xNode.NodePort draggedOutputTarget   = null;
        [NonSerialized] private xNode.NodePort autoConnectOutput     = null;
        [NonSerialized] private List<Vector2>  draggedOutputReroutes = new List<Vector2>();

        private RerouteReference       hoveredReroute   = new RerouteReference();
        public  List<RerouteReference> selectedReroutes = new List<RerouteReference>();
        private Vector2                dragBoxStart;
        private UnityEngine.Object[]   preBoxSelection;
        private RerouteReference[]     preBoxSelectionReroute;
        private Rect                   selectionBox;
        private bool                   isDoubleClick = false;
        private Vector2                lastMousePosition;
        private float                  dragThreshold = 1f;

        public void Controls()
        {
            this.wantsMouseMove = true;
            var e = Event.current;
            switch (e.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                    if (e.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        this.graphEditor.OnDropObjects(DragAndDrop.objectReferences);
                    }

                    break;
                case EventType.MouseMove:
                    //Keyboard commands will not get correct mouse position from Event
                    this.lastMousePosition = e.mousePosition;
                    break;
                case EventType.ScrollWheel:
                    var oldZoom = this.zoom;
                    if (e.delta.y > 0)
                        this.zoom += 0.1f * this.zoom;
                    else
                        this.zoom     -= 0.1f * this.zoom;
                    if (NodeEditorPreferences.GetSettings().zoomToMouse) this.panOffset += (1 - oldZoom / this.zoom) * (this.WindowToGridPosition(e.mousePosition) + this.panOffset);
                    break;
                case EventType.MouseDrag:
                    switch (e.button)
                    {
                        case 0:
                        {
                            if (this.IsDraggingPort)
                            {
                                // Set target even if we can't connect, so as to prevent auto-conn menu from opening erroneously
                                if (this.IsHoveringPort && this.hoveredPort.IsInput && !this.draggedOutput.IsConnectedTo(this.hoveredPort))
                                {
                                    this.draggedOutputTarget = this.hoveredPort;
                                }
                                else
                                {
                                    this.draggedOutputTarget = null;
                                }

                                this.Repaint();
                            }
                            else if (currentActivity == NodeActivity.HoldNode)
                            {
                                this.RecalculateDragOffsets(e);
                                currentActivity = NodeActivity.DragNode;
                                this.Repaint();
                            }

                            switch (currentActivity)
                            {
                                case NodeActivity.DragNode:
                                {
                                    // Holding ctrl inverts grid snap
                                    var gridSnap            = NodeEditorPreferences.GetSettings().gridSnap;
                                    if (e.control) gridSnap = !gridSnap;

                                    var mousePos = this.WindowToGridPosition(e.mousePosition);
                                    // Move selected nodes with offset
                                    for (var i = 0; i < Selection.objects.Length; i++)
                                    {
                                        if (!(Selection.objects[i] is xNode.Node node))
                                            continue;

                                        Undo.RecordObject(node, "Moved Node");
                                        var initial = node.position;
                                        node.position = mousePos + dragOffset[i];
                                        if (gridSnap)
                                        {
                                            node.position.x = (Mathf.Round((node.position.x + 8) / 16) * 16) - 8;
                                            node.position.y = (Mathf.Round((node.position.y + 8) / 16) * 16) - 8;
                                        }

                                        // Offset portConnectionPoints instantly if a node is dragged so they aren't delayed by a frame.
                                        var offset = node.position - initial;
                                        if (!(offset.sqrMagnitude > 0))
                                            continue;

                                        foreach (var output in node.Outputs)
                                        {
                                            if (this.portConnectionPoints.TryGetValue(output, out var rect))
                                            {
                                                rect.position                     += offset;
                                                this.portConnectionPoints[output] =  rect;
                                            }
                                        }

                                        foreach (var input in node.Inputs)
                                        {
                                            if (this.portConnectionPoints.TryGetValue(input, out var rect))
                                            {
                                                rect.position                    += offset;
                                                this.portConnectionPoints[input] =  rect;
                                            }
                                        }
                                    }

                                    // Move selected reroutes with offset
                                    for (var i = 0; i < this.selectedReroutes.Count; i++)
                                    {
                                        var pos = mousePos + dragOffset[Selection.objects.Length + i];
                                        if (gridSnap)
                                        {
                                            pos.x = (Mathf.Round(pos.x / 16) * 16);
                                            pos.y = (Mathf.Round(pos.y / 16) * 16);
                                        }

                                        this.selectedReroutes[i].SetPoint(pos);
                                    }

                                    this.Repaint();
                                    break;
                                }
                                case NodeActivity.HoldGrid:
                                    currentActivity             = NodeActivity.DragGrid;
                                    this.preBoxSelection        = Selection.objects;
                                    this.preBoxSelectionReroute = this.selectedReroutes.ToArray();
                                    this.dragBoxStart           = this.WindowToGridPosition(e.mousePosition);
                                    this.Repaint();
                                    break;
                                case NodeActivity.DragGrid:
                                {
                                    var boxStartPos = this.GridToWindowPosition(this.dragBoxStart);
                                    var boxSize     = e.mousePosition - boxStartPos;
                                    if (boxSize.x < 0)
                                    {
                                        boxStartPos.x += boxSize.x;
                                        boxSize.x     =  Mathf.Abs(boxSize.x);
                                    }

                                    if (boxSize.y < 0)
                                    {
                                        boxStartPos.y += boxSize.y;
                                        boxSize.y     =  Mathf.Abs(boxSize.y);
                                    }

                                    this.selectionBox = new Rect(boxStartPos, boxSize);
                                    this.Repaint();
                                    break;
                                }
                            }

                            break;
                        }
                        case 1:
                        case 2:
                        {
                            //check drag threshold for larger screens
                            if (e.delta.magnitude > this.dragThreshold)
                            {
                                this.panOffset += e.delta * this.zoom;
                                isPanning      =  true;
                            }

                            break;
                        }
                    }

                    break;
                case EventType.MouseDown:
                    this.Repaint();
                    if (e.button == 0)
                    {
                        this.draggedOutputReroutes.Clear();

                        if (this.IsHoveringPort)
                        {
                            if (this.hoveredPort.IsOutput)
                            {
                                this.draggedOutput     = this.hoveredPort;
                                this.autoConnectOutput = this.hoveredPort;
                            }
                            else
                            {
                                this.hoveredPort.VerifyConnections();
                                this.autoConnectOutput = null;
                                if (this.hoveredPort.IsConnected)
                                {
                                    var node                  = this.hoveredPort.node;
                                    var output                = this.hoveredPort.Connection;
                                    var outputConnectionIndex = output.GetConnectionIndex(this.hoveredPort);
                                    this.draggedOutputReroutes = output.GetReroutePoints(outputConnectionIndex);
                                    this.hoveredPort.Disconnect(output);
                                    this.draggedOutput       = output;
                                    this.draggedOutputTarget = this.hoveredPort;
                                    if (NodeEditor.onUpdateNode != null) NodeEditor.onUpdateNode(node);
                                }
                            }
                        }
                        else if (this.IsHoveringNode && this.IsHoveringTitle(this.hoveredNode))
                        {
                            // If mousedown on node header, select or deselect
                            if (!Selection.Contains(this.hoveredNode))
                            {
                                this.SelectNode(this.hoveredNode, e.control || e.shift);
                                if (!e.control && !e.shift) this.selectedReroutes.Clear();
                            }
                            else if (e.control || e.shift) this.DeselectNode(this.hoveredNode);

                            // Cache double click state, but only act on it in MouseUp - Except ClickCount only works in mouseDown.
                            this.isDoubleClick = (e.clickCount == 2);

                            e.Use();
                            currentActivity = NodeActivity.HoldNode;
                        }
                        else if (this.IsHoveringReroute)
                        {
                            // If reroute isn't selected
                            if (!this.selectedReroutes.Contains(this.hoveredReroute))
                            {
                                // Add it
                                if (e.control || e.shift)
                                    this.selectedReroutes.Add(this.hoveredReroute);
                                // Select it
                                else
                                {
                                    this.selectedReroutes  = new List<RerouteReference>() { this.hoveredReroute };
                                    Selection.activeObject = null;
                                }
                            }
                            // Deselect
                            else if (e.control || e.shift) this.selectedReroutes.Remove(this.hoveredReroute);

                            e.Use();
                            currentActivity = NodeActivity.HoldNode;
                        }
                        // If mousedown on grid background, deselect all
                        else if (!this.IsHoveringNode)
                        {
                            currentActivity = NodeActivity.HoldGrid;
                            if (!e.control && !e.shift)
                            {
                                this.selectedReroutes.Clear();
                                Selection.activeObject = null;
                            }
                        }
                    }

                    break;
                case EventType.MouseUp:
                    if (e.button == 0)
                    {
                        //Port drag release
                        if (this.IsDraggingPort)
                        {
                            // If connection is valid, save it
                            if (this.draggedOutputTarget != null && this.draggedOutput.CanConnectTo(this.draggedOutputTarget))
                            {
                                var node = this.draggedOutputTarget.node;
                                if (this.graph.nodes.Count != 0) this.draggedOutput.Connect(this.draggedOutputTarget);

                                // ConnectionIndex can be -1 if the connection is removed instantly after creation
                                var connectionIndex = this.draggedOutput.GetConnectionIndex(this.draggedOutputTarget);
                                if (connectionIndex != -1)
                                {
                                    this.draggedOutput.GetReroutePoints(connectionIndex).AddRange(this.draggedOutputReroutes);
                                    if (NodeEditor.onUpdateNode != null) NodeEditor.onUpdateNode(node);
                                    EditorUtility.SetDirty(this.graph);
                                }
                            }
                            // Open context menu for auto-connection if there is no target node
                            else if (this.draggedOutputTarget == null && NodeEditorPreferences.GetSettings().dragToCreate && this.autoConnectOutput   != null)
                            {
                                var menu = new GenericMenu();
                                this.graphEditor.AddContextMenuItems(menu);
                                menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
                            }

                            //Release dragged connection
                            this.draggedOutput    = null;
                            this.draggedOutputTarget = null;
                            EditorUtility.SetDirty(this.graph);
                            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
                        }
                        else if (currentActivity == NodeActivity.DragNode)
                        {
                            var nodes = Selection.objects.Where(x => x is xNode.Node).Select(x => x as xNode.Node);
                            foreach (var node in nodes) EditorUtility.SetDirty(node);
                            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
                        }
                        else if (!this.IsHoveringNode)
                        {
                            // If click outside node, release field focus
                            if (!isPanning)
                            {
                                EditorGUI.FocusTextInControl(null);
                                EditorGUIUtility.editingTextField = false;
                            }

                            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
                        }

                        // If click node header, select it.
                        if (currentActivity == NodeActivity.HoldNode && !(e.control || e.shift))
                        {
                            this.selectedReroutes.Clear();
                            this.SelectNode(this.hoveredNode, false);

                            // Double click to center node
                            if (this.isDoubleClick)
                            {
                                var nodeDimension = this.nodeSizes.ContainsKey(this.hoveredNode)
                                    ? this.nodeSizes[this.hoveredNode] / 2
                                    : Vector2.zero;
                                this.panOffset = -this.hoveredNode.position - nodeDimension;
                            }
                        }

                        // If click reroute, select it.
                        if (this.IsHoveringReroute && !(e.control || e.shift))
                        {
                            this.selectedReroutes  = new List<RerouteReference>() { this.hoveredReroute };
                            Selection.activeObject = null;
                        }

                        this.Repaint();
                        currentActivity = NodeActivity.Idle;
                    }
                    else if (e.button == 1 || e.button == 2)
                    {
                        if (!isPanning)
                        {
                            if (this.IsDraggingPort)
                            {
                                this.draggedOutputReroutes.Add(this.WindowToGridPosition(e.mousePosition));
                            }
                            else if (currentActivity == NodeActivity.DragNode && Selection.activeObject == null && this.selectedReroutes.Count == 1)
                            {
                                this.selectedReroutes[0].InsertPoint(this.selectedReroutes[0].GetPoint());
                                this.selectedReroutes[0] = new RerouteReference(this.selectedReroutes[0].port, this.selectedReroutes[0].connectionIndex, this.selectedReroutes[0].pointIndex + 1);
                            }
                            else if (this.IsHoveringReroute)
                            {
                                this.ShowRerouteContextMenu(this.hoveredReroute);
                            }
                            else if (this.IsHoveringPort)
                            {
                                this.ShowPortContextMenu(this.hoveredPort);
                            }
                            else if (this.IsHoveringNode && this.IsHoveringTitle(this.hoveredNode))
                            {
                                if (!Selection.Contains(this.hoveredNode)) this.SelectNode(this.hoveredNode, false);
                                this.autoConnectOutput = null;
                                var menu = new GenericMenu();
                                NodeEditor.GetEditor(this.hoveredNode, this).AddContextMenuItems(menu);
                                menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
                                e.Use(); // Fixes copy/paste context menu appearing in Unity 5.6.6f2 - doesn't occur in 2018.3.2f1 Probably needs to be used in other places.
                            }
                            else if (!this.IsHoveringNode)
                            {
                                this.autoConnectOutput = null;
                                var menu = new GenericMenu();
                                this.graphEditor.AddContextMenuItems(menu);
                                menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
                            }
                        }

                        isPanning = false;
                    }

                    // Reset DoubleClick
                    this.isDoubleClick = false;
                    break;
                case EventType.KeyDown:
                    if (EditorGUIUtility.editingTextField) break;
                    else if (e.keyCode == KeyCode.F) this.Home();
                    if (NodeEditorUtilities.IsMac())
                    {
                        if (e.keyCode == KeyCode.Return) this.RenameSelectedNode();
                    }
                    else
                    {
                        if (e.keyCode == KeyCode.F2) this.RenameSelectedNode();
                    }

                    if (e.keyCode == KeyCode.A)
                    {
                        if (Selection.objects.Any(x => this.graph.nodes.Contains(x as xNode.Node)))
                        {
                            foreach (var node in this.graph.nodes)
                            {
                                this.DeselectNode(node);
                            }
                        }
                        else
                        {
                            foreach (var node in this.graph.nodes)
                            {
                                this.SelectNode(node, true);
                            }
                        }

                        this.Repaint();
                    }

                    break;
                case EventType.ValidateCommand:
                case EventType.ExecuteCommand:
                    if (e.commandName == "SoftDelete")
                    {
                        if (e.type == EventType.ExecuteCommand) this.RemoveSelectedNodes();
                        e.Use();
                    }
                    else if (NodeEditorUtilities.IsMac() && e.commandName == "Delete")
                    {
                        if (e.type == EventType.ExecuteCommand) this.RemoveSelectedNodes();
                        e.Use();
                    }
                    else if (e.commandName == "Duplicate")
                    {
                        if (e.type == EventType.ExecuteCommand) this.DuplicateSelectedNodes();
                        e.Use();
                    }
                    else if (e.commandName == "Copy")
                    {
                        if (e.type == EventType.ExecuteCommand) this.CopySelectedNodes();
                        e.Use();
                    }
                    else if (e.commandName == "Paste")
                    {
                        if (e.type == EventType.ExecuteCommand) this.PasteNodes(this.WindowToGridPosition(this.lastMousePosition));
                        e.Use();
                    }

                    this.Repaint();
                    break;
                case EventType.Ignore:
                    // If release mouse outside window
                    if (e.rawType == EventType.MouseUp && currentActivity == NodeActivity.DragGrid)
                    {
                        this.Repaint();
                        currentActivity = NodeActivity.Idle;
                    }

                    break;
            }
        }

        private void RecalculateDragOffsets(Event current)
        {
            dragOffset = new Vector2[Selection.objects.Length + this.selectedReroutes.Count];
            // Selected nodes
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                if (Selection.objects[i] is xNode.Node)
                {
                    xNode.Node node = Selection.objects[i] as xNode.Node;
                    dragOffset[i] = node.position - this.WindowToGridPosition(current.mousePosition);
                }
            }

            // Selected reroutes
            for (int i = 0; i < this.selectedReroutes.Count; i++)
            {
                dragOffset[Selection.objects.Length + i] = this.selectedReroutes[i].GetPoint() - this.WindowToGridPosition(current.mousePosition);
            }
        }

        /// <summary> Puts all selected nodes in focus. If no nodes are present, resets view and zoom to to origin </summary>
        public void Home()
        {
            var nodes = Selection.objects.Where(o => o is xNode.Node).Cast<xNode.Node>().ToList();
            if (nodes.Count > 0)
            {
                Vector2 minPos = nodes.Select(x => x.position)
                                      .Aggregate((x, y) => new Vector2(Mathf.Min(x.x, y.x), Mathf.Min(x.y, y.y)));
                Vector2 maxPos = nodes
                                .Select(x => x.position + (this.nodeSizes.ContainsKey(x) ? this.nodeSizes[x] : Vector2.zero))
                                .Aggregate((x, y) => new Vector2(Mathf.Max(x.x, y.x), Mathf.Max(x.y, y.y)));
                this.panOffset = -(minPos + (maxPos - minPos) / 2f);
            }
            else
            {
                this.zoom   = 2;
                this.panOffset = Vector2.zero;
            }
        }

        /// <summary> Remove nodes in the graph in Selection.objects</summary>
        public void RemoveSelectedNodes()
        {
            // We need to delete reroutes starting at the highest point index to avoid shifting indices
            this.selectedReroutes = this.selectedReroutes.OrderByDescending(x => x.pointIndex).ToList();
            for (int i = 0; i < this.selectedReroutes.Count; i++)
            {
                this.selectedReroutes[i].RemovePoint();
            }

            this.selectedReroutes.Clear();
            foreach (UnityEngine.Object item in Selection.objects)
            {
                if (item is xNode.Node)
                {
                    xNode.Node node = item as xNode.Node;
                    this.graphEditor.RemoveNode(node);
                }
            }
        }

        /// <summary> Initiate a rename on the currently selected node </summary>
        public void RenameSelectedNode()
        {
            if (Selection.objects.Length == 1 && Selection.activeObject is xNode.Node)
            {
                xNode.Node node = Selection.activeObject as xNode.Node;
                Vector2    size;
                if (this.nodeSizes.TryGetValue(node, out size))
                {
                    RenamePopup.Show(Selection.activeObject, size.x);
                }
                else
                {
                    RenamePopup.Show(Selection.activeObject);
                }
            }
        }

        /// <summary> Draw this node on top of other nodes by placing it last in the graph.nodes list </summary>
        public void MoveNodeToTop(xNode.Node node)
        {
            int index;
            while ((index = this.graph.nodes.IndexOf(node)) != this.graph.nodes.Count - 1)
            {
                this.graph.nodes[index] = this.graph.nodes[index + 1];
                this.graph.nodes[index                              + 1] = node;
            }
        }

        /// <summary> Duplicate selected nodes and select the duplicates </summary>
        public void DuplicateSelectedNodes()
        {
            // Get selected nodes which are part of this graph
            xNode.Node[] selectedNodes = Selection.objects.Select(x => x as xNode.Node)
                                                  .Where(x => x != null && x.graph == this.graph).ToArray();
            if (selectedNodes == null || selectedNodes.Length == 0) return;
            // Get top left node position
            Vector2 topLeftNode = selectedNodes.Select(x => x.position)
                                               .Aggregate((x, y) => new Vector2(Mathf.Min(x.x, y.x),
                                                              Mathf.Min(x.y, y.y)));
            this.InsertDuplicateNodes(selectedNodes, topLeftNode + new Vector2(30, 30));
        }

        public void CopySelectedNodes()
        {
            copyBuffer = Selection.objects.Select(x => x as xNode.Node).Where(x => x != null && x.graph == this.graph)
                                  .ToArray();
        }

        public void PasteNodes(Vector2 pos)
        {
            this.InsertDuplicateNodes(copyBuffer, pos);
        }

        private void InsertDuplicateNodes(xNode.Node[] nodes, Vector2 topLeft)
        {
            if (nodes == null || nodes.Length == 0) return;

            // Get top-left node
            Vector2 topLeftNode = nodes.Select(x => x.position)
                                       .Aggregate((x, y) => new Vector2(Mathf.Min(x.x, y.x), Mathf.Min(x.y, y.y)));
            Vector2 offset = topLeft - topLeftNode;

            UnityEngine.Object[]               newNodes    = new UnityEngine.Object[nodes.Length];
            Dictionary<xNode.Node, xNode.Node> substitutes = new Dictionary<xNode.Node, xNode.Node>();
            for (int i = 0; i < nodes.Length; i++)
            {
                xNode.Node srcNode = nodes[i];
                if (srcNode == null) continue;

                // Check if user is allowed to add more of given node type
                xNode.Node.DisallowMultipleNodesAttribute disallowAttrib;
                Type                                      nodeType = srcNode.GetType();
                if (NodeEditorUtilities.GetAttrib(nodeType, out disallowAttrib))
                {
                    int typeCount = this.graph.nodes.Count(x => x.GetType() == nodeType);
                    if (typeCount >= disallowAttrib.max) continue;
                }

                xNode.Node newNode = this.graphEditor.CopyNode(srcNode);
                substitutes.Add(srcNode, newNode);
                newNode.position = srcNode.position + offset;
                newNodes[i]      = newNode;
            }

            // Walk through the selected nodes again, recreate connections, using the new nodes
            for (int i = 0; i < nodes.Length; i++)
            {
                xNode.Node srcNode = nodes[i];
                if (srcNode == null) continue;
                foreach (xNode.NodePort port in srcNode.Ports)
                {
                    for (int c = 0; c < port.ConnectionCount; c++)
                    {
                        xNode.NodePort inputPort =
                            port.Direction == xNode.NodePort.IO.Input ? port : port.GetConnection(c);
                        xNode.NodePort outputPort =
                            port.Direction == xNode.NodePort.IO.Output ? port : port.GetConnection(c);

                        xNode.Node newNodeIn, newNodeOut;
                        if (substitutes.TryGetValue(inputPort.node,  out newNodeIn) &&
                            substitutes.TryGetValue(outputPort.node, out newNodeOut))
                        {
                            newNodeIn.UpdatePorts();
                            newNodeOut.UpdatePorts();
                            inputPort  = newNodeIn.GetInputPort(inputPort.fieldName);
                            outputPort = newNodeOut.GetOutputPort(outputPort.fieldName);
                        }

                        if (!inputPort.IsConnectedTo(outputPort)) inputPort.Connect(outputPort);
                    }
                }
            }

            EditorUtility.SetDirty(this.graph);
            // Select the new nodes
            Selection.objects = newNodes;
        }

        /// <summary> Draw a connection as we are dragging it </summary>
        public void DrawDraggedConnection()
        {
            if (this.IsDraggingPort)
            {
                Gradient     gradient  = this.graphEditor.GetNoodleGradient(this.draggedOutput, null);
                float        thickness = this.graphEditor.GetNoodleThickness(this.draggedOutput, null);
                NoodlePath   path      = this.graphEditor.GetNoodlePath(this.draggedOutput, null);
                NoodleStroke stroke    = this.graphEditor.GetNoodleStroke(this.draggedOutput, null);

                Rect fromRect;
                if (!this._portConnectionPoints.TryGetValue(this.draggedOutput, out fromRect)) return;
                List<Vector2> gridPoints = new List<Vector2>();
                gridPoints.Add(fromRect.center);
                for (int i = 0; i < this.draggedOutputReroutes.Count; i++)
                {
                    gridPoints.Add(this.draggedOutputReroutes[i]);
                }

                if (this.draggedOutputTarget != null) gridPoints.Add(this.portConnectionPoints[this.draggedOutputTarget].center);
                else gridPoints.Add(this.WindowToGridPosition(Event.current.mousePosition));

                this.DrawNoodle(gradient, path, stroke, thickness, gridPoints);

                Color bgcol = Color.black;
                Color frcol = gradient.colorKeys[0].color;
                bgcol.a = 0.6f;
                frcol.a = 0.6f;

                // Loop through reroute points again and draw the points
                for (int i = 0; i < this.draggedOutputReroutes.Count; i++)
                {
                    // Draw reroute point at position
                    Rect rect = new Rect(this.draggedOutputReroutes[i], new Vector2(16, 16));
                    rect.position = new Vector2(rect.position.x - 8, rect.position.y - 8);
                    rect          = this.GridToWindowRect(rect);

                    NodeEditorGUILayout.DrawPortHandle(rect, bgcol, frcol);
                }
            }
        }

        bool IsHoveringTitle(xNode.Node node)
        {
            Vector2 mousePos = Event.current.mousePosition;
            //Get node position
            Vector2 nodePos = this.GridToWindowPosition(node.position);
            float   width;
            Vector2 size;
            if (this.nodeSizes.TryGetValue(node, out size)) width = size.x;
            else width                                       = 200;
            Rect windowRect = new Rect(nodePos, new Vector2(width / this.zoom, 30 / this.zoom));
            return windowRect.Contains(mousePos);
        }

        /// <summary> Attempt to connect dragged output to target node </summary>
        public void AutoConnect(xNode.Node node)
        {
            if (this.autoConnectOutput == null) return;

            // Find input port of same type
            xNode.NodePort inputPort =
                node.Ports.FirstOrDefault(x => x.IsInput && x.ValueType == this.autoConnectOutput.ValueType);
            // Fallback to input port
            if (inputPort == null) inputPort = node.Ports.FirstOrDefault(x => x.IsInput);
            // Autoconnect if connection is compatible
            if (inputPort != null && inputPort.CanConnectTo(this.autoConnectOutput)) this.autoConnectOutput.Connect(inputPort);

            // Save changes
            EditorUtility.SetDirty(this.graph);
            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
            this.autoConnectOutput = null;
        }
    }
}