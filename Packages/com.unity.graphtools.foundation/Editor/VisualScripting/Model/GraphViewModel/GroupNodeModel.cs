using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VisualScripting.Model;
using UnityEngine;

namespace UnityEditor.VisualScripting.GraphViewModel
{
    [Serializable]
    public class GroupNodeModel : NodeModel, IGroupNodeModel
    {
        [SerializeField]
        protected List<NodeModel> m_NodeModels = new List<NodeModel>();

        public IEnumerable<INodeModel> NodeModels => m_NodeModels;
        // Nested group are not supported, so we pretend that we are already grouped.
        public override bool IsGrouped => true;

        public void MoveDelta(Vector2 delta)
        {
            Undo.RegisterCompleteObjectUndo(NodeAssetReference, "Move");
            Position = Position + delta;
        }

        public void AddNodes(IEnumerable<INodeModel> models)
        {
            foreach (var model in models)
            {
                AddNode(model);
            }
        }

        public void AddNode(INodeModel nodeModel)
        {
            //TODO UNDO
            var model = (NodeModel)nodeModel;
            Undo.RegisterCompleteObjectUndo(NodeAssetReference, "Add node(s) to group");
            Undo.RegisterCompleteObjectUndo(model.NodeAssetReference, "Add node(s) to group");
            model.GroupNodeModel = this;
            m_NodeModels.Add(model);
            ((VSGraphModel)GraphModel).LastChanges.ChangedElements.Add(this);
        }

        public void RemoveNodes(IEnumerable<INodeModel> models)
        {
            foreach (var model in models)
            {
                RemoveNode(model);
            }
        }

        public static void Ungroup(IEnumerable<INodeModel> nodeModels)
        {
            foreach (IGrouping<GroupNodeModel,INodeModel> nodeModelsGrouping in nodeModels
                .GroupBy(m => m.GroupNodeModel as GroupNodeModel)
                .Where(g => g.Key != null))
            {
                nodeModelsGrouping.Key.RemoveNodes(nodeModelsGrouping);
            }
        }

        public void RemoveNode(INodeModel nodeModel)
        {
            //TODO UNDO
            var model = (NodeModel)nodeModel;
            Undo.RegisterCompleteObjectUndo(NodeAssetReference, "Remove Node(s) from group");
            Undo.RegisterCompleteObjectUndo(model.NodeAssetReference, "Remove Node(s) from group");
            model.GroupNodeModel = null;
            m_NodeModels.Remove(model);
            ((VSGraphModel)GraphModel).LastChanges.ChangedElements.Add(this);
        }

        public void Rename(string newName)
        {
            Undo.RegisterCompleteObjectUndo(NodeAssetReference, "Rename group");
            Title = newName;
            ((VSGraphModel)GraphModel).LastChanges.ChangedElements.Add(this);
        }

        public override void Destroy()
        {
            foreach (var nodeModel in NodeModels.OfType<NodeModel>())
                nodeModel.GroupNodeModel = null;

            base.Destroy();
        }
    }
}
