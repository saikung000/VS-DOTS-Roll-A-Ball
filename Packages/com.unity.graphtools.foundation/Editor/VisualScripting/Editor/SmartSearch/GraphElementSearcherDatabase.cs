﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor.EditorCommon.Extensions;
using UnityEditor.EditorCommon.Utility;
using UnityEditor.Searcher;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.VisualScripting;
using Object = UnityEngine.Object;

namespace UnityEditor.VisualScripting.Editor.SmartSearch
{
    [Flags]
    public enum MemberFlags
    {
        Constructor,
        Extension,
        Field,
        Method,
        Property
    }

    [PublicAPI]
    public class GraphElementSearcherDatabase
    {
        enum IfConditionMode
        {
            Basic,
            Advanced,
            Complete
        }

        const string k_Constant = "Constant";
        const string k_ControlFlow = "Control Flow";
        const string k_LoopStack = "... Loop Stack";
        const string k_Operator = "Operator";
        const string k_InlineExpression = "Inline Expression";
        const string k_InlineLabel = "10+y";
        const string k_Stack = "Stack";
        const string k_Group = "Group";
        const string k_NewFunction = "Create New Function";
        const string k_FunctionName = "My Function";
        const string k_Sticky = "Sticky Note";
        const string k_Then = "then";
        const string k_Else = "else";
        const string k_IfCondition = "If Condition";
        const string k_FunctionMembers = "Function Members";
        const string k_GraphVariables = "Graph Variables";
        const string k_Function = "Function";
        const string k_Graphs = "Graphs";
        const string k_Macros = "Macros";
        const string k_Macro = "Macro";

        static readonly Vector2 k_StackOffset = new Vector2(320, 120);
        static readonly Vector2 k_ThenStackOffset = new Vector2(-220, 300);
        static readonly Vector2 k_ElseStackOffset = new Vector2(170, 300);
        static readonly Vector2 k_ClosedFlowStackOffset = new Vector2(-25, 450);

        // TODO: our builder methods ("AddStack",...) all use this field. Users should be able to create similar methods. making it public until we find a better solution
        public readonly List<SearcherItem> Items;
        public readonly Stencil Stencil;

        public GraphElementSearcherDatabase(Stencil stencil)
        {
            Stencil = stencil;
            Items = new List<SearcherItem>();
        }

        public GraphElementSearcherDatabase AddMacros()
        {
            string[] assetGUIDs = AssetDatabase.FindAssets($"t:{typeof(VSGraphAssetModel).Name}");
            List<VSGraphAssetModel> macros = assetGUIDs.Select(assetGuid =>
                AssetDatabase.LoadAssetAtPath<VSGraphAssetModel>(AssetDatabase.GUIDToAssetPath(assetGuid)))
                .Where(x =>
                {
                    if (x.GraphModel == null)
                    {
                        Debug.Log("No GraphModel");
                    }else if (x.GraphModel.Stencil == null)
                    {
                        Debug.Log("No Stencil");
                    }else
                        return x.GraphModel.Stencil.GetType() == typeof(MacroStencil);

                    return false;
                })
                .ToList();

            if (macros.Count == 0)
                return this;

            SearcherItem parent = SearcherItemUtility.GetItemFromPath(Items, k_Macros);

            foreach (VSGraphAssetModel macro in macros)
            {
                parent.AddChild(new GraphNodeModelSearcherItem(
                    new GraphAssetSearcherItemData(macro),
                    data => ((VSGraphModel)data.GraphModel).CreateMacroRefNode(
                        macro.GraphModel as VSGraphModel,
                        data.Position,
                        data.SpawnFlags
                    ),
                    $"{k_Macro} {macro.name}"
                ));
            }

            return this;
        }

