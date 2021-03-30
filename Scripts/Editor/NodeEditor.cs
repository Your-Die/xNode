using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;

#endif

namespace XNodeEditor
{
    /// <summary> Base class to derive custom Node editors from. Use this to create your own custom inspectors and editors for your nodes. </summary>
    [CustomNodeEditor(typeof(xNode.Node))]
    public class NodeEditor : Internal.NodeEditorBase<NodeEditor, NodeEditor.CustomNodeEditorAttribute,
        xNode.Node>
    {
        /// <summary> Fires every whenever a node was modified through the editor </summary>
        public static Action<xNode.Node> onUpdateNode;

        public readonly static Dictionary<xNode.NodePort, Vector2> portPositions =
            new Dictionary<xNode.NodePort, Vector2>();

#if ODIN_INSPECTOR
        protected internal static bool inNodeEditor = false;
#endif

        public new virtual void OnHeaderGUI()
        {
            GUILayout.Label(this.target.name, NodeEditorResources.styles.nodeHeader, GUILayout.Height(30));
        }

        /// <summary> Draws standard field editors for all public fields </summary>
        public virtual void OnBodyGUI()
        {
#if ODIN_INSPECTOR
            inNodeEditor = true;
#endif

            // Unity specifically requires this to save/update any serial object.
            // serializedObject.Update(); must go at the start of an inspector gui, and
            // serializedObject.ApplyModifiedProperties(); goes at the end.
            this.serializedObject.Update();
            string[] excludes = { "m_Script", "graph", "position", "ports" };

#if ODIN_INSPECTOR
            InspectorUtilities.BeginDrawPropertyTree(this.objectTree, true);
            GUIHelper.PushLabelWidth(84);
            this.objectTree.Draw(true);
            InspectorUtilities.EndDrawPropertyTree(this.objectTree);
            GUIHelper.PopLabelWidth();
#else
            // Iterate through serialized properties and draw them like the Inspector (But with ports)
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren)) {
                enterChildren = false;
                if (excludes.Contains(iterator.name)) continue;
                NodeEditorGUILayout.PropertyField(iterator, true);
            }
#endif

            // Iterate through dynamic ports and draw them in the order in which they are serialized
            foreach (xNode.NodePort dynamicPort in this.target.DynamicPorts)
            {
                if (NodeEditorGUILayout.IsDynamicPortListPort(dynamicPort)) continue;
                NodeEditorGUILayout.PortField(dynamicPort);
            }

            this.serializedObject.ApplyModifiedProperties();

#if ODIN_INSPECTOR
            // Call repaint so that the graph window elements respond properly to layout changes coming from Odin
            if (GUIHelper.RepaintRequested)
            {
                GUIHelper.ClearRepaintRequest();
                this.window.Repaint();
            }
#endif

#if ODIN_INSPECTOR
            inNodeEditor = false;
#endif
        }

        public virtual int GetWidth()
        {
            Type type = this.target.GetType();
            int  width;
            if (type.TryGetAttributeWidth(out width)) return width;
            else return 208;
        }

        /// <summary> Returns color for target node </summary>
        public virtual Color GetTint()
        {
            // Try get color from [NodeTint] attribute
            var  type = this.target.GetType();

            if (type.TryGetAttributeTint(out var color))
                return color;

            // Return default color (grey)
            return NodeEditorPreferences.GetSettings().tintColor;
        }

        public virtual GUIStyle GetBodyStyle()
        {
            return NodeEditorResources.styles.nodeBody;
        }

        public virtual GUIStyle GetBodyHighlightStyle()
        {
            return NodeEditorResources.styles.nodeHighlight;
        }

        /// <summary> Override to display custom node header tooltips </summary>
        public virtual string GetHeaderTooltip()
        {
            return null;
        }

        /// <summary> Add items for the context menu when right-clicking this node. Override to add custom menu items. </summary>
        public virtual void AddContextMenuItems(GenericMenu menu)
        {
            bool canRemove = true;
            // Actions if only one node is selected
            if (Selection.objects.Length == 1 && Selection.activeObject is xNode.Node)
            {
                xNode.Node node = Selection.activeObject as xNode.Node;
                menu.AddItem(new GUIContent("Move To Top"), false, () => NodeEditorWindow.current.MoveNodeToTop(node));
                menu.AddItem(new GUIContent("Rename"),      false, NodeEditorWindow.current.RenameSelectedNode);

                canRemove = NodeGraphEditor.GetEditor(node.graph, NodeEditorWindow.current).CanRemove(node);
            }

            // Add actions to any number of selected nodes
            menu.AddItem(new GUIContent("Copy"),      false, NodeEditorWindow.current.CopySelectedNodes);
            menu.AddItem(new GUIContent("Duplicate"), false, NodeEditorWindow.current.DuplicateSelectedNodes);

            if (canRemove) menu.AddItem(new GUIContent("Remove"), false, NodeEditorWindow.current.RemoveSelectedNodes);
            else menu.AddItem(new GUIContent("Remove"),           false, null);

            // Custom sctions if only one node is selected
            if (Selection.objects.Length == 1 && Selection.activeObject is xNode.Node)
            {
                xNode.Node node = Selection.activeObject as xNode.Node;
                menu.AddCustomContextMenuItems(node);
            }
        }

        /// <summary> Rename the node asset. This will trigger a reimport of the node. </summary>
        public void Rename(string newName)
        {
            if (newName == null || newName.Trim() == "")
                newName = NodeEditorUtilities.NodeDefaultName(this.target.GetType());
            this.target.name = newName;
            this.OnRename();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this.target));
        }

        /// <summary> Called after this node's name has changed. </summary>
        public virtual void OnRename()
        {
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class CustomNodeEditorAttribute : Attribute,
                                                 INodeEditorAttrib
        {
            private Type inspectedType;

            /// <summary> Tells a NodeEditor which Node type it is an editor for </summary>
            /// <param name="inspectedType">Type that this editor can edit</param>
            public CustomNodeEditorAttribute(Type inspectedType)
            {
                this.inspectedType = inspectedType;
            }

            public Type GetInspectedType()
            {
                return this.inspectedType;
            }
        }
    }
}