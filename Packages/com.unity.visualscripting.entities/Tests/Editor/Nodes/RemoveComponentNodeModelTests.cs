using System;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Nodes
{
    class RemoveComponentNodeModelTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        static void CreateRemoveComponentInGraph(VSGraphModel graphModel, Type componentToRemove)
        {
            var component = componentToRemove.GenerateTypeHandle(graphModel.Stencil);
            var group = graphModel.CreateComponentQuery("g");
            group.AddComponent(graphModel.Stencil, component, ComponentDefinitionFlags.None);

            var groupInstance = graphModel.CreateVariableNode(group, Vector2.zero);
            var onUpdateEntities = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
            graphModel.CreateEdge(onUpdateEntities.InstancePort, groupInstance.OutputPort);

            var entityInstance = graphModel.CreateVariableNode(
                onUpdateEntities.FunctionParameterModels.Single(
                    p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil)
                ),
                Vector2.zero);

            var removeComponent = onUpdateEntities.CreateStackedNode<RemoveComponentNodeModel>("remove");
            removeComponent.ComponentType = component;

            graphModel.CreateEdge(removeComponent.EntityPort, entityInstance.OutputPort);
        }

        [Test]
        public void TestRemoveComponent_RootContext([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
                {
                    CreateRemoveComponentInGraph(graphModel, typeof(Translation));
                },
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Translation)),
                (manager, entityIndex, e) => Assert.That(!manager.HasComponent<Translation>(e)));
        }

        [Test]
        public void TestRemoveComponent_ForEachContext([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
                {
                    // group1 - Position
                    var group1 = graphModel.CreateComponentQuery("g1");
                    group1.AddComponent(graphModel.Stencil, typeof(Translation).GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
                    var group1Instance = graphModel.CreateVariableNode(group1, Vector2.zero);

                    // group2 - Scale (will add RenderMesh)
                    var group2 = graphModel.CreateComponentQuery("g2");
                    group2.AddComponent(graphModel.Stencil, typeof(Scale).GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
                    var group2Instance = graphModel.CreateVariableNode(group2, Vector2.zero);

                    // update group 1
                    var onUpdateEntities = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                    graphModel.CreateEdge(onUpdateEntities.InstancePort, group1Instance.OutputPort);

                    // nested update group 2
                    var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>( Vector2.zero);
                    var forAllNode = forAllStack.CreateLoopNode(onUpdateEntities, 0) as ForAllEntitiesNodeModel;
                    graphModel.CreateEdge(forAllNode.InputPort, group2Instance.OutputPort);
                    graphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                    // Remove Scale component
                    var addComponent = forAllStack.CreateStackedNode<RemoveComponentNodeModel>("remove");
                    addComponent.ComponentType = typeof(Scale).GenerateTypeHandle(Stencil);

                    var entityInstance = graphModel.CreateVariableNode(
                        forAllStack.FunctionParameterModels.Single(
                            p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil)
                        ),
                        Vector2.zero);
                    graphModel.CreateEdge(addComponent.EntityPort, entityInstance.OutputPort);
                },
                (manager, entityIndex, e) =>
                {
                    // HACK as there is no Single update method as entry point (just the on UpdateEntities right now)
                    var toAdd = entityIndex == 0 ? typeof(Translation) : typeof(Scale);
                    manager.AddComponent(e, toAdd);
                },
                (manager, entityIndex, e) => Assert.That(!manager.HasComponent<Scale>(e)));
        }
    }
}
