using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests
{
    public class BasicCodeGenBases : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test(Description = "VSB-214 regression test")]
        public void TestFunctionCall([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
            {
                var query = graph.CreateComponentQuery("m_Query");
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

                var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
                graph.CreateEdge(onUpdate.InstancePort, queryInstance.OutputPort);

                var propertyInfo = typeof(Time).GetProperty("timeScale", BindingFlags.Static | BindingFlags.Public);
                var setProperty = onUpdate.CreateFunctionCallNode(propertyInfo?.SetMethod);

                var floatConst = graph.CreateConstantNode("floatConst", TypeHandle.Float, Vector2.zero);
                ((FloatConstantModel)floatConst).value = 0.42f;

                graph.CreateEdge(setProperty.GetPortForParameter("value"), floatConst.OutputPort);
            },
            (manager, entityIndex, entity) => manager.AddComponentData(entity, new Translation()),
            (manager, entityIndex, entity) => Assert.That(Time.timeScale, Is.EqualTo(0.42f)));
        }

        [Test(Description = "VSB-178 regression test")]
        public void SetNonGraphVariableDoesntTriggerASingletonUpdate([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
                {
                    var query = graph.CreateComponentQuery("m_Query");
                    var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

                    var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
                    graph.CreateEdge(onUpdate.InstancePort, queryInstance.OutputPort);

                    var floatVariable = onUpdate.CreateFunctionVariableDeclaration("MyFloat", TypeHandle.Float);
                    floatVariable.CreateInitializationValue();
                    var floatInstance = graph.CreateVariableNode(floatVariable, Vector2.zero);

                    var set = onUpdate.CreateStackedNode<SetVariableNodeModel>("set");
                    graph.CreateEdge(set.InstancePort, floatInstance.OutputPort);

                    var setProperty = onUpdate.CreateStackedNode<SetPropertyGroupNodeModel>("Set Property");
                    var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                    setProperty.AddMember(member);

                    var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                    graph.CreateEdge(setProperty.InstancePort, translation.OutputPort);
                    graph.CreateEdge(setProperty.InputsById[member.GetId()], floatInstance.OutputPort);
                },
                (manager, entityIndex, entity) => manager.AddComponentData(entity, new Translation()),
                (manager, entityIndex, entity) =>
                {
                    // We need to check as we created a singleton entity with no Translation but only the GraphData component
                    if (manager.HasComponent<Translation>(entity))
                        Assert.That(manager.GetComponentData<Translation>(entity).Value.x, Is.EqualTo(0f));
                });
        }

        [Test]
        public void GetGraphVariable([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
                {
                    var query = graph.CreateComponentQuery("m_Query");
                    var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

                    var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
                    graph.CreateEdge(onUpdate.InstancePort, queryInstance.OutputPort);

                    var floatVariable = graph.CreateGraphVariableDeclaration("MyFloat", TypeHandle.Float, true);
                    floatVariable.CreateInitializationValue();
                    ((ConstantNodeModel)floatVariable.InitializationModel).ObjectValue = 10f;
                    var floatInstance = graph.CreateVariableNode(floatVariable, Vector2.zero);

                    var setProperty = onUpdate.CreateStackedNode<SetPropertyGroupNodeModel>("Set Property", 0);
                    var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                    setProperty.AddMember(member);

                    var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                    graph.CreateEdge(setProperty.InstancePort, translation.OutputPort);
                    graph.CreateEdge(setProperty.InputsById[member.GetId()], floatInstance.OutputPort);
                },
                (manager, entityIndex, entity) => manager.AddComponentData(entity, new Translation()),
                (manager, entityIndex, entity) =>
                {
                    // We need to check as we created a singleton entity with no Translation but only the GraphData component
                    if (manager.HasComponent<Translation>(entity))
                        Assert.That(manager.GetComponentData<Translation>(entity).Value.x, Is.EqualTo(10f));
                });
        }

        [Test] // TODO: fix jobs
        public void SetGraphVariable([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
                {
                    var query = graph.CreateComponentQuery("m_Query");
                    var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

                    var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
                    graph.CreateEdge(onUpdate.InstancePort, queryInstance.OutputPort);

                    var floatVariable = graph.CreateGraphVariableDeclaration("MyFloat", TypeHandle.Float, true);
                    var floatInstance = graph.CreateVariableNode(floatVariable, Vector2.zero);
                    var floatConst = graph.CreateConstantNode("floatConst", TypeHandle.Float, Vector2.zero);
                    ((FloatConstantModel)floatConst).value = 10f;

                    var setVariable = onUpdate.CreateStackedNode<SetVariableNodeModel>("Set Variable", 0);
                    graph.CreateEdge(setVariable.InstancePort, floatInstance.OutputPort);
                    graph.CreateEdge(setVariable.ValuePort, floatConst.OutputPort);
                },
                (manager, entityIndex, entity) => manager.AddComponentData(entity, new Translation()),
                (manager, entityIndex, entity) =>
                {
                    var graphData = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("GraphData"));
                    if (manager.HasComponent(entity, graphData))
                    {
                        var getComponentMethod = typeof(EntityManager)
                            .GetMethod(nameof(EntityManager.GetComponentData))?
                            .MakeGenericMethod(graphData);
                        var singleton = getComponentMethod?.Invoke(manager, new object[] {entity});
                        var myFloat = graphData.GetField("MyFloat").GetValue(singleton);
                        Assert.That(myFloat, Is.EqualTo(10f));
                    }
                });
        }

        [Test]
        public void OneGroupIterationSystem([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
                {
                    ComponentQueryDeclarationModel group = GraphModel.CreateComponentQuery("g1");
                    TypeHandle positionType = typeof(Translation).GenerateTypeHandle(Stencil);
                    group.AddComponent(Stencil, positionType, ComponentDefinitionFlags.None);
                    IVariableModel groupInstance = GraphModel.CreateVariableNode(group, Vector2.zero);

                    OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);

                    GraphModel.CreateEdge(onUpdateModel.InstancePort, groupInstance.OutputPort);

                    SetPropertyGroupNodeModel set = onUpdateModel.CreateStackedNode<SetPropertyGroupNodeModel>("set");
                    var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                    set.AddMember(member);

                    IVariableModel posComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == positionType), Vector2.zero);
                    GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);

                    ((FloatConstantModel)set.InputConstantsById[member.GetId()]).value = 2f;
                },
                (manager, entityIndex, e) => manager.AddComponentData(e, new Translation { Value = { x = entityIndex } }),
                (manager, entityIndex, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f)));
        }

        [Test(Description = "This test should just pass as SharedComponent must be declared as value and be inserted first in ForEachLambda context")]
        public void OneGroupIterationSystem_SharedAndRegularComponents([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
                {
                    var query = GraphModel.CreateComponentQuery("query");

                    var positionType = typeof(Translation).GenerateTypeHandle(Stencil);
                    query.AddComponent(Stencil, positionType, ComponentDefinitionFlags.None);

                    var renderType = typeof(RenderMesh).GenerateTypeHandle(Stencil);
                    query.AddComponent(Stencil, renderType, ComponentDefinitionFlags.Shared);

                    var groupInstance = GraphModel.CreateVariableNode(query, Vector2.zero);
                    var onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                    GraphModel.CreateEdge(onUpdateModel.InstancePort, groupInstance.OutputPort);

                    var posComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == positionType), Vector2.zero);
                    var renderComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == renderType), Vector2.zero);

                    var logTranslation = onUpdateModel.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[]{typeof(object)}), 0);
                    var logRenderMesh = onUpdateModel.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[]{typeof(object)}), 1);

                    GraphModel.CreateEdge(logTranslation.GetParameterPorts().First(), posComponent.OutputPort);
                    GraphModel.CreateEdge(logRenderMesh.GetParameterPorts().First(), renderComponent.OutputPort);
                },
                (manager, entityIndex, e) =>
                {
                    manager.AddComponentData(e, new Translation { Value = { x = entityIndex } });
                    manager.AddSharedComponentData(e, new RenderMesh());
                },
                (manager, entityIndex, e) => Assert.Pass());
        }

        [Test]
        public void OneGroupIterationSystem_LocalVariable([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
                {
                    ComponentQueryDeclarationModel group = GraphModel.CreateComponentQuery("g1");
                    TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    group.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    IVariableModel groupInstance = GraphModel.CreateVariableNode(group, Vector2.zero);

                    OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                    GraphModel.CreateEdge(onUpdateModel.InstancePort, groupInstance.OutputPort);

                    var localDeclaration = onUpdateModel.CreateFunctionVariableDeclaration("local", TypeHandle.Float);
                    var local = GraphModel.CreateVariableNode(localDeclaration, Vector2.zero);

                    var log = onUpdateModel.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[]{typeof(object)}), 0);

                    GraphModel.CreateEdge(log.GetParameterPorts().First(), local.OutputPort);
                },
                (manager, entityIndex, e) => {},
                (manager, entityIndex, e) => Assert.Pass());
        }


        [Test]
        public void OneGroupIterationSystem_ReadOnly([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
                {
                    ComponentQueryDeclarationModel group = GraphModel.CreateComponentQuery("g1");
                    TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    TypeHandle rotationType = typeof(Rotation).GenerateTypeHandle(Stencil);
                    group.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    group.AddComponent(Stencil, rotationType, ComponentDefinitionFlags.None);
                    IVariableModel groupInstance = GraphModel.CreateVariableNode(group, Vector2.zero);

                    OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);

                    GraphModel.CreateEdge(onUpdateModel.InstancePort, groupInstance.OutputPort);

                    var log = onUpdateModel.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[]{typeof(object)}), 0);

                    IVariableModel posComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                    GraphModel.CreateEdge(log.GetParameterPorts().First(), posComponent.OutputPort);
                },
                (manager, entityIndex, e) => manager.AddComponents(e,
                    new ComponentTypes(ComponentType.ReadWrite<Translation>(), ComponentType.ReadWrite<Rotation>())),
                (manager, entityIndex, e) => Assert.Pass());
        }

        [Test]
        public void NestedIterationSystem_DifferentGroups_SameComponent([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
                {
                    TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);

                    // group1 - Position
                    ComponentQueryDeclarationModel group1 = GraphModel.CreateComponentQuery("g1");
                    group1.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    IVariableModel group1Instance = GraphModel.CreateVariableNode(group1, Vector2.zero);

                    // group2 - Translation too
                    ComponentQueryDeclarationModel group2 = GraphModel.CreateComponentQuery("g2");
                    group2.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    IVariableModel group2Instance = GraphModel.CreateVariableNode(group2, Vector2.zero);

                    // update group 1
                    OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                    GraphModel.CreateEdge(onUpdateModel.InstancePort, group1Instance.OutputPort);

                    // nested update group 2
                    var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                    var forAllNode = forAllStack.CreateLoopNode(onUpdateModel, 0) as ForAllEntitiesNodeModel;
                    Assert.That(forAllNode, Is.Not.Null);
                    GraphModel.CreateEdge(forAllNode.InputPort, group2Instance.OutputPort);
                    GraphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                    // set  group2.translation = ...
                    SetPropertyGroupNodeModel set = forAllStack.CreateStackedNode<SetPropertyGroupNodeModel>("set");
                    var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                    set.AddMember(member);

                    IVariableModel posComponent = GraphModel.CreateVariableNode(forAllStack.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                    GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);

                    ((FloatConstantModel)set.InputConstantsById[member.GetId()]).value = 2f;
                },
                (manager, entityIndex, e) => manager.AddComponentData(e, new Translation { Value = { x = entityIndex } }),
                (manager, entityIndex, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f)));
        }

        [Test]
        public void NestedIterationSystem_DifferentGroups_NestedLocalVariable([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, g =>
                {
                    TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    TypeHandle rotationType = typeof(Rotation).GenerateTypeHandle(Stencil);

                    // group1 - Position
                    ComponentQueryDeclarationModel group1 = GraphModel.CreateComponentQuery("g1");
                    group1.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    IVariableModel group1Instance = GraphModel.CreateVariableNode(group1, Vector2.zero);

                    // group2 - Rotation too
                    ComponentQueryDeclarationModel group2 = GraphModel.CreateComponentQuery("g2");
                    group2.AddComponent(Stencil, rotationType, ComponentDefinitionFlags.None);
                    IVariableModel group2Instance = GraphModel.CreateVariableNode(group2, Vector2.zero);

                    // update group 1
                    OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                    GraphModel.CreateEdge(onUpdateModel.InstancePort, group1Instance.OutputPort);

                    // nested update group 2
                    var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>( Vector2.zero);
                    var forAllNode = forAllStack.CreateLoopNode(onUpdateModel, 0) as ForAllEntitiesNodeModel;
                    Assert.That(forAllNode, Is.Not.Null);
                    GraphModel.CreateEdge(forAllNode.InputPort, group2Instance.OutputPort);
                    GraphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                    var decl = forAllStack.CreateFunctionVariableDeclaration("x", TypeHandle.Int);
                    // set  group1.translation = ...
                    SetVariableNodeModel set = forAllStack.CreateStackedNode<SetVariableNodeModel>("set");

                    IVariableModel posComponent = GraphModel.CreateVariableNode(decl, Vector2.zero);
                    GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);
                });
        }

        [Test]
        public void NestedIterationSystem_DifferentGroups_DifferentComponents([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
                {
                    TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    TypeHandle rotationType = typeof(Rotation).GenerateTypeHandle(Stencil);

                    // group1 - Position
                    ComponentQueryDeclarationModel group1 = GraphModel.CreateComponentQuery("g1");
                    group1.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    IVariableModel group1Instance = GraphModel.CreateVariableNode(group1, Vector2.zero);

                    // group2 - Rotation too
                    ComponentQueryDeclarationModel group2 = GraphModel.CreateComponentQuery("g2");
                    group2.AddComponent(Stencil, rotationType, ComponentDefinitionFlags.None);
                    IVariableModel group2Instance = GraphModel.CreateVariableNode(group2, Vector2.zero);

                    // update group 1
                    OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                    GraphModel.CreateEdge(onUpdateModel.InstancePort, group1Instance.OutputPort);

                    // nested update group 2
                    var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>( Vector2.zero);
                    var forAllNode = forAllStack.CreateLoopNode(onUpdateModel, 0) as ForAllEntitiesNodeModel;
                    Assert.That(forAllNode, Is.Not.Null);
                    GraphModel.CreateEdge(forAllNode.InputPort, group2Instance.OutputPort);
                    GraphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                    // set  group1.translation = ...
                    SetPropertyGroupNodeModel set = forAllStack.CreateStackedNode<SetPropertyGroupNodeModel>("set");
                    var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                    set.AddMember(member);

                    IVariableModel posComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                    GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);

                    ((FloatConstantModel)set.InputConstantsById[member.GetId()]).value = 2f;
                },
                (manager, entityIndex, e) =>
                {
                    manager.AddComponentData(e, new Translation { Value = { x = entityIndex } });
                    manager.AddComponentData(e, new Rotation() );
                },
                (manager, entityIndex, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f)));
        }

        [Test]
        public void NestedIteration_DifferentGroups_DifferentEntitiesAccess([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
                {
                    var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    var scaleType = typeof(Scale).GenerateTypeHandle(Stencil);

                    // group1 - Position
                    var group1 = GraphModel.CreateComponentQuery("g1");
                    group1.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    var group1Instance = GraphModel.CreateVariableNode(group1, Vector2.zero);

                    // group2 - Scale
                    var group2 = GraphModel.CreateComponentQuery("g2");
                    group2.AddComponent(Stencil, scaleType, ComponentDefinitionFlags.None);
                    var group2Instance = GraphModel.CreateVariableNode(group2, Vector2.zero);

                    // update group 1
                    var update = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                    GraphModel.CreateEdge(update.InstancePort, group1Instance.OutputPort);

                    // nested update group 2
                    var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                    var forAllNode = forAllStack.CreateLoopNode(update, 0) as ForAllEntitiesNodeModel;
                    Assert.That(forAllNode, Is.Not.Null);
                    GraphModel.CreateEdge(forAllNode.InputPort, group2Instance.OutputPort);
                    GraphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                    // entity from group 1
                    var entity1 = graph.CreateVariableNode(
                        update.FunctionParameterModels.Single(
                            p => p.DataType == typeof(Entity).GenerateTypeHandle(graph.Stencil)),
                        Vector2.zero);

                    // entity from group 2
                    var entity2 = graph.CreateVariableNode(
                        forAllStack.FunctionParameterModels.Single(
                            p => p.DataType == typeof(Entity).GenerateTypeHandle(graph.Stencil)),
                        Vector2.zero);

                    // set a new Translation to entities of group1
                    var setTranslation = forAllStack.CreateStackedNode<SetComponentNodeModel>("set translation");
                    setTranslation.ComponentType = typeof(Translation).GenerateTypeHandle(graph.Stencil);
                    setTranslation.DefineNode();
                    ((FloatConstantModel)setTranslation.InputConstantsById["z"]).value = 10f;
                    graph.CreateEdge(setTranslation.EntityPort, entity1.OutputPort);

                    // set a new Scale to entities of group2
                    var setScale = forAllStack.CreateStackedNode<SetComponentNodeModel>("set scale");
                    setScale.ComponentType = typeof(Scale).GenerateTypeHandle(graph.Stencil);
                    setScale.DefineNode();
                    ((FloatConstantModel) setScale.InputConstantsById["Value"]).value = 30f;
                    graph.CreateEdge(setScale.EntityPort, entity2.OutputPort);
                },
                (manager, index, entity) =>
                {
                    if (index % 2 == 0)
                        manager.AddComponentData(entity, new Translation());
                    else
                        manager.AddComponentData(entity, new Scale());
                },
                (manager, index, entity) =>
                {
                    if (manager.HasComponent<Translation>(entity))
                        Assert.That(manager.GetComponentData<Translation>(entity).Value.z, Is.EqualTo(10f));

                    if (manager.HasComponent<Scale>(entity))
                        Assert.That(manager.GetComponentData<Scale>(entity).Value, Is.EqualTo(30f));
                }
            );
        }

        [Test]
        public void MultipleOnUpdateEntities([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
                {
                    var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    var query = GraphModel.CreateComponentQuery("g1");
                    query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    var queryInstance = GraphModel.CreateVariableNode(query, Vector2.zero);

                    CreateUpdateAndLogEntity(graph, queryInstance);
                    CreateUpdateAndLogEntity(graph, queryInstance);
                },
                (manager, index, entity) => manager.AddComponentData(entity, new Translation()),
                (manager, index, entity) => Assert.Pass()
            );
        }

        static void CreateUpdateAndLogEntity(VSGraphModel graphModel, IVariableModel variable)
        {
            // update entities
            var update = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
            graphModel.CreateEdge(update.InstancePort, variable.OutputPort);

            // Create entity from update
            var entity = graphModel.CreateVariableNode(
                update.FunctionParameterModels.Single(
                    p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil)),
                Vector2.zero);

            // Log the entity
            var log = update.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[]{typeof(object)}), 0);
            graphModel.CreateEdge(log.GetParameterPorts().First(), entity.OutputPort);
        }
    }
}