        public GraphElementSearcherDatabase AddGraphsMethods()
        {
            string[] assetGUIDs = AssetDatabase.FindAssets($"t:{typeof(VSGraphAssetModel).Name}");
            List<Tuple<IGraphModel, FunctionModel>> methods = assetGUIDs.SelectMany(assetGuid =>
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                VSGraphAssetModel graphAssetModel = AssetDatabase.LoadAssetAtPath<VSGraphAssetModel>(assetPath);

                if (!graphAssetModel || ((Object)graphAssetModel.GraphModel) == null)
                    return Enumerable.Empty<Tuple<IGraphModel, FunctionModel>>();

                var functionModels = graphAssetModel.GraphModel.NodeModels.OfExactType<FunctionModel>()
                        .Select(fm => new Tuple<IGraphModel, FunctionModel>(fm.GraphModel, fm));

                return functionModels.Concat(graphAssetModel.GraphModel.NodeModels.OfExactType<EventFunctionModel>()
                        .Select(fm => new Tuple<IGraphModel, FunctionModel>(fm.GraphModel, fm)));
            }).ToList();

            if (methods.Count == 0)
                return this;

            TypeHandle voidTypeHandle = typeof(void).GenerateTypeHandle(Stencil);

            foreach (Tuple<IGraphModel, FunctionModel> method in methods)
            {
                IGraphModel graphModel = method.Item1;
                FunctionModel functionModel = method.Item2;
                string graphName = graphModel.AssetModel.Name;
                var name = $"{k_Function} {functionModel.Title}";
                SearcherItem graphRoot = SearcherItemUtility.GetItemFromPath(Items, $"{k_Graphs}/{graphName}");

                if (functionModel.ReturnType == voidTypeHandle)
                {
                    graphRoot.AddChild(new StackNodeModelSearcherItem(
                        new FunctionRefSearcherItemData(graphModel, functionModel),
                        data => ((StackBaseModel)data.StackModel).CreateFunctionRefCallNode(
                            functionModel,
                            data.Index,
                            data.SpawnFlags
                        ),
                        name
                    ));
                    continue;
                }

                graphRoot.AddChild(new GraphNodeModelSearcherItem(
                    new FunctionRefSearcherItemData(graphModel, functionModel),
                    data => ((VSGraphModel)data.GraphModel).CreateFunctionRefCallNode(
                        functionModel,
                        data.Position,
                        data.SpawnFlags
                    ),
                    name
                ));
            }

            return this;
        }

        public GraphElementSearcherDatabase AddGraphAssetMembers(IGraphModel graph)
        {
            SearcherItem parent = null;
            TypeHandle voidTypeHandle = Stencil.GenerateTypeHandle(typeof(void));

            foreach (var functionModel in graph.NodeModels.OfType<FunctionModel>())
            {
                if (parent == null)
                {
                    parent = SearcherItemUtility.GetItemFromPath(Items, graph.Name);
                }

                if (functionModel.ReturnType == voidTypeHandle)
                {
                    parent.AddChild(new StackNodeModelSearcherItem(
                        new GraphAssetSearcherItemData(graph.AssetModel),
                        data => ((StackBaseModel)data.StackModel).CreateFunctionRefCallNode(
                            functionModel,
                            data.Index,
                            data.SpawnFlags
                        ),
                        functionModel.Title
                    ));
                    continue;
                }

                parent.AddChild(new GraphNodeModelSearcherItem(
                    new GraphAssetSearcherItemData(graph.AssetModel),
                    data => ((VSGraphModel)data.GraphModel).CreateFunctionRefCallNode(
                        functionModel,
                        data.Position,
                        data.SpawnFlags
                    ),
                    functionModel.Title
                ));
            }

            return this;
        }

