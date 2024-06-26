using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Model;
using UnityEngine;
using Port = UnityEditor.Experimental.GraphView.Port;

namespace UnityEditor.VisualScripting.GraphViewModel
{
    public enum LoopConnectionType
    {
        None,
        Stack,
        LoopStack
    }

    public interface INodeModel : IGraphElementModel, IUndoRedoAware
    {
        ModelState State { get; }
        AbstractNodeAsset NodeAssetReference { get; }
        IStackModel ParentStackModel { get; }
        IGroupNodeModel GroupNodeModel { get; }
        string Title { get; }
        Vector2 Position { get; set; }
        IReadOnlyDictionary<string, IPortModel> InputsById { get; }
        IReadOnlyDictionary<string, IPortModel> OutputsById { get; }
        IReadOnlyList<IPortModel> InputsByDisplayOrder { get; }
        IReadOnlyList<IPortModel> OutputsByDisplayOrder { get; }
        bool IsStacked { get; }
        bool IsGrouped { get; }
        bool IsCondition { get; }
        bool IsInsertLoop { get; }
        LoopConnectionType LoopConnectionType { get; }
        bool IsBranchType { get; }
        Color Color { get; set; }
        bool HasUserColor { get; set; }
        int OriginalInstanceId { get; set; }

        void OnConnection(IPortModel selfConnectedPortModel, IPortModel otherConnectedPortModel);
        void OnDisconnection(IPortModel selfConnectedPortModel, IPortModel otherConnectedPortModel);

        Port.Capacity GetPortCapacity(PortModel portModel);
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class INodeModelExtensions
    {
        public static IEnumerable<IPortModel> GetPortModels(this INodeModel node)
        {
            return node.InputsByDisplayOrder.Concat(node.OutputsByDisplayOrder);
        }

        public static IEnumerable<IEdgeModel> GetConnectedEdges(this INodeModel nodeModel)
        {
            var graphModel = nodeModel.GraphModel;
            return nodeModel.GetPortModels().SelectMany(p => graphModel.GetEdgesConnections(p));
        }

        public static IEnumerable<INodeModel> GetConnectedNodes(this INodeModel nodeModel)
        {
            foreach (IPortModel portModel in nodeModel.GetPortModels())
            {
                foreach (IPortModel connectionPortModel in portModel.ConnectionPortModels)
                {
                    yield return connectionPortModel.NodeModel;
                }
            }
        }
    }
}
