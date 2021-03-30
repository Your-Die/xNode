using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNodeEditor.Internal;

namespace XNodeEditor
{
    /// <summary> Contains GUI methods </summary>
    public partial class NodeEditorWindow
    {
        public  NodeGraphEditor          graphEditor;
        private List<UnityEngine.Object> selectionCache;
        private List<xNode.Node>         culledNodes;

        /// <summary> 19 if docked, 22 if not </summary>
        private int topPadding => this.isDocked() ? 19 : 22;

        /// <summary> Executed after all other window GUI. Useful if Zoom is ruining your day. Automatically resets after being run.</summary>
        public event Action onLateGUI;

        private static readonly Vector3[] polyLineTempArray = new Vector3[2];

        protected virtual void OnGUI()
        {
            var e = Event.current;
            var m = GUI.matrix;

            if (this.graph == null)
                return;

            this.ValidateGraphEditor();
            this.Controls();

            this.DrawGrid(this.position, this.zoom, this.panOffset);
            this.DrawConnections();
            this.DrawDraggedConnection();
            this.DrawNodes();
            this.DrawSelectionBox();
            this.DrawTooltip();
            this.graphEditor.OnGUI();

            // Run and reset onLateGUI
            if (this.onLateGUI != null)
            {
                this.onLateGUI();
                this.onLateGUI = null;
            }

            GUI.matrix = m;
        }

        public static void BeginZoomed(Rect rect, float zoom, float topPadding)
        {
            GUI.EndClip();

            GUIUtility.ScaleAroundPivot(Vector2.one / zoom, rect.size * 0.5f);
            Vector4 padding = new Vector4(0, topPadding, 0, 0);
            padding *= zoom;
            GUI.BeginClip(new Rect(-((rect.width * zoom) - rect.width) * 0.5f,
                                   -(((rect.height * zoom) - rect.height) * 0.5f) + (topPadding * zoom),
                                   rect.width  * zoom,
                                   rect.height * zoom));
        }

        public static void EndZoomed(Rect rect, float zoom, float topPadding)
        {
            GUIUtility.ScaleAroundPivot(Vector2.one * zoom, rect.size * 0.5f);
            Vector3 offset = new Vector3(
                (((rect.width * zoom) - rect.width) * 0.5f),
                (((rect.height * zoom) - rect.height) * 0.5f) + (-topPadding * zoom) + topPadding,
                0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);
        }

        public void DrawGrid(Rect rect, float zoom, Vector2 panOffset)
        {
            rect.position = Vector2.zero;

            Vector2   center   = rect.size / 2f;
            Texture2D gridTex  = this.graphEditor.GetGridTexture();
            Texture2D crossTex = this.graphEditor.GetSecondaryGridTexture();

            // Offset from origin in tile units
            float xOffset = -(center.x * zoom + panOffset.x)                / gridTex.width;
            float yOffset = ((center.y - rect.size.y) * zoom + panOffset.y) / gridTex.height;

            Vector2 tileOffset = new Vector2(xOffset, yOffset);

            // Amount of tiles
            float tileAmountX = Mathf.Round(rect.size.x * zoom) / gridTex.width;
            float tileAmountY = Mathf.Round(rect.size.y * zoom) / gridTex.height;

            Vector2 tileAmount = new Vector2(tileAmountX, tileAmountY);

            // Draw tiled background
            GUI.DrawTextureWithTexCoords(rect, gridTex,  new Rect(tileOffset,                           tileAmount));
            GUI.DrawTextureWithTexCoords(rect, crossTex, new Rect(tileOffset + new Vector2(0.5f, 0.5f), tileAmount));
        }

