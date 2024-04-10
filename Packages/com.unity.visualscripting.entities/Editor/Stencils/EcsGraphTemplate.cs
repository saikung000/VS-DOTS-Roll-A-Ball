using System;
using Packages.VisualScripting.Editor.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    class EcsGraphTemplate : ICreatableGraphTemplate
    {
        public Type StencilType => typeof(EcsStencil);
        public string GraphTypeName => "ECS Graph";
        public string DefaultAssetName => "ECSGraph";

        public void InitBasicGraph(VSGraphModel graphModel)
        {
            var group = graphModel.CreateComponentQuery("myGroup");
            var node = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("On Update Entities", Vector2.zero);
            var groupInstance = graphModel.CreateVariableNode(group, new Vector2(-145, 8));

            graphModel.CreateEdge(node.InstancePort, groupInstance.OutputPort);
        }
    }
}