        public GraphElementSearcherDatabase AddNodesWithSearcherItemAttribute()
        {
            var types = TypeCache.GetTypesWithAttribute<SearcherItemAttribute>();
            foreach (var type in types)
            {
                var attributes = type.GetCustomAttributes<SearcherItemAttribute>().ToList();
                if (!attributes.Any())
                    continue;

                foreach (var attribute in attributes)
                {
                    if (!attribute.StencilType.IsInstanceOfType(Stencil))
                        continue;

                    var name = attribute.Path.Split('/').Last();
                    var path = attribute.Path.Remove(attribute.Path.LastIndexOf('/') + 1);

                    switch (attribute.Context)
                    {
                        case SearcherContext.Graph:
                        {
                            var node = new GraphNodeModelSearcherItem(
                                new NodeSearcherItemData(type),
                                data =>
                                {
                                    var vsGraphModel = (VSGraphModel)data.GraphModel;
                                    return vsGraphModel.CreateNode(type, name, data.Position, data.SpawnFlags);

                                },
                                name
                            );
                            Items.AddAtPath(node, path);
                            break;
                        }

                        case SearcherContext.Stack:
                        {
                            var node = new StackNodeModelSearcherItem(
                                new NodeSearcherItemData(type),
                                data =>
                                {
                                    var stackModel = (StackBaseModel)data.StackModel;
                                    return stackModel.CreateStackedNode(type, name, data.Index, data.SpawnFlags);
                                },
                                name
                            );
                            Items.AddAtPath(node, path);
                            break;
                        }

                        default:
                            Debug.LogWarning($"The node {type} is not a {SearcherContext.Stack} or " +
                                $"{SearcherContext.Graph} node, so it cannot be add in the Searcher");
                            break;
                    }

                    break;
                }
            }

            return this;
        }

        public GraphElementSearcherDatabase AddStickyNote()
        {
            var node = new GraphNodeModelSearcherItem(
                new SearcherItemData(SearcherItemTarget.StickyNote),
                data =>
                {
                    var rect = new Rect(data.Position, StickyNote.defaultSize);
                    var vsGraphModel = (VSGraphModel)data.GraphModel;

                    return data.SpawnFlags.IsOrphan()
                        ? vsGraphModel.CreateOrphanStickyNote(k_Sticky, rect)
                        : vsGraphModel.CreateStickyNote(k_Sticky, rect);
                },
                k_Sticky
            );
            Items.AddAtPath(node);

            return this;
        }

        public GraphElementSearcherDatabase AddEmptyFunction()
        {
            var node = new GraphNodeModelSearcherItem(
                new SearcherItemData(SearcherItemTarget.EmptyFunction),
                data => ((VSGraphModel)data.GraphModel).CreateFunction(
                    k_FunctionName,
                    data.Position,
                    data.SpawnFlags
                ),
                k_NewFunction
            );
            Items.AddAtPath(node);

            return this;
        }

        public GraphElementSearcherDatabase AddGroup()
        {
            var node = new GraphNodeModelSearcherItem(
                new SearcherItemData(SearcherItemTarget.Group),
                data => ((VSGraphModel)data.GraphModel).CreateGroup(
                    k_Group,
                    data.Position,
                    data.SpawnFlags
                ),
                k_Group
            );
            Items.AddAtPath(node);

            return this;
        }

        public GraphElementSearcherDatabase AddStack()
        {
            var node = new GraphNodeModelSearcherItem(
                new SearcherItemData(SearcherItemTarget.Stack),
                data => ((VSGraphModel)data.GraphModel).CreateStack(
                    string.Empty,
                    data.Position,
                    data.SpawnFlags
                ),
                k_Stack
            );
            Items.AddAtPath(node);

            return this;
        }

        public GraphElementSearcherDatabase AddInlineExpression()
        {
            var node = new GraphNodeModelSearcherItem(
                new SearcherItemData(SearcherItemTarget.InlineExpression),
                data => ((VSGraphModel)data.GraphModel).CreateInlineExpressionNode(
                    k_InlineLabel,
                    data.Position,
                    data.SpawnFlags
                ),
                k_InlineExpression
            );
            Items.AddAtPath(node, k_Constant);

            return this;
        }

