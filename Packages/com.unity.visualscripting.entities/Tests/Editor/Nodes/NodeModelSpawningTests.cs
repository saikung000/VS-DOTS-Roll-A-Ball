using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingTests.Models
{
    public class NodeModelSpawningTests : BaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void Test_NodeSpawningWillNotCauseErrors()
        {
            //Prepare
            var reflectedNodeTypes = GetAllNonAbstractNodeModelTypes();

            //Act
            SpawnAllNodeModelsInGraph(GraphModel);

            //Validate
            var spawnedNodeTypes = GetAllSpawnedNodeTypes();
            var missingSpawnedNode = reflectedNodeTypes.Except(spawnedNodeTypes).ToList();
            if (missingSpawnedNode.Any())
            {
                string errorMessage = "The following types have not been spawned in the SpawnAllNodeModelsInGraph():\n\n";
                StringBuilder builder = new StringBuilder(errorMessage);
                foreach (var missingNode in missingSpawnedNode)
                    builder.AppendLine(missingNode.ToString());
                Debug.LogError(builder.ToString());
            }

            Assert.That(missingSpawnedNode, Is.Empty);
        }

        void SpawnAllNodeModelsInGraph(VSGraphModel graphModel)
        {
            StackModel stack;
            FunctionModel funcModel;
            //--Floating Nodes--

            //Stack-Derived NodeModels
            stack = GraphModel.CreateNode<StackModel>("StackModel");
            funcModel = GraphModel.CreateNode<FunctionModel>("FunctionModel");
            var methodInfo = TypeSystem.GetMethod(typeof(Debug), nameof(Debug.Log), true);

            GraphModel.CreateEventFunction(methodInfo,Vector2.zero);
            GraphModel.CreateNode<OnEndEntitiesNodeModel>("OnEndEntitiesNodeModel");
            GraphModel.CreateNode<OnEventNodeModel>("OnEventNodeModel");
            GraphModel.CreateNode<OnStartEntitiesNodeModel>("OnStartEntitiesNodeModel");
            var onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("OnUpdateEntitiesNodeModel");
            GraphModel.CreateNode<PostUpdate>("PostUpdate");
            GraphModel.CreateNode<PreUpdate>("PreUpdate");
            GraphModel.CreateNode<KeyDownEventModel>("KeyDownEventModel");
            GraphModel.CreateLoopStack(typeof(ForEachHeaderModel), Vector2.zero);
            GraphModel.CreateLoopStack(typeof(WhileHeaderModel), Vector2.zero);
            GraphModel.CreateLoopStack(typeof(ForAllEntitiesStackModel), Vector2.zero);

            //Constant-typed NodeModels
            GraphModel.CreateNode<BooleanConstantNodeModel>("BooleanConstantNodeModel");
            GraphModel.CreateNode<ColorConstantModel>("ColorConstantModel");
            GraphModel.CreateNode<CurveConstantNodeModel>("CurveConstantNodeModel");
            GraphModel.CreateNode<DoubleConstantModel>("DoubleConstantModel");
            GraphModel.CreateNode<EnumConstantNodeModel>("EnumConstantNodeModel");
            GraphModel.CreateNode<FloatConstantModel>("FloatConstantModel");
            GraphModel.CreateNode<GetPropertyGroupNodeModel>("GetPropertyGroupNodeModel");
            GraphModel.CreateNode<InputConstantModel>("InputConstantModel");
            GraphModel.CreateNode<IntConstantModel>("IntConstantModel");
            GraphModel.CreateNode<LayerConstantModel>("LayerConstantModel");
            GraphModel.CreateNode<LayerMaskConstantModel>("LayerMaskConstantModel");
            GraphModel.CreateNode<ObjectConstantModel>("ObjectConstantModel");
            GraphModel.CreateNode<QuaternionConstantModel>("QuaternionConstantModel");
            GraphModel.CreateNode<StringConstantModel>("StringConstantModel");
            GraphModel.CreateNode<TagConstantModel>("TagConstantModel");
            GraphModel.CreateNode<TypeConstantModel>("TypeConstantModel");
            GraphModel.CreateNode<Vector2ConstantModel>("Vector2ConstantModel");
            GraphModel.CreateNode<Vector3ConstantModel>("Vector3ConstantModel");
            GraphModel.CreateNode<Vector4ConstantModel>("Vector4ConstantModel");
            GraphModel.CreateNode<ConstantSceneAssetNodeModel>("ConstantSceneAssetNodeModel");
            GraphModel.CreateNode<Float2ConstantModel>("Float2ConstantModel");
            GraphModel.CreateNode<Float3ConstantModel>("Float3ConstantModel");
            GraphModel.CreateNode<Float4ConstantModel>("Float4ConstantModel");

            //Misc
            void DefineSystemConstant(SystemConstantNodeModel m)
            {
                m.ReturnType = typeof(float).GenerateTypeHandle(Stencil);
                m.DeclaringType = typeof(Mathf).GenerateTypeHandle(Stencil);
                m.Identifier = "PI";
            }
            GraphModel.CreateNode<SystemConstantNodeModel>("SystemConstantNodeModel", Vector2.zero, SpawnFlags.Default, DefineSystemConstant);
            GraphModel.CreateNode<GroupNodeModel>("GroupNodeModel");
            GraphModel.CreateNode<GetInputNodeModel>("GetInputNodeModel");
            GraphModel.CreateNode<GetOrCreateComponentNodeModel>("GetOrCreateComponentNodeModel");
            GraphModel.CreateNode<GetSingletonNodeModel>("GetSingletonNodeModel");
            GraphModel.CreateNode<ThisNodeModel>("ThisNodeModel");
            VariableDeclarationModel decl = graphModel.CreateGraphVariableDeclaration("MyVariableName", typeof(int).GenerateTypeHandle(graphModel.Stencil), true);
            GraphModel.CreateVariableNode(decl, Vector2.zero);
            GraphModel.CreateNode<MacroRefNodeModel>("MacroRefNodeModel");
            GraphModel.CreateInlineExpressionNode("2+2", Vector2.zero);
            GraphModel.CreateBinaryOperatorNode(BinaryOperatorKind.Add, Vector2.zero);
            GraphModel.CreateUnaryOperatorNode(UnaryOperatorKind.PostIncrement, Vector2.zero);

            //--Stack-Contained Nodes--
            stack.CreateStackedNode<AddComponentNodeModel>();
            stack.CreateStackedNode<DestroyEntityNodeModel>();
            stack.CreateStackedNode<ForAllEntitiesNodeModel>();
            stack.CreateStackedNode<ForEachNodeModel>();
            stack.CreateFunctionCallNode(TypeSystem.GetMethod(typeof(Debug), nameof(Debug.Log), true));
            stack.CreateFunctionRefCallNode(funcModel);
            stack.CreateStackedNode<InstantiateNodeModel>();
            stack.CreateStackedNode<IfConditionNodeModel>();
            stack.CreateStackedNode<LogNodeModel>();
            stack.CreateStackedNode<RemoveComponentNodeModel>();
            stack.CreateStackedNode<SetComponentNodeModel>();
            stack.CreateStackedNode<SetPositionNodeModel>();
            stack.CreateStackedNode<SetRotationNodeModel>();
            stack.CreateStackedNode<WhileNodeModel>();
            stack.CreateStackedNode<SetPropertyGroupNodeModel>();
            stack.CreateStackedNode<SetVariableNodeModel>();
            funcModel.CreateStackedNode<ReturnNodeModel>();

            TypeHandle eventTypeHandle = typeof(UnitTestEvent).GenerateTypeHandle(Stencil);
            onUpdateModel.CreateStackedNode<SendEventNodeModel>("SendEventNodeModel", 0, SpawnFlags.Default, n => n.EventType = eventTypeHandle);
        }

        HashSet<Type> GetAllSpawnedNodeTypes()
        {
            var spawnedNodeTypes = new HashSet<Type>(GetAllNodes().Select(n => n.GetType()));
            foreach(var nodeType in GetAllNodes().OfType<StackBaseModel>().SelectMany(stack => stack.NodeModels).Select(n => n.GetType()))
                spawnedNodeTypes.Add(nodeType);
            return spawnedNodeTypes;
        }

        static HashSet<Type> GetAllNonAbstractNodeModelTypes()
        {
            HashSet<Type> nodeModelTypes = new HashSet<Type>();
            ConcreteTypesDerivingFrom(typeof(NodeModel), nodeModelTypes);
            return nodeModelTypes;

            void ConcreteTypesDerivingFrom(Type expectedBaseType, HashSet<Type> foundTypes)
            {
                if (!expectedBaseType.IsAbstract && !expectedBaseType.Assembly.FullName.Contains(".Tests"))
                    foundTypes.Add(expectedBaseType);

                var subTypes = TypeCache.GetTypesDerivedFrom(expectedBaseType);
                foreach (var subType in subTypes)
                {
                    if(subType.BaseType.IsGenericType && subType.BaseType.GetGenericTypeDefinition() == expectedBaseType
                        || subType.BaseType == expectedBaseType)
                        ConcreteTypesDerivingFrom(subType, foundTypes);
                }
            }
        }
    }
}
