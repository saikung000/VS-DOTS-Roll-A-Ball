using System;
using System.Reflection;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model
{
    public static class VSCreationExtensions
    {
        static TNodeModel CreateModel<TNodeModel>(this GraphModel graphModel, string name, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default, Action<TNodeModel> preDefineSetup = null) where TNodeModel : NodeModel
        {
            return graphModel.CreateNode(name, position, spawnFlags, preDefineSetup);
        }

        static T CreateModel<T>(this GraphModel graphModel, Type nodeType, string name, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default, Action<NodeModel> preDefineSetup = null) where T : NodeModel
        {
            return (T)graphModel.CreateNode(nodeType, name, position, spawnFlags, preDefineSetup);
        }

        static INodeModel CreateStackedNode(this StackBaseModel stackModel, Type type, string nodeName, int index,
            SpawnFlags spawnFlags = SpawnFlags.Default, Action<NodeModel> setup = null)
        {
            return stackModel.CreateStackedNode(type, nodeName, index, spawnFlags, setup);
        }

        static T CreateStackedNode<T>(this StackBaseModel stackModel, string nodeName, int index,
            SpawnFlags spawnFlags = SpawnFlags.Default, Action<T> setup = null) where T : NodeModel
        {
            return stackModel.CreateStackedNode(nodeName, index, spawnFlags, setup);
        }


        public static GroupNodeModel CreateGroup(this VSGraphModel graphModel, string name, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return graphModel.CreateModel<GroupNodeModel>(name, position, spawnFlags);
        }

        public static StackBaseModel CreateStack(this VSGraphModel graphModel, string name, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            var stackTypeToCreate = graphModel.Stencil.GetDefaultStackModelType();
            if (!typeof(StackModel).IsAssignableFrom(stackTypeToCreate))
                stackTypeToCreate = typeof(StackModel);

            return graphModel.CreateModel<StackBaseModel>(stackTypeToCreate, name, position, spawnFlags);
        }

        public static FunctionModel CreateFunction(this VSGraphModel graphModel, string methodName, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            graphModel.Stencil.GetSearcherDatabaseProvider().ClearReferenceItemsSearcherDatabases();
            var uniqueMethodName = graphModel.GetUniqueName(methodName);

            return graphModel.CreateModel<FunctionModel>(uniqueMethodName, position, spawnFlags);
        }

        public static T CreateLoopStack<T>(this VSGraphModel graphModel, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default) where T : LoopStackModel
        {
            return graphModel.CreateNode<T>("loopStack", position, spawnFlags, node => node.CreateLoopVariables(null));
        }

        public static LoopStackModel CreateLoopStack(this VSGraphModel graphModel, Type loopStackType, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return graphModel.CreateModel<LoopStackModel>(loopStackType, "loopStack", position, spawnFlags, node => ((LoopStackModel)node).CreateLoopVariables(null));
        }

        public static FunctionRefCallNodeModel CreateFunctionRefCallNode(this StackBaseModel stackModel,
            FunctionModel methodInfo, int index = -1, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return stackModel.CreateStackedNode<FunctionRefCallNodeModel>(methodInfo.Title, index, spawnFlags, n => n.Function = methodInfo);
        }

        public static FunctionRefCallNodeModel CreateFunctionRefCallNode(this VSGraphModel graphModel,
            FunctionModel methodInfo, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return graphModel.CreateModel<FunctionRefCallNodeModel>(methodInfo.Title, position, spawnFlags, n => n.Function = methodInfo);
        }

        public static FunctionCallNodeModel CreateFunctionCallNode(this VSGraphModel graphModel, MethodBase methodInfo,
            Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return graphModel.CreateModel<FunctionCallNodeModel>(methodInfo.Name, position, spawnFlags, n => n.MethodInfo = methodInfo);
        }

        public static FunctionCallNodeModel CreateFunctionCallNode(this StackBaseModel stackModel, MethodInfo methodInfo,
            int index = -1, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return CreateStackedNode<FunctionCallNodeModel>(stackModel, methodInfo.Name, index, spawnFlags,
                n => n.MethodInfo = methodInfo);
        }

        public static InlineExpressionNodeModel CreateInlineExpressionNode(this VSGraphModel graphModel,
            string expression, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            const string nodeName = "inline";
            void Setup(InlineExpressionNodeModel m) => m.Expression = expression;
            return graphModel.CreateModel<InlineExpressionNodeModel>(nodeName, position, spawnFlags, Setup);
        }

        public static UnaryOperatorNodeModel CreateUnaryStatementNode(this StackBaseModel stackModel,
            UnaryOperatorKind kind, int index, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return CreateStackedNode<UnaryOperatorNodeModel>(stackModel, kind.ToString(), index, spawnFlags,
                n => n.kind = kind);
        }

        public static UnaryOperatorNodeModel CreateUnaryOperatorNode(this VSGraphModel graphModel,
            UnaryOperatorKind kind, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return graphModel.CreateModel<UnaryOperatorNodeModel>(kind.ToString(), position, spawnFlags, n => n.kind = kind);
        }

        public static BinaryOperatorNodeModel CreateBinaryOperatorNode(this VSGraphModel graphModel,
            BinaryOperatorKind kind, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return graphModel.CreateModel<BinaryOperatorNodeModel>(kind.ToString(), position, spawnFlags, n => n.kind = kind);
        }

        public static IVariableModel CreateVariableNode(this VSGraphModel graphModel,
            IVariableDeclarationModel declarationModel, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            if (declarationModel == null)
                return graphModel.CreateNode<ThisNodeModel>("this", position);

            return graphModel.CreateModel<VariableNodeModel>(declarationModel.Title, position, spawnFlags, v => v.DeclarationModel = declarationModel);
        }

        public static IVariableModel CreateVariableNodeNoUndo(this VSGraphModel graphModel,
            IVariableDeclarationModel declarationModel, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            if (declarationModel == null)
                return graphModel.CreateNode<ThisNodeModel>("this", position, SpawnFlags.CreateNodeAsset);

            return graphModel.CreateNode<VariableNodeModel>(declarationModel.Title, position, spawnFlags, v => v.DeclarationModel = declarationModel);
        }

        public static IConstantNodeModel CreateConstantNode(this VSGraphModel graphModel, string constantName,
            TypeHandle constantTypeHandle, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            var nodeType = graphModel.Stencil.GetConstantNodeModelType(constantTypeHandle);
            return CreateConstantNodeModel(graphModel, constantName, nodeType, constantTypeHandle ,position, spawnFlags);
        }

        static IConstantNodeModel CreateConstantNodeModel(GraphModel graphModel, string constantName, Type nodeType,
            TypeHandle constantTypeHandle, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            void PreDefineSetup(NodeModel model)
            {
                if (model is ConstantNodeModel constantModel)
                    constantModel.PredefineSetup(constantTypeHandle);
            }

            return graphModel.CreateModel<ConstantNodeModel>(nodeType, constantName, position, spawnFlags, PreDefineSetup);
        }

        public static ISystemConstantNodeModel CreateSystemConstantNode(this VSGraphModel graphModel, Type type,
            PropertyInfo propertyInfo, Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            void Setup(SystemConstantNodeModel n)
            {
                n.ReturnType = propertyInfo.PropertyType.GenerateTypeHandle(graphModel.Stencil);
                n.DeclaringType = propertyInfo.DeclaringType.GenerateTypeHandle(graphModel.Stencil);
                n.Identifier = propertyInfo.Name;
            }

            var name = $"{type.FriendlyName(false)} > {propertyInfo.Name}";
            return graphModel.CreateModel<SystemConstantNodeModel>(name, position, spawnFlags, Setup);
        }

        public static EventFunctionModel CreateEventFunction(this VSGraphModel graphModel, MethodInfo methodInfo,
            Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            graphModel.Stencil.GetSearcherDatabaseProvider().ClearReferenceItemsSearcherDatabases();

            void Setup(EventFunctionModel e) => e.EventType = methodInfo.DeclaringType.GenerateTypeHandle(graphModel.Stencil);

            return graphModel.CreateModel<EventFunctionModel>(methodInfo.Name, position, spawnFlags, Setup);
        }

        public static SetPropertyGroupNodeModel CreateSetPropertyGroupNode(this StackBaseModel stackModel, int index)
        {
            var nodeModel = stackModel.CreateStackedNode<SetPropertyGroupNodeModel>("Set Property", index);
            return nodeModel;
        }

        public static GetPropertyGroupNodeModel CreateGetPropertyGroupNode(this VSGraphModel graphModel,
            Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            return graphModel.CreateModel<GetPropertyGroupNodeModel>("Get Property", position, spawnFlags);
        }

        public static GroupNodeModel CreateGroupNode(this VSGraphModel graphModel, string name, Vector2 position)
        {
            return graphModel.CreateNode<GroupNodeModel>(name, position);
        }

       public static MacroRefNodeModel CreateMacroRefNode(this VSGraphModel graphModel, VSGraphModel macroGraphModel,
           Vector2 position, SpawnFlags spawnFlags = SpawnFlags.Default)
       {
           return graphModel.CreateModel<MacroRefNodeModel>(graphModel.AssetModel.Name, position, spawnFlags, n => n.Macro = macroGraphModel);
       }
    }
}