        public GraphElementSearcherDatabase AddBinaryOperators()
        {
            SearcherItem parent = SearcherItemUtility.GetItemFromPath(Items, k_Operator);

            foreach (BinaryOperatorKind kind in Enum.GetValues(typeof(BinaryOperatorKind)))
            {
                parent.AddChild(new GraphNodeModelSearcherItem(
                    new BinaryOperatorSearcherItemData(kind),
                    data => ((VSGraphModel)data.GraphModel).CreateBinaryOperatorNode(
                        kind,
                        data.Position,
                        data.SpawnFlags
                    ),
                    kind.ToString()
                ));
            }

            return this;
        }

        public GraphElementSearcherDatabase AddUnaryOperators()
        {
            SearcherItem parent = SearcherItemUtility.GetItemFromPath(Items, k_Operator);

            foreach (UnaryOperatorKind kind in Enum.GetValues(typeof(UnaryOperatorKind)))
            {
                if (kind == UnaryOperatorKind.PostDecrement || kind == UnaryOperatorKind.PostIncrement)
                {
                    parent.AddChild(new StackNodeModelSearcherItem(
                        new UnaryOperatorSearcherItemData(kind),
                        data => ((StackBaseModel)data.StackModel).CreateUnaryStatementNode(
                            kind,
                            data.Index,
                            data.SpawnFlags
                        ),
                        kind.ToString()
                    ));
                    continue;
                }

                parent.AddChild(new GraphNodeModelSearcherItem(
                    new UnaryOperatorSearcherItemData(kind),
                    data => ((VSGraphModel)data.GraphModel).CreateUnaryOperatorNode(
                        kind,
                        data.Position,
                        data.SpawnFlags
                    ),
                    kind.ToString()
                ));
            }

            return this;
        }

        public GraphElementSearcherDatabase AddControlFlows()
        {
            AddIfCondition(IfConditionMode.Basic);
            AddIfCondition(IfConditionMode.Advanced);
            AddIfCondition(IfConditionMode.Complete);

            SearcherItem parent = null;
            var loopTypes = TypeCache.GetTypesDerivedFrom<LoopStackModel>();

            foreach (var loopType in loopTypes.Where(t => !t.IsAbstract))
            {
                if (parent == null)
                {
                    parent = SearcherItemUtility.GetItemFromPath(Items, k_ControlFlow);
                }

                var name = $"{VseUtility.GetTitle(loopType)}{k_LoopStack}";
                parent.AddChild(new StackNodeModelSearcherItem(
                    new ControlFlowSearcherItemData(loopType),
                    data =>
                    {
                        var stackModel = (StackBaseModel)data.StackModel;
                        var elements = new List<IGraphElementModel>();

                        var graphModel = (VSGraphModel)stackModel.GraphModel;
                        var stackPosition = new Vector2(
                            stackModel.Position.x + k_StackOffset.x,
                            stackModel.Position.y + k_StackOffset.y
                        );

                        LoopStackModel loopStack = graphModel.CreateLoopStack(
                            loopType,
                            stackPosition,
                            data.SpawnFlags
                        );

                        var node = loopStack.CreateLoopNode(
                            stackModel,
                            data.Index,
                            data.SpawnFlags);

                        elements.Add(node);
                        elements.Add(loopStack);

                        var edge = data.SpawnFlags.IsOrphan()
                            ? graphModel.CreateOrphanEdge(loopStack.InputPort, node.OutputPort)
                            : graphModel.CreateEdge(loopStack.InputPort, node.OutputPort);
                        elements.Add(edge);

                        return elements.ToArray();
                    },
                    name
                ));
            }

            return this;
        }