        public void DrawSelectionBox()
        {
            if (currentActivity == NodeActivity.DragGrid)
            {
                Vector2 curPos = this.WindowToGridPosition(Event.current.mousePosition);
                Vector2 size   = curPos - this.dragBoxStart;
                Rect    r      = new Rect(this.dragBoxStart, size);
                r.position =  this.GridToWindowPosition(r.position);
                r.size     /= this.zoom;
                Handles.DrawSolidRectangleWithOutline(r, new Color(0, 0, 0, 0.1f), new Color(1, 1, 1, 0.6f));
            }
        }

        public static bool DropdownButton(string name, float width)
        {
            return GUILayout.Button(name, EditorStyles.toolbarDropDown, GUILayout.Width(width));
        }

        /// <summary> Show right-click context menu for hovered reroute </summary>
        void ShowRerouteContextMenu(RerouteReference reroute)
        {
            GenericMenu contextMenu = new GenericMenu();
            contextMenu.AddItem(new GUIContent("Remove"), false, () => reroute.RemovePoint());
            contextMenu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
        }

        /// <summary> Show right-click context menu for hovered port </summary>
        void ShowPortContextMenu(xNode.NodePort hoveredPort)
        {
            GenericMenu contextMenu = new GenericMenu();
            foreach (var port in hoveredPort.GetConnections())
            {
                var name  = port.node.name;
                var index = hoveredPort.GetConnectionIndex(port);
                contextMenu.AddItem(new GUIContent($"Disconnect({name})"), false, () => hoveredPort.Disconnect(index));
            }

            contextMenu.AddItem(new GUIContent("Clear Connections"), false, () => hoveredPort.ClearConnections());
            contextMenu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
        }

        static Vector2 CalculateBezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u   = 1 - t;
            float tt  = t  * t, uu  = u  * u;
            float uuu = uu * u, ttt = tt * t;
            return new Vector2(
                (uuu * p0.x) + (3 * uu * t * p1.x) + (3 * u * tt * p2.x) + (ttt * p3.x),
                (uuu * p0.y) + (3 * uu * t * p1.y) + (3 * u * tt * p2.y) + (ttt * p3.y)
            );
        }

        /// <summary> Draws a line segment without allocating temporary arrays </summary>
        static void DrawAAPolyLineNonAlloc(float thickness, Vector2 p0, Vector2 p1)
        {
            polyLineTempArray[0].x = p0.x;
            polyLineTempArray[0].y = p0.y;
            polyLineTempArray[1].x = p1.x;
            polyLineTempArray[1].y = p1.y;
            Handles.DrawAAPolyLine(thickness, polyLineTempArray);
        }

