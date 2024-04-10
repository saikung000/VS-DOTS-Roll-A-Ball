using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.EditorCommon.Redux;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityEditor.VisualScripting.Editor
{
    public class CreateNodeFromLoopPortAction : IAction
    {
        public readonly IPortModel PortModel;
        public readonly Vector2 Position;
        public readonly IEnumerable<IEdgeModel> EdgesToDelete;
        public readonly IGroupNodeModel GroupModel;

        public CreateNodeFromLoopPortAction(IPortModel portModel, Vector2 position,
            IEnumerable<IEdgeModel> edgesToDelete = null, IGroupNodeModel groupModel = null)
        {
            PortModel = portModel;
            Position = position;
            EdgesToDelete = edgesToDelete ?? Enumerable.Empty<IEdgeModel>();
            GroupModel = groupModel;
        }
    }

    public class CreateNodeFromExecutionPortAction : IAction
    {
        public readonly IPortModel PortModel;
        public readonly Vector2 Position;
        public readonly IEnumerable<IEdgeModel> EdgesToDelete;
        public readonly IGroupNodeModel GroupModel;

        public CreateNodeFromExecutionPortAction(IPortModel portModel, Vector2 position,
            IEnumerable<IEdgeModel> edgesToDelete = null, IGroupNodeModel groupModel = null)
        {
            PortModel = portModel;
            Position = position;
            EdgesToDelete = edgesToDelete ?? Enumerable.Empty<IEdgeModel>();
            GroupModel = groupModel;
        }
    }

    public class CreateInsertLoopNodeAction : IAction
    {
        public readonly IPortModel PortModel;
        public readonly IStackModel StackModel;
        public readonly LoopStackModel LoopStackModel;
        public readonly int Index;
        public readonly IEnumerable<IEdgeModel> EdgesToDelete;
        public readonly IGroupNodeModel GroupModel;

        public CreateInsertLoopNodeAction(IPortModel portModel, IStackModel stackModel, int index,
            LoopStackModel loopStackModel, IEnumerable<IEdgeModel> edgesToDelete = null,
            IGroupNodeModel groupModel = null)
        {
            PortModel = portModel;
            StackModel = stackModel;
            Index = index;
            LoopStackModel = loopStackModel;
            EdgesToDelete = edgesToDelete ?? Enumerable.Empty<IEdgeModel>();
            GroupModel = groupModel;
        }
    }

    public class CreateNodeFromInputPortAction : IAction
    {
        public readonly IPortModel PortModel;
        public readonly IEnumerable<IEdgeModel> EdgesToDelete;
        public readonly Vector2 Position;
        public readonly GraphNodeModelSearcherItem SelectedItem;
        public readonly IGroupNodeModel GroupModel;

        public CreateNodeFromInputPortAction(IPortModel portModel, Vector2 position,
            GraphNodeModelSearcherItem selectedItem, IEnumerable<IEdgeModel> edgesToDelete = null,
            IGroupNodeModel groupModel = null)
        {
            PortModel = portModel;
            Position = position;
            SelectedItem = selectedItem;
            EdgesToDelete = edgesToDelete ?? Enumerable.Empty<IEdgeModel>();
            GroupModel = groupModel;
        }
    }

    public class CreateStackedNodeFromOutputPortAction : IAction
    {
        public readonly IPortModel PortModel;
        public readonly IStackModel StackModel;
        public readonly int Index;
        public readonly StackNodeModelSearcherItem SelectedItem;
        public readonly IEnumerable<IEdgeModel> EdgesToDelete;
        public readonly IGroupNodeModel GroupModel;

        public CreateStackedNodeFromOutputPortAction(IPortModel portModel, IStackModel stackModel, int index,
            StackNodeModelSearcherItem selectedItem, IEnumerable<IEdgeModel> edgesToDelete = null,
            IGroupNodeModel groupModel = null)
        {
            PortModel = portModel;
            StackModel = stackModel;
            Index = index;
            SelectedItem = selectedItem;
            EdgesToDelete = edgesToDelete ?? Enumerable.Empty<IEdgeModel>();
            GroupModel = groupModel;
        }
    }

    public class CreateNodeFromOutputPortAction : IAction
    {
        public readonly IPortModel PortModel;
        public readonly Vector2 Position;
        public readonly GraphNodeModelSearcherItem SelectedItem;
        public readonly IEnumerable<IEdgeModel> EdgesToDelete;
        public readonly IGroupNodeModel GroupModel;

        public CreateNodeFromOutputPortAction(IPortModel portModel, Vector2 position,
            GraphNodeModelSearcherItem selectedItem, IEnumerable<IEdgeModel> edgesToDelete = null,
            IGroupNodeModel groupModel = null)
        {
            PortModel = portModel;
            Position = position;
            SelectedItem = selectedItem;
            EdgesToDelete = edgesToDelete ?? Enumerable.Empty<IEdgeModel>();
            GroupModel = groupModel;
        }
    }

    public class CreateEdgeAction : IAction
    {
        public readonly IPortModel InputPortModel;
        public readonly IPortModel OutputPortModel;
        public readonly List<IEdgeModel> EdgeModelsToDelete;

        public CreateEdgeAction(IPortModel inputPortModel, IPortModel outputPortModel,
            List<IEdgeModel> edgeModelsToDelete = null)
        {
            Assert.IsTrue(inputPortModel.Direction == Direction.Input);
            Assert.IsTrue(outputPortModel.Direction == Direction.Output);
            InputPortModel = inputPortModel;
            OutputPortModel = outputPortModel;
            EdgeModelsToDelete = edgeModelsToDelete;
        }
    }

    public class SplitEdgeAndInsertNodeAction : IAction
    {
        public readonly IEdgeModel EdgeModel;
        public readonly INodeModel NodeModel;

        public SplitEdgeAndInsertNodeAction(IEdgeModel edgeModel, INodeModel nodeModel)
        {
            EdgeModel = edgeModel;
            NodeModel = nodeModel;
        }
    }

    public class CreateNodeOnEdgeAction : IAction
    {
        public readonly IEdgeModel EdgeModel;
        public readonly Vector2 Position;
        public readonly GraphNodeModelSearcherItem SelectedItem;

        public CreateNodeOnEdgeAction(IEdgeModel edgeModel, Vector2 position,
            GraphNodeModelSearcherItem selectedItem)
        {
            EdgeModel = edgeModel;
            Position = position;
            SelectedItem = selectedItem;
        }
    }
}