        void AddIfCondition(IfConditionMode mode)
        {
            var nodeName = $"{k_IfCondition} {mode}";
            var node = new StackNodeModelSearcherItem(
                new ControlFlowSearcherItemData(typeof(IfConditionNodeModel)),
                data =>
                {
                    var elements = new List<IGraphElementModel>();
                    var stackModel = (StackBaseModel)data.StackModel;

                    // Create If node
                    var ifConditionNode = stackModel.CreateStackedNode<IfConditionNodeModel>(nodeName, spawnFlags:data.SpawnFlags);
                    elements.Add(ifConditionNode);

                    if (mode <= IfConditionMode.Basic)
                        return elements.ToArray();

                    // Create Then and Else stacks
                    var graphModel = (VSGraphModel)stackModel.GraphModel;

                    var thenPosition = new Vector2(stackModel.Position.x + k_ThenStackOffset.x,
                        stackModel.Position.y + k_ThenStackOffset.y);
                    var thenStack = graphModel.CreateStack(k_Then, thenPosition, data.SpawnFlags);
                    elements.Add(thenStack);

                    var elsePosition = new Vector2(stackModel.Position.x + k_ElseStackOffset.x,
                        stackModel.Position.y + k_ElseStackOffset.y);
                    var elseStack = graphModel.CreateStack(k_Else, elsePosition, data.SpawnFlags);
                    elements.Add(elseStack);

                    // Connect Then and Else stacks to If node
                    var thenInput = thenStack.InputPorts.First();
                    elements.Add(data.SpawnFlags.IsOrphan()
                        ? graphModel.CreateOrphanEdge(thenInput,ifConditionNode.ThenPort)
                        : graphModel.CreateEdge(thenInput,ifConditionNode.ThenPort)
                    );

                    var elseInput = elseStack.InputPorts.First();
                    elements.Add(data.SpawnFlags.IsOrphan()
                        ? graphModel.CreateOrphanEdge(elseInput,ifConditionNode.ElsePort)
                        : graphModel.CreateEdge(elseInput,ifConditionNode.ElsePort)
                    );

                    if (mode != IfConditionMode.Complete)
                        return elements.ToArray();

                    // Create End of Condition stack
                    var completeStackPosition = new Vector2(stackModel.Position.x + k_ClosedFlowStackOffset.x,
                        stackModel.Position.y + k_ClosedFlowStackOffset.y);

                    var completeFlowStack = graphModel.CreateStack("", completeStackPosition, data.SpawnFlags);
                    elements.Add(completeFlowStack);

                    // Connect to Then and Else stacks
                    var thenOutput = thenStack.OutputPorts.First();
                    var floatStackInput = completeFlowStack.InputPorts.First();
                    elements.Add(data.SpawnFlags.IsOrphan()
                        ? graphModel.CreateOrphanEdge(floatStackInput, thenOutput)
                        : graphModel.CreateEdge(floatStackInput, thenOutput)
                    );

                    var elseOutput = elseStack.OutputPorts.First();
                    elements.Add(data.SpawnFlags.IsOrphan()
                        ? graphModel.CreateOrphanEdge(floatStackInput, elseOutput)
                        : graphModel.CreateEdge(floatStackInput, elseOutput)
                    );

                    return elements.ToArray();
                },
                nodeName);

            Items.AddAtPath(node, k_ControlFlow);
        }

        public GraphElementSearcherDatabase AddConstants(IEnumerable<Type> types)
        {
            foreach (Type type in types)
            {
                AddConstants(type);
            }

            return this;
        }

        public GraphElementSearcherDatabase AddConstants(Type type)
        {
            TypeHandle handle = type.GenerateTypeHandle(Stencil);

            SearcherItem parent = SearcherItemUtility.GetItemFromPath(Items, k_Constant);
            parent.AddChild(new GraphNodeModelSearcherItem(
                new TypeSearcherItemData(handle, SearcherItemTarget.Constant),
                data => ((VSGraphModel)data.GraphModel).CreateConstantNode(
                    "",
                    handle,
                    data.Position,
                    data.SpawnFlags),
                $"{type.FriendlyName().Nicify()} {k_Constant}"
            ));

            return this;
        }