        /// <summary> Draw a bezier from output to input in grid coordinates </summary>
        public void DrawNoodle(Gradient      gradient, NoodlePath path, NoodleStroke stroke, float thickness,
                               List<Vector2> gridPoints)
        {
            // convert grid points to window points
            for (int i = 0; i < gridPoints.Count; ++i)
                gridPoints[i] = this.GridToWindowPosition(gridPoints[i]);

            Color originalHandlesColor = Handles.color;
            Handles.color = gradient.Evaluate(0f);
            int length = gridPoints.Count;
            switch (path)
            {
                case NoodlePath.Curvy:
                    Vector2 outputTangent = Vector2.right;
                    for (int i = 0; i < length - 1; i++)
                    {
                        Vector2 inputTangent;
                        // Cached most variables that repeat themselves here to avoid so many indexer calls :p
                        Vector2 point_a           = gridPoints[i];
                        Vector2 point_b           = gridPoints[i + 1];
                        float   dist_ab           = Vector2.Distance(point_a, point_b);
                        if (i == 0) outputTangent = this.zoom * dist_ab * 0.01f * Vector2.right;
                        if (i < length - 2)
                        {
                            Vector2 point_c       = gridPoints[i + 2];
                            Vector2 ab            = (point_b - point_a).normalized;
                            Vector2 cb            = (point_b - point_c).normalized;
                            Vector2 ac            = (point_c - point_a).normalized;
                            Vector2 p             = (ab      + cb)                                 * 0.5f;
                            float   tangentLength = (dist_ab + Vector2.Distance(point_b, point_c)) * 0.005f * this.zoom;
                            float side =
                                ((ac.x * (point_b.y - point_a.y)) - (ac.y * (point_b.x - point_a.x)));

                            p            = tangentLength * Mathf.Sign(side) * new Vector2(-p.y, p.x);
                            inputTangent = p;
                        }
                        else
                        {
                            inputTangent = this.zoom * dist_ab * 0.01f * Vector2.left;
                        }

                        // Calculates the tangents for the bezier's curves.
                        float   zoomCoef  = 50 / this.zoom;
                        Vector2 tangent_a = point_a + outputTangent * zoomCoef;
                        Vector2 tangent_b = point_b + inputTangent  * zoomCoef;
                        // Hover effect.
                        int division = Mathf.RoundToInt(.2f * dist_ab) + 3;
                        // Coloring and bezier drawing.
                        int     draw           = 0;
                        Vector2 bezierPrevious = point_a;
                        for (int j = 1; j <= division; ++j)
                        {
                            if (stroke == NoodleStroke.Dashed)
                            {
                                draw++;
                                if (draw >= 2) draw = -2;
                                if (draw < 0) continue;
                                if (draw == 0)
                                    bezierPrevious =
                                        CalculateBezierPoint(point_a, tangent_a, tangent_b, point_b,
                                                             (j - 1f) / (float) division);
                            }

                            if (i == length - 2)
                                Handles.color = gradient.Evaluate((j + 1f) / division);
                            Vector2 bezierNext =
                                CalculateBezierPoint(point_a, tangent_a, tangent_b, point_b, j / (float) division);
                            DrawAAPolyLineNonAlloc(thickness, bezierPrevious, bezierNext);
                            bezierPrevious = bezierNext;
                        }

                        outputTangent = -inputTangent;
                    }

                    break;
                case NoodlePath.Straight:
                    for (int i = 0; i < length - 1; i++)
                    {
                        Vector2 point_a = gridPoints[i];
                        Vector2 point_b = gridPoints[i + 1];
                        // Draws the line with the coloring.
                        Vector2 prev_point = point_a;
                        // Approximately one segment per 5 pixels
                        int segments = (int) Vector2.Distance(point_a, point_b) / 5;
                        segments = Math.Max(segments, 1);

                        int draw = 0;
                        for (int j = 0; j <= segments; j++)
                        {
                            draw++;
                            float   t    = j / (float) segments;
                            Vector2 lerp = Vector2.Lerp(point_a, point_b, t);
                            if (draw > 0)
                            {
                                if (i == length - 2) Handles.color = gradient.Evaluate(t);
                                DrawAAPolyLineNonAlloc(thickness, prev_point, lerp);
                            }

                            prev_point = lerp;
                            if (stroke == NoodleStroke.Dashed && draw >= 2) draw = -2;
                        }
                    }

                    break;
                case NoodlePath.Angled:
                    for (int i = 0; i < length - 1; i++)
                    {
                        if (i == length - 1) continue; // Skip last index
                        if (gridPoints[i].x <= gridPoints[i + 1].x - (50 / this.zoom))
                        {
                            float   midpoint = (gridPoints[i].x + gridPoints[i + 1].x) * 0.5f;
                            Vector2 start_1  = gridPoints[i];
                            Vector2 end_1    = gridPoints[i + 1];
                            start_1.x = midpoint;
                            end_1.x   = midpoint;
                            if (i == length - 2)
                            {
                                DrawAAPolyLineNonAlloc(thickness, gridPoints[i], start_1);
                                Handles.color = gradient.Evaluate(0.5f);
                                DrawAAPolyLineNonAlloc(thickness, start_1, end_1);
                                Handles.color = gradient.Evaluate(1f);
                                DrawAAPolyLineNonAlloc(thickness, end_1, gridPoints[i + 1]);
                            }
                            else
                            {
                                DrawAAPolyLineNonAlloc(thickness, gridPoints[i], start_1);
                                DrawAAPolyLineNonAlloc(thickness, start_1,       end_1);
                                DrawAAPolyLineNonAlloc(thickness, end_1,         gridPoints[i + 1]);
                            }
                        }
                        else
                        {
                            float   midpoint = (gridPoints[i].y + gridPoints[i + 1].y) * 0.5f;
                            Vector2 start_1  = gridPoints[i];
                            Vector2 end_1    = gridPoints[i + 1];
                            start_1.x += 25 / this.zoom;
                            end_1.x   -= 25 / this.zoom;
                            Vector2 start_2 = start_1;
                            Vector2 end_2   = end_1;
                            start_2.y = midpoint;
                            end_2.y   = midpoint;
                            if (i == length - 2)
                            {
                                DrawAAPolyLineNonAlloc(thickness, gridPoints[i], start_1);
                                Handles.color = gradient.Evaluate(0.25f);
                                DrawAAPolyLineNonAlloc(thickness, start_1, start_2);
                                Handles.color = gradient.Evaluate(0.5f);
                                DrawAAPolyLineNonAlloc(thickness, start_2, end_2);
                                Handles.color = gradient.Evaluate(0.75f);
                                DrawAAPolyLineNonAlloc(thickness, end_2, end_1);
                                Handles.color = gradient.Evaluate(1f);
                                DrawAAPolyLineNonAlloc(thickness, end_1, gridPoints[i + 1]);
                            }
                            else
                            {
                                DrawAAPolyLineNonAlloc(thickness, gridPoints[i], start_1);
                                DrawAAPolyLineNonAlloc(thickness, start_1,       start_2);
                                DrawAAPolyLineNonAlloc(thickness, start_2,       end_2);
                                DrawAAPolyLineNonAlloc(thickness, end_2,         end_1);
                                DrawAAPolyLineNonAlloc(thickness, end_1,         gridPoints[i + 1]);
                            }
                        }
                    }

                    break;
                case NoodlePath.ShaderLab:
                    Vector2 start = gridPoints[0];
                    Vector2 end   = gridPoints[length - 1];
                    //Modify first and last point in array so we can loop trough them nicely.
                    gridPoints[0]          = gridPoints[0]          + Vector2.right * (20 / this.zoom);
                    gridPoints[length - 1] = gridPoints[length - 1] + Vector2.left  * (20 / this.zoom);
                    //Draw first vertical lines going out from nodes
                    Handles.color = gradient.Evaluate(0f);
                    DrawAAPolyLineNonAlloc(thickness, start, gridPoints[0]);
                    Handles.color = gradient.Evaluate(1f);
                    DrawAAPolyLineNonAlloc(thickness, end, gridPoints[length - 1]);
                    for (int i = 0; i < length - 1; i++)
                    {
                        Vector2 point_a = gridPoints[i];
                        Vector2 point_b = gridPoints[i + 1];
                        // Draws the line with the coloring.
                        Vector2 prev_point = point_a;
                        // Approximately one segment per 5 pixels
                        int segments = (int) Vector2.Distance(point_a, point_b) / 5;
                        segments = Math.Max(segments, 1);

                        int draw = 0;
                        for (int j = 0; j <= segments; j++)
                        {
                            draw++;
                            float   t    = j / (float) segments;
                            Vector2 lerp = Vector2.Lerp(point_a, point_b, t);
                            if (draw > 0)
                            {
                                if (i == length - 2) Handles.color = gradient.Evaluate(t);
                                DrawAAPolyLineNonAlloc(thickness, prev_point, lerp);
                            }

                            prev_point = lerp;
                            if (stroke == NoodleStroke.Dashed && draw >= 2) draw = -2;
                        }
                    }

                    gridPoints[0]          = start;
                    gridPoints[length - 1] = end;
                    break;
            }

            Handles.color = originalHandlesColor;
        }

