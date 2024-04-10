using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.Assertions;
using static UnityEditor.VisualScripting.Model.VSPreferences;

namespace UnityEditor.VisualScripting.Editor
{
    static class EdgeReducers
    {
        const int k_NodeOffset = 60;
        const int k_StackOffset = 120;

        public static void Register(Store store)
        {
            store.Register<CreateNodeFromLoopPortAction>(CreateNodeFromLoopPort);
            store.Register<CreateInsertLoopNodeAction>(CreateInsertLoopNode);
            store.Register<CreateNodeFromExecutionPortAction>(CreateNodeFromExecutionPort);
            store.Register<CreateNodeFromInputPortAction>(CreateGraphNodeFromInputPort);
            store.Register<CreateStackedNodeFromOutputPortAction>(CreateStackedNodeFromOutputPort);
            store.Register<CreateNodeFromOutputPortAction>(CreateNodeFromOutputPort);
            store.Register<CreateEdgeAction>(CreateEdge);
            store.Register<SplitEdgeAndInsertNodeAction>(SplitEdgeAndInsertNode);
            store.Register<CreateNodeOnEdgeAction>(CreateNodeOnEdge);
        }

        static State CreateNodeFromLoopPort(State previousState, CreateNodeFromLoopPortAction action)
        {
            var graphModel = (VSGraphModel)previousState.CurrentGraphModel;
            graphModel.DeleteEdges(action.EdgesToDelete);

            var stackPosition = action.Position - Vector2.right * k_StackOffset;

            if (action.PortModel.NodeModel is LoopNodeModel loopNodeModel)
            {
                var loopStackType = loopNodeModel.MatchingStackType;
                var loopStack = graphModel.CreateLoopStack(loopStackType, stackPosition);

                AddToGroup(new[] { loopStack }, action.GroupModel);

                graphModel.CreateEdge(loopStack.InputPort, action.PortModel);
            }
            else
            {
                var stack = graphModel.CreateStack(null, stackPosition);
                graphModel.CreateEdge(stack.InputPorts[0], action.PortModel);
            }

            graphModel.LastChanges?.ChangedElements.Add(action.PortModel.NodeModel);

            return previousState;
        }

        static State CreateInsertLoopNode(State previousState, CreateInsertLoopNodeAction action)
        {
            var graphModel = (VSGraphModel)previousState.CurrentGraphModel;
            graphModel.DeleteEdges(action.EdgesToDelete);

            var loopNode = ((StackBaseModel)action.StackModel).CreateStackedNode(
                action.LoopStackModel.MatchingStackedNodeType, "", action.Index);

            AddToGroup(new[] { loopNode }, action.GroupModel);

            graphModel.CreateEdge(action.PortModel, loopNode.OutputsByDisplayOrder.First());
            graphModel.LastChanges?.ChangedElements.Add(action.PortModel.NodeModel);

            return previousState;
        }

        static State CreateNodeFromExecutionPort(State previousState, CreateNodeFromExecutionPortAction action)
        {
            var graphModel = (VSGraphModel)previousState.CurrentGraphModel;
            graphModel.DeleteEdges(action.EdgesToDelete);

            var stackPosition = action.Position - Vector2.right * k_StackOffset;
            var stack = graphModel.CreateStack(string.Empty, stackPosition);

            AddToGroup(new[] { stack }, action.GroupModel);

            if (action.PortModel.Direction == Direction.Output)
                graphModel.CreateEdge(stack.InputPorts[0], action.PortModel);
            else
                graphModel.CreateEdge(action.PortModel, stack.OutputPorts[0]);

            graphModel.LastChanges?.ChangedElements.Add(action.PortModel.NodeModel);

            return previousState;
        }

        static State CreateGraphNodeFromInputPort(State previousState, CreateNodeFromInputPortAction action)
        {
            var graphModel = (VSGraphModel)previousState.CurrentGraphModel;
            graphModel.DeleteEdges(action.EdgesToDelete);

            var position = action.Position - Vector2.up * k_NodeOffset;
            var elementModels = action.SelectedItem.CreateElements.Invoke(
                new GraphNodeCreationData(graphModel, position));

            if (elementModels.Length == 0 || !(elementModels[0] is INodeModel selectedNodeModel))
                return previousState;

            AddToGroup(elementModels.OfType<INodeModel>(), action.GroupModel);

            var outputPortModel = action.PortModel.DataType == TypeHandle.Unknown
                ? selectedNodeModel.OutputsByDisplayOrder.FirstOrDefault()
                : GetFirstPortModelOfType(action.PortModel.DataType, selectedNodeModel.OutputsByDisplayOrder);

            if (outputPortModel != null)
            {
                var newEdge = graphModel.CreateEdge(action.PortModel, outputPortModel);
                if (newEdge != null && previousState.Preferences.GetBool(BoolPref.AutoAlignDraggedEdges))
                    graphModel.LastChanges?.ModelsToAutoAlign.Add(newEdge);
            }

            graphModel.LastChanges?.ChangedElements.Add(action.PortModel.NodeModel);

            return previousState;
        }