        public GraphElementSearcherDatabase AddMembers(
            IEnumerable<Type> types,
            MemberFlags memberFlags,
            BindingFlags bindingFlags,
            Dictionary<string, List<Type>> blackList = null
        )
        {
            foreach (Type type in types)
            {
                if (memberFlags.HasFlag(MemberFlags.Constructor))
                {
                    AddConstructors(type, bindingFlags);
                }

                if (memberFlags.HasFlag(MemberFlags.Field))
                {
                    AddFields(type, bindingFlags);
                }

                if (memberFlags.HasFlag(MemberFlags.Property))
                {
                    AddProperties(type, bindingFlags);
                }

                if (memberFlags.HasFlag(MemberFlags.Method))
                {
                    AddMethods(type, bindingFlags, blackList);
                }

                if (memberFlags.HasFlag(MemberFlags.Extension))
                {
                    AddExtensionMethods(type);
                }
            }

            return this;
        }

        public GraphElementSearcherDatabase AddExtensionMethods(Type type)
        {
            Dictionary<Type, List<MethodInfo>> extensions = TypeSystem.GetExtensionMethods(Stencil.GetAssemblies());

            if (!extensions.TryGetValue(type, out var methodInfos))
                return this;

            SearcherItem parent = null;

            foreach (MethodInfo methodInfo in methodInfos
                .Where(m => !m.GetParameters().Any(p => p.ParameterType.IsByRef || p.IsOut)))
            {
                if (parent == null)
                {
                    parent = SearcherItemUtility.GetItemFromPath(Items, type.FriendlyName(false));
                }

                MethodDetails details = methodInfo.GetMethodDetails();

                if (methodInfo.ReturnType != typeof(void))
                {
                    parent.AddChild(new GraphNodeModelSearcherItem(
                        new MethodSearcherItemData(methodInfo),
                        data => ((VSGraphModel)data.GraphModel).CreateFunctionCallNode(
                            methodInfo,
                            data.Position,
                            data.SpawnFlags
                        ),
                        details.MethodName,
                        details.Details
                    ));
                    continue;
                }

                parent.AddChild(new StackNodeModelSearcherItem(
                    new MethodSearcherItemData(methodInfo),
                    data => ((StackBaseModel)data.StackModel).CreateFunctionCallNode(
                        methodInfo,
                        data.Index,
                        data.SpawnFlags
                    ),
                    details.MethodName,
                    details.Details
                ));
            }

            return this;
        }

        public GraphElementSearcherDatabase AddConstructors(Type type, BindingFlags bindingFlags)
        {
            SearcherItem parent = null;

            foreach (ConstructorInfo constructorInfo in type.GetConstructors(bindingFlags))
            {
                if (parent == null)
                {
                    parent = SearcherItemUtility.GetItemFromPath(Items, type.FriendlyName(false));
                }

                MethodDetails details = constructorInfo.GetMethodDetails();
                parent.AddChild(new GraphNodeModelSearcherItem(
                    new ConstructorSearcherItemData(constructorInfo),
                    data => ((VSGraphModel)data.GraphModel).CreateFunctionCallNode(
                        constructorInfo,
                        data.Position,
                        data.SpawnFlags
                    ),
                    details.MethodName,
                    details.Details
                ));
            }

            return this;
        }