        /// <summary> Draws all connections </summary>
        public void DrawConnections()
        {
            Vector2 mousePos = Event.current.mousePosition;
            List<RerouteReference> selection = this.preBoxSelectionReroute != null
                ? new List<RerouteReference>(this.preBoxSelectionReroute)
                : new List<RerouteReference>();
            this.hoveredReroute = new RerouteReference();

            List<Vector2> gridPoints = new List<Vector2>(2);

            Color col = GUI.color;
            foreach (xNode.Node node in this.graph.nodes)
            {
                //If a null node is found, return. This can happen if the nodes associated script is deleted. It is currently not possible in Unity to delete a null asset.
                if (node == null) continue;

                // Draw full connections and output > reroute
                foreach (xNode.NodePort output in node.Outputs)
                {
                    //Needs cleanup. Null checks are ugly
                    if (!this._portConnectionPoints.TryGetValue(output, out var fromRect))
                        continue;

                    Color portColor = this.graphEditor.GetPortColor(output);
                    for (int k = 0; k < output.ConnectionCount; k++)
                    {
                        xNode.NodePort input = output.GetConnection(k);

                        Gradient     noodleGradient  = this.graphEditor.GetNoodleGradient(output, input);
                        float        noodleThickness = this.graphEditor.GetNoodleThickness(output, input);
                        NoodlePath   noodlePath      = this.graphEditor.GetNoodlePath(output, input);
                        NoodleStroke noodleStroke    = this.graphEditor.GetNoodleStroke(output, input);

                        // Error handling
                        if (input == null)
                            continue; //If a script has been updated and the port doesn't exist, it is removed and null is returned. If this happens, return.
                        if (!input.IsConnectedTo(output)) input.Connect(output);
                        if (!this._portConnectionPoints.TryGetValue(input, out var toRect)) continue;

                        List<Vector2> reroutePoints = output.GetReroutePoints(k);

                        gridPoints.Clear();
                        gridPoints.Add(fromRect.center);
                        gridPoints.AddRange(reroutePoints);
                        gridPoints.Add(toRect.center);
                        this.DrawNoodle(noodleGradient, noodlePath, noodleStroke, noodleThickness, gridPoints);

                        // Loop through reroute points again and draw the points
                        for (int i = 0; i < reroutePoints.Count; i++)
                        {
                            RerouteReference rerouteRef = new RerouteReference(output, k, i);
                            // Draw reroute point at position
                            Rect rect = new Rect(reroutePoints[i], new Vector2(12, 12));
                            rect.position = new Vector2(rect.position.x - 6, rect.position.y - 6);
                            rect          = this.GridToWindowRect(rect);

                            // Draw selected reroute points with an outline
                            if (this.selectedReroutes.Contains(rerouteRef))
                            {
                                GUI.color = NodeEditorPreferences.GetSettings().highlightColor;
                                GUI.DrawTexture(rect, NodeEditorResources.dotOuter);
                            }

                            GUI.color = portColor;
                            GUI.DrawTexture(rect, NodeEditorResources.dot);
                            if (rect.Overlaps(this.selectionBox)) selection.Add(rerouteRef);
                            if (rect.Contains(mousePos)) this.hoveredReroute = rerouteRef;
                        }
                    }
                }
            }

            GUI.color = col;
            if (Event.current.type != EventType.Layout && currentActivity == NodeActivity.DragGrid)
                this.selectedReroutes = selection;
        }

