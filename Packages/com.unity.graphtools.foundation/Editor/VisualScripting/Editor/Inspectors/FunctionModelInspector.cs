using System;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.NodeAssets;

namespace UnityEditor.VisualScripting.Editor
{
    [CustomEditor(typeof(GenericStackAsset<>), true)]
    class StackModelInspector : NodeModelInspector
    {
        protected override bool DoDefaultInspector => false;
    }

    [CustomEditor(typeof(LoopNodeAsset<>), true)]
    class LoopStackModelInspector : NodeModelInspector
    {
        protected override bool DoDefaultInspector => false;
    }

    [CustomEditor(typeof(GenericFunctionAsset<>), true)]
    class FunctionModelInspector : NodeModelInspector
    {
        protected override bool DoDefaultInspector => false;

        protected override void GraphElementInspectorGUI(Action refreshUI)
        {
            if (!(target is AbstractNodeAsset asset) || !(asset.Model is FunctionModel inv))
                return;

            inv.Title = EditorGUILayout.DelayedTextField("Name", inv.Title);

            var graph = (VSGraphModel)inv.GraphModel;
            graph.Stencil.TypeEditor(inv.ReturnType, (theType, i) =>
            {
                inv.ReturnType = theType;

                // TODO: update return nodes
//                foreach (return nodes)
//                    return.UpdateTypeFromDeclaration();
                refreshUI();
            });

            if (graph != null)
            {
                inv.EnableProfiling = EditorGUILayout.Toggle("Enable Profiling", inv.EnableProfiling);
            }

            var property = serializedObject.FindProperty("m_NodeModel.m_NodeModels");
            EditorGUILayout.PropertyField(property, true);
        }
    }
}