        static State CreateStackedNodeFromOutputPort(State previousState, CreateStackedNodeFromOutputPortAction action)
        {
            var graphModel = (VSGraphModel)previousState.CurrentGraphModel;
            graphModel.DeleteEdges(action.EdgesToDelete);

            var elementModels = action.SelectedItem.CreateElements.Invoke(
                new StackNodeCreationData(action.StackModel, action.Index));

            if (elementModels.Length == 0 || !(elementModels[0] is INodeModel selectedNodeModel))
                return previousState;

            AddToGroup(elementModels.OfType<INodeModel>(), action.GroupModel);

            var outputPortModel = action.PortModel;
            var newInput = selectedNodeModel.InputsByDisplayOrder.FirstOrDefault();
            if (newInput != null)
            {
                CreateItemizedNode(previousState, graphModel, newInput, ref outputPortModel);
                var newEdge = graphModel.CreateEdge(newInput, outputPortModel);
                if (newEdge != null && previousState.Preferences.GetBool(BoolPref.AutoAlignDraggedEdges))
                    graphModel.LastChanges?.ModelsToAutoAlign.Add(newEdge);
            }

            graphModel.LastChanges?.ChangedElements.Add(action.PortModel.NodeModel);

            return previousState;
        }

        static State CreateNodeFromOutputPort(State previousState, CreateNodeFromOutputPortAction action)
        {
            var graphModel = (VSGraphModel)previousState.CurrentGraphModel;
            graphModel.DeleteEdges(action.EdgesToDelete);

            var position = action.Position - Vector2.up * k_NodeOffset;
            var elementModels = action.SelectedItem.CreateElements.Invoke(
                new GraphNodeCreationData(graphModel, position));

            if (!elementModels.Any() || !(elementModels[0] is INodeModel selectedNodeModel))
                return previousState;

            AddToGroup(elementModels.OfType<INodeModel>(), action.GroupModel);

            var inputPortModel = selectedNodeModel is FunctionCallNodeModel
                ? GetFirstPortModelOfType(action.PortModel.DataType, selectedNodeModel.InputsByDisplayOrder)
                : selectedNodeModel.InputsByDisplayOrder.FirstOrDefault();

            if (inputPortModel == null)
                return previousState;

            var outputPortModel = action.PortModel;

            CreateItemizedNode(previousState, graphModel, inputPortModel, ref outputPortModel);
            var newEdge = graphModel.CreateEdge(inputPortModel, outputPortModel);

            if (newEdge != null && previousState.Preferences.GetBool(BoolPref.AutoAlignDraggedEdges))
                graphModel.LastChanges?.ModelsToAutoAlign.Add(newEdge);

            graphModel.LastChanges?.ChangedElements.Add(action.PortModel.NodeModel);

            return previousState;
        }

        static void AddToGroup(IEnumerable<INodeModel> nodes, IGroupNodeModel groupModel)
        {
            if (groupModel == null)
                return;

            IEnumerable<INodeModel> nodeModels = nodes.ToList();
            ((GroupNodeModel)groupModel).AddNodes(nodeModels);
        }

        static State CreateNodeOnEdge(State previousState, CreateNodeOnEdgeAction action)
        {
            IEdgeModel edgeModel = action.EdgeModel;

            // Instantiate node
            var graphModel = (VSGraphModel)previousState.CurrentGraphModel;
            var position = action.Position - Vector2.up * k_NodeOffset;
            var elementModels = action.SelectedItem.CreateElements.Invoke(
                new GraphNodeCreationData(graphModel, position));

            if (elementModels.Length == 0 || !(elementModels[0] is INodeModel selectedNodeModel))
                return previousState;

            // Connect input port
            var inputPortModel = selectedNodeModel is FunctionCallNodeModel
                ? selectedNodeModel.InputsByDisplayOrder.FirstOrDefault(p =>
                    p.DataType.Equals(edgeModel.OutputPortModel.DataType))
                : selectedNodeModel.InputsByDisplayOrder.FirstOrDefault();

            if (inputPortModel != null)
                graphModel.CreateEdge(inputPortModel, edgeModel.OutputPortModel);

            // Find first matching output type and connect it
            var outputPortModel = GetFirstPortModelOfType(edgeModel.InputPortModel.DataType,
                selectedNodeModel.OutputsByDisplayOrder);

            if (outputPortModel != null)
                graphModel.CreateEdge(edgeModel.InputPortModel, outputPortModel);

            // Delete old edge
            graphModel.DeleteEdge(edgeModel);

            return previousState;
        }

        static State CreateEdge(State previousState, CreateEdgeAction action)
        {
            var graphModel = (VSGraphModel)previousState.CurrentGraphModel;
            IPortModel outputPortModel = action.OutputPortModel;
            IPortModel inputPortModel = action.InputPortModel;

            if (inputPortModel.NodeModel is LoopStackModel loopStackModel)
            {
                if (!loopStackModel.MatchingStackedNodeType.IsInstanceOfType(outputPortModel.NodeModel))
                    return previousState;
            }

            CreateItemizedNode(previousState, graphModel, inputPortModel, ref outputPortModel);
            graphModel.CreateEdge(inputPortModel, outputPortModel);

            if (action.EdgeModelsToDelete?.Count > 0)
                graphModel.DeleteEdges(action.EdgeModelsToDelete);

            return previousState;
        }