        public GraphElementSearcherDatabase AddMethods(
            Type type,
            BindingFlags bindingFlags,
            Dictionary<string, List<Type>> blackList = null
        )
        {
            SearcherItem parent = null;

            foreach (MethodInfo methodInfo in type.GetMethods(bindingFlags)
                .Where(m => !m.IsSpecialName
                    && m.GetCustomAttribute<ObsoleteAttribute>() == null
                    && m.GetCustomAttribute<HiddenAttribute>() == null
                    && !m.Name.StartsWith("get_", StringComparison.Ordinal)
                    && !m.Name.StartsWith("set_", StringComparison.Ordinal)
                    && !m.GetParameters().Any(p => p.ParameterType.IsByRef || p.IsOut || p.ParameterType.IsPointer)
                    && !SearcherItemCollectionUtility.IsMethodBlackListed(m, blackList))
                .OrderBy(m => m.Name))
            {
                if (parent == null)
                {
                    parent = SearcherItemUtility.GetItemFromPath(Items, type.FriendlyName(false));
                }

                MethodDetails details = methodInfo.GetMethodDetails();

                if (methodInfo.ReturnType == typeof(void))
                {
                    parent.AddChild(new StackNodeModelSearcherItem(
                        new MethodSearcherItemData(methodInfo),
                        data => ((StackBaseModel)data.StackModel).CreateFunctionCallNode(
                            methodInfo,
                            data.Index,
                            data.SpawnFlags
                        ),
                        details.MethodName,
                        details.Details
                    ));
                    continue;
                }

                if (!methodInfo.ReturnType.IsPointer)
                {
                    parent.AddChild(new GraphNodeModelSearcherItem(
                        new MethodSearcherItemData(methodInfo),
                        data => ((VSGraphModel)data.GraphModel).CreateFunctionCallNode(
                            methodInfo,
                            data.Position,
                            data.SpawnFlags
                        ),
                        details.MethodName,
                        details.Details
                    ));
                }
            }

            return this;
        }

        public GraphElementSearcherDatabase AddProperties(Type type, BindingFlags bindingFlags)
        {
            SearcherItem parent = null;

            foreach (PropertyInfo propertyInfo in type.GetProperties(bindingFlags)
                .OrderBy(p => p.Name)
                .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() == null
                    && p.GetCustomAttribute<HiddenAttribute>() == null))
            {
                var children = new List<SearcherItem>();

                if (propertyInfo.GetIndexParameters().Length > 0) // i.e : Vector2.this[int]
                {
                    children.Add(new GraphNodeModelSearcherItem(
                        new PropertySearcherItemData(propertyInfo),
                        data => ((VSGraphModel)data.GraphModel).CreateFunctionCallNode(
                            propertyInfo.GetMethod,
                            data.Position,
                            data.SpawnFlags
                        ),
                        propertyInfo.Name
                    ));
                }
                else
                {
                    if (propertyInfo.CanRead)
                    {
                        if (propertyInfo.GetMethod.IsStatic)
                        {
                            if (propertyInfo.CanWrite)
                            {
                                children.Add(new GraphNodeModelSearcherItem(
                                    new PropertySearcherItemData(propertyInfo),
                                    data => ((VSGraphModel)data.GraphModel).CreateFunctionCallNode(
                                        propertyInfo.GetMethod,
                                        data.Position,
                                        data.SpawnFlags
                                    ),
                                    propertyInfo.Name
                                ));
                            }
                            else
                            {
                                children.Add(new GraphNodeModelSearcherItem(
                                    new PropertySearcherItemData(propertyInfo),
                                    data => ((VSGraphModel)data.GraphModel).CreateSystemConstantNode(
                                        type,
                                        propertyInfo,
                                        data.Position,
                                        data.SpawnFlags
                                    ),
                                    propertyInfo.Name
                                ));
                            }
                        }
                        else
                        {
                            children.Add(new GraphNodeModelSearcherItem(
                                new PropertySearcherItemData(propertyInfo),
                                data =>
                                {
                                    INodeModel nodeModel = ((VSGraphModel)data.GraphModel).CreateGetPropertyGroupNode(
                                        data.Position,
                                        data.SpawnFlags
                                    );
                                    ((GetPropertyGroupNodeModel)nodeModel).AddMember(
                                        propertyInfo.GetUnderlyingType(),
                                        propertyInfo.Name
                                    );
                                    return nodeModel;
                                },
                                propertyInfo.Name
                            ));
                        }
                    }

                    if (propertyInfo.CanWrite)
                    {
                        children.Add(new StackNodeModelSearcherItem(
                            new PropertySearcherItemData(propertyInfo),
                            data => ((StackBaseModel)data.StackModel).CreateFunctionCallNode(
                                propertyInfo.SetMethod,
                                data.Index,
                                data.SpawnFlags
                            ),
                            propertyInfo.Name
                        ));
                    }
                }

                if (children.Count == 0)
                {
                    continue;
                }

                if (parent == null)
                {
                    parent = SearcherItemUtility.GetItemFromPath(Items, type.FriendlyName(false));
                }

                foreach (SearcherItem child in children)
                {
                    parent.AddChild(child);
                }
            }

