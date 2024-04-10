using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace UnityEditor.VisualScripting.GraphViewModel
{
    [Serializable]
    public abstract class StackBaseModel : NodeModel, IStackModel
    {
        [SerializeField]
        List<AbstractNodeAsset> m_NodeModels = new List<AbstractNodeAsset>();

        public override CapabilityFlags Capabilities => CapabilityFlags.Selectable | CapabilityFlags.Deletable |
                                                        CapabilityFlags.Movable | CapabilityFlags.DeletableWhenEmpty;

        public virtual IFunctionModel OwningFunctionModel { get; set; }

        public IReadOnlyList<INodeModel> NodeModels => new NodeAssetListAdapter<INodeModel>(m_NodeModels);

        List<IPortModel> m_OutputPorts = new List<IPortModel>();
        List<IPortModel> m_InputPorts = new List<IPortModel>();

        public virtual bool AcceptNode(Type nodeType)
        {
            // Do not accept more than 1 branched node
            bool isBranchedNode = Attribute.IsDefined(nodeType, typeof(BranchedNodeAttribute));
            foreach (var child in m_NodeModels)
            {
                if (isBranchedNode && Attribute.IsDefined(child.GetType(), typeof(BranchedNodeAttribute)))
                {
                    return false;
                }
            }

            return true;
        }

        public IReadOnlyList<IPortModel> InputPorts => m_InputPorts;
        public override IReadOnlyList<IPortModel> InputsByDisplayOrder => InputPorts;

        public IReadOnlyList<IPortModel> OutputPorts
        {
            get
            {
                return DelegatesOutputsToNode(out var last)
                    ? last.OutputsByDisplayOrder
                        .Where(p => p.PortType == PortType.Execution || p.PortType == PortType.Loop).ToList()
                    : m_OutputPorts;
            }
        }

        public override IReadOnlyList<IPortModel> OutputsByDisplayOrder => OutputPorts;

        public bool DelegatesOutputsToNode(out INodeModel last)
        {
            last = m_NodeModels.LastOrDefault()?.Model;

            return last!= null && last.IsBranchType && last.OutputsById.Count > 0;
        }

        public void CleanUp()
        {
            m_NodeModels.RemoveAll(n => n == null);
        }

        public TNodeType CreateStackedNode<TNodeType>(string nodeName = "", int index = -1, SpawnFlags spawnFlags = SpawnFlags.Default, Action<TNodeType> setup = null) where TNodeType : NodeModel
        {
            var node = (TNodeType)CreateStackedNode(typeof(TNodeType), nodeName, index, spawnFlags, n => setup?.Invoke((TNodeType)n));
            return node;
        }

        public INodeModel CreateStackedNode(Type nodeTypeToCreate, string nodeName = "", int index = -1, SpawnFlags spawnFlags = SpawnFlags.Default, Action<NodeModel> preDefineSetup = null)
        {
            if (nodeTypeToCreate == null)
                throw new InvalidOperationException("Cannot create node with a null type");
            NodeModel nodeModel;
            if (spawnFlags.IsSerializable())
                nodeModel = (NodeModel)SpawnNodeAsset(nodeTypeToCreate).Model;
            else
                nodeModel = (NodeModel)Activator.CreateInstance(nodeTypeToCreate);

            nodeModel.Title = nodeName ?? nodeTypeToCreate.Name;
            nodeModel.Position = Vector2.zero;
            nodeModel.GraphModel = GraphModel;
            nodeModel.ParentStackModel = this;
            preDefineSetup?.Invoke(nodeModel);
            nodeModel.DefineNode();
            if(!spawnFlags.IsOrphan())
            {
                if (spawnFlags.IsUndoable())
                {
                    Undo.RegisterCreatedObjectUndo(nodeModel.NodeAssetReference, "Create Node");
                    AddStackedNode(nodeModel, index);
                }
                else
                    AddStackedNodeNoUndo(nodeModel, index);
            }

            return nodeModel;

            AbstractNodeAsset SpawnNodeAsset(Type typeToSpawn)
            {
                var genericNodeAssetType = typeof(NodeAsset<>).MakeGenericType(typeToSpawn);
                var derivedTypes = TypeCache.GetTypesDerivedFrom(genericNodeAssetType);

                if (derivedTypes.Count == 0)
                    throw new InvalidOperationException($"No NodeAssets of type NodeAsset<{typeToSpawn.Name}>");
                Assert.AreEqual(derivedTypes.Count, 1,
                    $"Multiple NodeAssets of type NodeAsset<{typeToSpawn.Name}> have been found");

                return ScriptableObject.CreateInstance(derivedTypes[0]) as AbstractNodeAsset;
            }
        }

        public void MoveStackedNodes(IReadOnlyCollection<INodeModel> nodesToMove, int actionNewIndex, bool deleteWhenEmpty = true)
        {
            if (nodesToMove == null)
                return;

            int i = 0;
            foreach (var nodeModel in nodesToMove)
            {
                var parentStack = (StackBaseModel)nodeModel.ParentStackModel;
                if (parentStack != null)
                {
                    parentStack.RemoveStackedNode(nodeModel);
                    if (deleteWhenEmpty && parentStack.Capabilities.HasFlag(CapabilityFlags.DeletableWhenEmpty) &&
                        parentStack != this &&
                        !parentStack.GetConnectedNodes().Any() &&
                        !parentStack.NodeModels.Any())
                        ((VSGraphModel)GraphModel).DeleteNode(parentStack, GraphViewModel.GraphModel.DeleteConnections.True);
                }
            }

            // We need to do it in two passes to allow for same stack move of multiple nodes.
            foreach (var nodeModel in nodesToMove)
                AddStackedNode(nodeModel, actionNewIndex == -1 ? -1 : actionNewIndex + i++);
        }

        public void AddStackedNode(INodeModel nodeModelInterface, int index)
        {
            if (!AcceptNode(nodeModelInterface.GetType()))
                return;

            var nodeModel = (NodeModel)nodeModelInterface;

            Utility.SaveAssetIntoObject(nodeModel.NodeAssetReference, (Object)AssetModel);
            Undo.RegisterCompleteObjectUndo(NodeAssetReference, "Add Node");

            nodeModel.GraphModel = GraphModel;
            nodeModel.ParentStackModel = this;
            if (index == -1)
                m_NodeModels.Add(nodeModel.NodeAssetReference);
            else
                m_NodeModels.Insert(index, nodeModel.NodeAssetReference);

            VSGraphModel vsGraphModel = (VSGraphModel)GraphModel;
            vsGraphModel.LastChanges.ChangedElements.Add(nodeModel);

            EditorUtility.SetDirty(NodeAssetReference);
        }

        public void AddStackedNodeNoUndo(INodeModel nodeModelInterface, int index)
        {
            if (!AcceptNode(nodeModelInterface.GetType()))
                return;

            var nodeModel = (NodeModel)nodeModelInterface;

            Utility.SaveAssetIntoObject(nodeModel.NodeAssetReference, (Object)AssetModel);

            nodeModel.GraphModel = GraphModel;
            nodeModel.ParentStackModel = this;
            if (index == -1)
                m_NodeModels.Add(nodeModel.NodeAssetReference);
            else
                m_NodeModels.Insert(index, nodeModel.NodeAssetReference);

            VSGraphModel vsGraphModel = (VSGraphModel)GraphModel;
            vsGraphModel.LastChanges.ChangedElements.Add(nodeModel);
        }

        public void RemoveStackedNode(INodeModel nodeModel)
        {
            Undo.RegisterCompleteObjectUndo(NodeAssetReference, "RemoveNode");
            Undo.RegisterCompleteObjectUndo(nodeModel.NodeAssetReference, "Unparent Node");
            ((NodeModel)nodeModel).ParentStackModel = null;
            m_NodeModels.Remove(nodeModel.NodeAssetReference);

            VSGraphModel vsGraphModel = (VSGraphModel)GraphModel;
            vsGraphModel.LastChanges.DeletedElements++;

            EditorUtility.SetDirty(NodeAssetReference);
        }

        protected override void OnPreDefineNode()
        {
            m_InputPorts = new List<IPortModel>();
            m_OutputPorts = new List<IPortModel>();
            base.OnPreDefineNode();
        }

        protected override void OnDefineNode()
        {
            AddInputExecutionPort(null);
            AddExecutionOutputPort(null);
        }

        public void ClearNodes()
        {
            m_NodeModels.Clear();
        }

        protected override PortModel AddInputPort(string portName, PortType portType, TypeHandle dataType, string portId = null)
        {
            var inputPort = base.AddInputPort(portName, portType, dataType, portId);
            m_InputPorts.Add(inputPort);
            return inputPort;
        }

        protected override PortModel AddOutputPort(string portName, PortType portType, TypeHandle dataType, string portId = null)
        {
            var outputPort = base.AddOutputPort(portName, portType, dataType, portId);
            m_OutputPorts.Add(outputPort);
            return outputPort;
        }
    }
}