        static State SplitEdgeAndInsertNode(State previousState, SplitEdgeAndInsertNodeAction action)
        {
            Assert.IsTrue(action.NodeModel.InputsById.Count > 0);
            Assert.IsTrue(action.NodeModel.OutputsById.Count > 0);

            var graphModel = ((VSGraphModel)previousState.CurrentGraphModel);
            graphModel.CreateEdge(action.EdgeModel.InputPortModel, action.NodeModel.OutputsByDisplayOrder.First());
            graphModel.CreateEdge(action.NodeModel.InputsByDisplayOrder.First(), action.EdgeModel.OutputPortModel);
            graphModel.DeleteEdge(action.EdgeModel);

            return previousState;
        }

        [CanBeNull]
        static IPortModel GetFirstPortModelOfType(TypeHandle typeHandle, IEnumerable<IPortModel> portModels)
        {
            Stencil stencil = portModels.First().GraphModel.Stencil;
            IPortModel unknownPortModel = null;

            // Return the first matching Input portModel
            // If no match was found, return the first Unknown typed portModel
            // Else return null.
            foreach (IPortModel portModel in portModels)
            {
                if (portModel.DataType == TypeHandle.Unknown && unknownPortModel == null)
                {
                    unknownPortModel = portModel;
                }

                if (typeHandle.IsAssignableFrom(portModel.DataType, stencil))
                {
                    return portModel;
                }
            }

            return unknownPortModel;
        }

        static void CreateItemizedNode(State state, VSGraphModel graphModel, IPortModel inputPortModel, ref IPortModel outputPortModel)
        {
            bool wasItemized = false;
            ItemizeOptions currentItemizeOptions = state.Preferences.CurrentItemizeOptions;
            INodeModel nodeToConnect = null;
            // automatically itemize, i.e. duplicate variables as they get connected
            if (outputPortModel.Connected && currentItemizeOptions != ItemizeOptions.Nothing)
            {
                nodeToConnect = outputPortModel.NodeModel;
                var offset = Vector2.up * k_NodeOffset;

                if (currentItemizeOptions.HasFlag(ItemizeOptions.Constants)
                    && nodeToConnect is ConstantNodeModel constantModel)
                {
                    string newName = string.IsNullOrEmpty(constantModel.Title)
                        ? "Temporary"
                        : constantModel.Title + "Copy";
                    nodeToConnect = graphModel.CreateConstantNode(
                        newName,
                        constantModel.Type.GenerateTypeHandle(graphModel.Stencil),
                        constantModel.Position + offset
                    );
                    ((ConstantNodeModel)nodeToConnect).ObjectValue = constantModel.ObjectValue;
                }
                else if (currentItemizeOptions.HasFlag(ItemizeOptions.Variables)
                    && nodeToConnect is VariableNodeModel variableModel)
                {
                    nodeToConnect = graphModel.CreateVariableNode(variableModel.DeclarationModel,
                        variableModel.Position + offset);
                }
                else if (currentItemizeOptions.HasFlag(ItemizeOptions.Variables)
                        && nodeToConnect is ThisNodeModel thisModel)
                {
                    nodeToConnect = graphModel.CreateNode<ThisNodeModel>("this", thisModel.Position + offset);
                }
                else if (currentItemizeOptions.HasFlag(ItemizeOptions.SystemConstants) &&
                    nodeToConnect is SystemConstantNodeModel sysConstModel)
                {
                    Action<SystemConstantNodeModel> preDefineSetup = m =>
                    {
                        m.ReturnType = sysConstModel.ReturnType;
                        m.DeclaringType = sysConstModel.DeclaringType;
                        m.Identifier = sysConstModel.Identifier;
                    };
                    nodeToConnect = graphModel.CreateNode(sysConstModel.Title, sysConstModel.Position + offset, SpawnFlags.Default, preDefineSetup);
                }

                wasItemized = nodeToConnect != outputPortModel.NodeModel;
                outputPortModel = nodeToConnect.OutputsById[outputPortModel.UniqueId];
            }

            GroupNodeModel groupNodeModel = null;
            if (wasItemized)
            {
                if (inputPortModel.NodeModel.IsGrouped)
                    groupNodeModel = (GroupNodeModel)inputPortModel.NodeModel.GroupNodeModel;
                else if (inputPortModel.NodeModel.IsStacked && inputPortModel.NodeModel.ParentStackModel.IsGrouped)
                    groupNodeModel = (GroupNodeModel)inputPortModel.NodeModel.ParentStackModel.GroupNodeModel;
            }

            if (groupNodeModel != null)
                groupNodeModel.AddNode(nodeToConnect);
        }
    }
}