        private void DrawNodes()
        {
            var e = Event.current;
            if (e.type == EventType.Layout)
            {
                this.selectionCache = new List<UnityEngine.Object>(Selection.objects);
            }

            System.Reflection.MethodInfo onValidate = null;
            if (Selection.activeObject != null && Selection.activeObject is xNode.Node)
            {
                onValidate = Selection.activeObject.GetType().GetMethod("OnValidate");
                if (onValidate != null) EditorGUI.BeginChangeCheck();
            }

            BeginZoomed(this.position, this.zoom, this.topPadding);

            var mousePos = Event.current.mousePosition;

            if (e.type != EventType.Layout)
            {
                this.hoveredNode = null;
                this.hoveredPort = null;
            }

            var preSelection = this.preBoxSelection != null
                ? new List<UnityEngine.Object>(this.preBoxSelection)
                : new List<UnityEngine.Object>();

            // Selection box stuff
            var boxStartPos = this.GridToWindowPositionNoClipped(this.dragBoxStart);
            var boxSize     = mousePos - boxStartPos;
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

            var selectionBox = new Rect(boxStartPos, boxSize);

            //Save guiColor so we can revert it
            var guiColor = GUI.color;

            var removeEntries = new List<xNode.NodePort>();

            if (e.type == EventType.Layout)
                this.culledNodes = new List<xNode.Node>();

            for (var n = 0; n < this.graph.nodes.Count; n++)
            {
                // Skip null nodes. The user could be in the process of renaming scripts, so removing them at this point is not advisable.
                if (this.graph.nodes[n] == null)
                    continue;

                if (n >= this.graph.nodes.Count)
                    return;

                var node = this.graph.nodes[n];

                // Culling
                if (e.type == EventType.Layout)
                {
                    // Cull unselected nodes outside view
                    if (!Selection.Contains(node) && this.ShouldBeCulled(node))
                    {
                        this.culledNodes.Add(node);
                        continue;
                    }
                }
                else if (this.culledNodes.Contains(node)) continue;

                if (e.type == EventType.Repaint)
                {
                    removeEntries.Clear();
                    foreach (var kvp in this._portConnectionPoints)
                        if (kvp.Key.node == node)
                            removeEntries.Add(kvp.Key);
                    foreach (var k in removeEntries) this._portConnectionPoints.Remove(k);
                }

                var nodeEditor = NodeEditor.GetEditor(node, this);

                NodeEditor.portPositions.Clear();

                // Set default label width. This is potentially overridden in OnBodyGUI
                EditorGUIUtility.labelWidth = 84;

                //Get node position
                var nodePos = this.GridToWindowPositionNoClipped(node.position);

                GUILayout.BeginArea(new Rect(nodePos, new Vector2(nodeEditor.GetWidth(), 4000)));

                var selected = this.selectionCache.Contains(this.graph.nodes[n]);

                if (selected)
                {
                    var style          = new GUIStyle(nodeEditor.GetBodyStyle());
                    var highlightStyle = new GUIStyle(nodeEditor.GetBodyHighlightStyle());
                    highlightStyle.padding = style.padding;
                    style.padding          = new RectOffset();
                    GUI.color              = nodeEditor.GetTint();
                    GUILayout.BeginVertical(style);
                    GUI.color = NodeEditorPreferences.GetSettings().highlightColor;
                    GUILayout.BeginVertical(new GUIStyle(highlightStyle));
                }
                else
                {
                    var style = new GUIStyle(nodeEditor.GetBodyStyle());
                    GUI.color = nodeEditor.GetTint();
                    GUILayout.BeginVertical(style);
                }

                GUI.color = guiColor;
                EditorGUI.BeginChangeCheck();

                //Draw node contents
                nodeEditor.OnHeaderGUI();
                nodeEditor.OnBodyGUI();

                //If user changed a value, notify other scripts through onUpdateNode
                if (EditorGUI.EndChangeCheck())
                {
                    if (NodeEditor.onUpdateNode != null) NodeEditor.onUpdateNode(node);
                    EditorUtility.SetDirty(node);
                    nodeEditor.serializedObject.ApplyModifiedProperties();
                }

                GUILayout.EndVertical();

                //Cache data about the node for next frame
                if (e.type == EventType.Repaint)
                {
                    var size = GUILayoutUtility.GetLastRect().size;
                    if (this.nodeSizes.ContainsKey(node))
                        this.nodeSizes[node] = size;
                    else
                        this.nodeSizes.Add(node, size);

                    foreach (var kvp in NodeEditor.portPositions)
                    {
                        var portHandlePos = kvp.Value;
                        portHandlePos += node.position;
                        var rect = new Rect(portHandlePos.x - 8, portHandlePos.y - 8, 16, 16);
                        this.portConnectionPoints[kvp.Key] = rect;
                    }
                }

                if (selected) GUILayout.EndVertical();

                if (e.type != EventType.Layout)
                {
                    //Check if we are hovering this node
                    var nodeSize                                        = GUILayoutUtility.GetLastRect().size;
                    var windowRect                                      = new Rect(nodePos, nodeSize);
                    if (windowRect.Contains(mousePos)) this.hoveredNode = node;

                    //If dragging a selection box, add nodes inside to selection
                    if (currentActivity == NodeActivity.DragGrid)
                    {
                        if (windowRect.Overlaps(selectionBox)) preSelection.Add(node);
                    }

                    //Check if we are hovering any of this nodes ports
                    //Check input ports
                    foreach (var input in node.Inputs)
                    {
                        //Check if port rect is available
                        if (!this.portConnectionPoints.ContainsKey(input)) continue;
                        var r =
                            this.GridToWindowRectNoClipped(this.portConnectionPoints[input]);
                        if (r.Contains(mousePos)) this.hoveredPort = input;
                    }

                    //Check all output ports
                    foreach (var output in node.Outputs)
                    {
                        //Check if port rect is available
                        if (!this.portConnectionPoints.ContainsKey(output)) continue;
                        var r =
                            this.GridToWindowRectNoClipped(this.portConnectionPoints[output]);
                        if (r.Contains(mousePos)) this.hoveredPort = output;
                    }
                }

                GUILayout.EndArea();
            }

            if (e.type != EventType.Layout && currentActivity == NodeActivity.DragGrid)
                Selection.objects = preSelection.ToArray();
            EndZoomed(this.position, this.zoom, this.topPadding);

            //If a change in is detected in the selected node, call OnValidate method.
            //This is done through reflection because OnValidate is only relevant in editor,
            //and thus, the code should not be included in build.
            if (onValidate != null && EditorGUI.EndChangeCheck()) onValidate.Invoke(Selection.activeObject, null);
        }