            return this;
        }

        public GraphElementSearcherDatabase AddFields(Type type, BindingFlags bindingFlags)
        {
            SearcherItem parent = null;

            foreach (FieldInfo fieldInfo in type.GetFields(bindingFlags)
                .OrderBy(f => f.Name)
                .Where(f => f.GetCustomAttribute<ObsoleteAttribute>() == null
                    && f.GetCustomAttribute<HiddenAttribute>() == null))
            {
                if (parent == null)
                {
                    parent = SearcherItemUtility.GetItemFromPath(Items, type.FriendlyName(false));
                }

                parent.AddChild(new GraphNodeModelSearcherItem(
                    new FieldSearcherItemData(fieldInfo),
                    data =>
                    {
                        INodeModel nodeModel = ((VSGraphModel)data.GraphModel).CreateGetPropertyGroupNode(
                            data.Position,
                            data.SpawnFlags
                        );
                        ((GetPropertyGroupNodeModel)nodeModel).AddMember(fieldInfo.GetUnderlyingType(), fieldInfo.Name);
                        return nodeModel;
                    },
                    fieldInfo.Name
                ));

                if (fieldInfo.CanWrite())
                {
                    parent.AddChild(new StackNodeModelSearcherItem(
                        new FieldSearcherItemData(fieldInfo),
                        data =>
                        {
                            INodeModel nodeModel = ((StackBaseModel)data.StackModel).CreateSetPropertyGroupNode(
                                data.Index
                            );
                            ((SetPropertyGroupNodeModel)nodeModel).AddMember(
                                fieldInfo.GetUnderlyingType(),
                                fieldInfo.Name
                            );
                            return nodeModel;
                        },
                        fieldInfo.Name
                    ));
                }
            }

            return this;
        }

        public GraphElementSearcherDatabase AddFunctionMembers(IFunctionModel functionModel)
        {
            if (functionModel == null)
                return this;

            SearcherItem parent = null;
            IEnumerable<IVariableDeclarationModel> members = functionModel.FunctionParameterModels.Union(
                functionModel.FunctionVariableModels);

            foreach (IVariableDeclarationModel declarationModel in members)
            {
                if (parent == null)
                {
                    parent = SearcherItemUtility.GetItemFromPath(Items, k_FunctionMembers);
                }

                parent.AddChild(new GraphNodeModelSearcherItem(
                    new TypeSearcherItemData(declarationModel.DataType, SearcherItemTarget.Variable),
                    data => ((VSGraphModel)data.GraphModel).CreateVariableNode(
                        declarationModel,
                        data.Position,
                        data.SpawnFlags
                    ),
                    declarationModel.Name.Nicify()
                ));
            }

            return this;
        }

        public GraphElementSearcherDatabase AddGraphVariables(IGraphModel graphModel)
        {
            SearcherItem parent = null;
            var vsGraphModel = (VSGraphModel)graphModel;

            foreach (IVariableDeclarationModel declarationModel in vsGraphModel.GraphVariableModels)
            {
                if (parent == null)
                {
                    parent = SearcherItemUtility.GetItemFromPath(Items, k_GraphVariables);
                }

                parent.AddChild(new GraphNodeModelSearcherItem(
                    new TypeSearcherItemData(declarationModel.DataType, SearcherItemTarget.Variable),
                    data => ((VSGraphModel)data.GraphModel).CreateVariableNode(
                        declarationModel,
                        data.Position,
                        data.SpawnFlags
                    ),
                    declarationModel.Name.Nicify()
                ));
            }

            return this;
        }

        public SearcherDatabase Build()
        {
            return SearcherDatabase.Create(Items, "", false);
        }
    }
}