        private bool ShouldBeCulled(xNode.Node node)
        {
            Vector2 nodePos = this.GridToWindowPositionNoClipped(node.position);
            if (nodePos.x      / this._zoom > this.position.width) return true;  // Right
            else if (nodePos.y / this._zoom > this.position.height) return true; // Bottom
            else if (this.nodeSizes.ContainsKey(node))
            {
                Vector2 size = this.nodeSizes[node];
                if (nodePos.x      + size.x < 0) return true; // Left
                else if (nodePos.y + size.y < 0) return true; // Top
            }

            return false;
        }

        private void DrawTooltip()
        {
            if (!NodeEditorPreferences.GetSettings().portTooltips || this.graphEditor == null)
                return;
            string tooltip = null;
            if (this.hoveredPort != null)
            {
                tooltip = this.graphEditor.GetPortTooltip(this.hoveredPort);
            }
            else if (this.hoveredNode != null && this.IsHoveringNode && this.IsHoveringTitle(this.hoveredNode))
            {
                tooltip = NodeEditor.GetEditor(this.hoveredNode, this).GetHeaderTooltip();
            }

            if (string.IsNullOrEmpty(tooltip)) return;
            GUIContent content = new GUIContent(tooltip);
            Vector2    size    = NodeEditorResources.styles.tooltip.CalcSize(content);
            size.x += 8;
            Rect rect = new Rect(Event.current.mousePosition - (size), size);
            EditorGUI.LabelField(rect, content, NodeEditorResources.styles.tooltip);
            this.Repaint();
        }
    }
}