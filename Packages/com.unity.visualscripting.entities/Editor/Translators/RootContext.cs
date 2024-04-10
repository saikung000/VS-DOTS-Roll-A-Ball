using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Collections;
using Unity.Entities;
using UnityEditor.VisualScripting.Model.Stencils;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    class RootContext : TranslationContext
    {
        public const string SingletonComponentTypeName = "GraphData";
        const string k_SingletonVariableName = "graphData";

        readonly RoslynEcsTranslator.TranslationOptions m_TranslationOptions;
        readonly EcsStencil m_Stencil;

        bool m_NeedToCompleteDependenciesFirst;
        ClassDeclarationSyntax m_ClassDeclaration;
        List<MemberDeclarationSyntax> m_ClassMembers = new List<MemberDeclarationSyntax>();
        List<StatementSyntax> m_InitializationStatements;
        HashSet<string> m_TakenNames = new HashSet<string>();
        List<StatementSyntax> m_UpdateStatements = new List<StatementSyntax>();
        Dictionary<RoslynEcsTranslator.IterationContext, PerGroupCtxComponents> m_WrittenComponentsPerGroup =
            new Dictionary<RoslynEcsTranslator.IterationContext, PerGroupCtxComponents>();
        Dictionary<Type, string> m_CreatedManagers = new Dictionary<Type, string>();
        IEntityManipulationTranslator m_EntityTranslator;
        LocalDeclarationStatementSyntax m_SingletonDeclarationSyntax;
        ExpressionStatementSyntax m_SingletonUpdateSyntax;

        struct QueryStateComponent
        {
            public bool Tracking;
            public string ComponentName;
        }
        Dictionary<ComponentQueryDeclarationModel, QueryStateComponent> m_QueryHasStateTracking = new Dictionary<ComponentQueryDeclarationModel, QueryStateComponent>();

        List<StatementSyntax> InitializationStatements => m_InitializationStatements
            ?? (m_InitializationStatements = new List<StatementSyntax>());

        // just exists to get the protected/internal names
        [DisableAutoCreation]
        class SafeGuardNamingSystem : ComponentSystem
        {
            public const string EntityQueryMethodName = nameof(GetEntityQuery);
            public const string OnCreateManagerName = nameof(OnCreate);
            public const string SetSingletonName = nameof(SetSingleton);
            public const string GetSingletonName = nameof(GetSingleton);

            protected override void OnUpdate() => throw new NotImplementedException();
        }

        public RootContext(EcsStencil stencil, string systemName, RoslynEcsTranslator.TranslationOptions translationOptions)
            : base(null)
        {
            m_Stencil = stencil;
            m_TranslationOptions = translationOptions;
            m_ClassDeclaration = ClassDeclaration(systemName)
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)));
        }

        public override Allocator AllocatorType => Allocator.TempJob; // Weird but it throws at runtime otherwise
        public override RoslynEcsTranslator.TranslationOptions TranslationOptions => m_TranslationOptions;

        public override void RecordComponentAccess(
            RoslynEcsTranslator.IterationContext group,
            TypeHandle componentType,
            RoslynEcsTranslator.AccessMode mode)
        {
            if(mode > RoslynEcsTranslator.AccessMode.Read)
                m_WrittenComponentsPerGroup[group].WrittenComponents.Add(componentType);
        }

        public override ExpressionSyntax GetCachedValue(string key, ExpressionSyntax value, TypeHandle modelReturnType, params IdentifierNameSyntax[] attributes)
        {
            return value;
        }

        protected override StatementSyntax GetOrDeclareEntityArray(RoslynEcsTranslator.IterationContext iterationContext, out StatementSyntax arrayDisposal)
        {
            arrayDisposal = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(iterationContext.EntitiesArrayName),
                            IdentifierName(nameof(IDisposable.Dispose)))))
                .NormalizeWhitespace();
            return RoslynBuilder.DeclareLocalVariable(
                typeof(NativeArray<Entity>),
                iterationContext.EntitiesArrayName,
                ForEachContext.MakeInitEntityArray(iterationContext),
                RoslynBuilder.VariableDeclarationType.InferredType);
        }

        protected override string IncludeTrackingSystemStateComponent(ComponentQueryDeclarationModel query, bool trackProcessed)
        {
            if (!m_QueryHasStateTracking.TryGetValue(query, out var stateComponentDesc))
                stateComponentDesc = new QueryStateComponent { Tracking = trackProcessed, ComponentName = $"{query.VariableName}Tracking" };
            else
                stateComponentDesc.Tracking |= trackProcessed;
            m_QueryHasStateTracking[query] = stateComponentDesc;

            return stateComponentDesc.ComponentName;
        }

        HashSet<Type> m_EventSystems = new HashSet<Type>();
        public override bool GetEventSystem(RoslynEcsTranslator.IterationContext iterationContext, Type eventType)
        {
            m_EventSystems.Add(eventType);
            return m_WrittenComponentsPerGroup[iterationContext].WrittenEvents.Add(eventType);
        }

        public override void AddStatement(StatementSyntax mds)
        {
            m_UpdateStatements.Add(mds);
        }

        public override void AddEntityDeclaration(string variableName)
        {
            throw new NotImplementedException();
        }

        public override string GetJobIndexParameterName()
        {
            throw new NotImplementedException("RootContext.GetJobIndexParameterName");
        }

        class PerGroupCtxComponents
        {
            public readonly RoslynEcsTranslator.IterationContext Context;
            public HashSet<TypeHandle> WrittenComponents = new HashSet<TypeHandle>();
            public HashSet<Type> WrittenEvents = new HashSet<Type>();

            public PerGroupCtxComponents(RoslynEcsTranslator.IterationContext context)
            {
                Context = context;
            }
        }

        public override string GetOrDeclareComponentQuery(RoslynEcsTranslator.IterationContext ctx)
        {
            // TODO: we're overwriting previous iterations on the same group
            m_WrittenComponentsPerGroup.Add(ctx, new PerGroupCtxComponents(ctx));

            return ctx.GroupName;
        }

        public override string GetOrDeclareComponentArray(
            RoslynEcsTranslator.IterationContext ctx,
            ComponentDefinition componentDefinition,
            out LocalDeclarationStatementSyntax arrayInitialization,
            out StatementSyntax arrayDisposal)
        {
            Type resolvedType = componentDefinition.TypeHandle.Resolve(m_Stencil);
            var arrayName = ctx.GetComponentDataArrayName(resolvedType);

            Type arrayType = typeof(NativeArray<>).MakeGenericType(resolvedType);
            arrayInitialization = RoslynBuilder.DeclareLocalVariable(
                arrayType, arrayName,
                MakeInitComponentDataArrayExpression(ctx, resolvedType),
                RoslynBuilder.VariableDeclarationType.InferredType);

            arrayDisposal = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(arrayName),
                            IdentifierName(nameof(IDisposable.Dispose)))))
                .NormalizeWhitespace();

            return arrayName;
        }

        public override TranslationContext PushContext(IIteratorStackModel query, RoslynEcsTranslator roslynEcsTranslator, UpdateMode mode)
        {
            if (roslynEcsTranslator.GameObjectCodeGen || !m_TranslationOptions.HasFlag(RoslynEcsTranslator.TranslationOptions.UseJobs))
            {
                m_NeedToCompleteDependenciesFirst = true;
                return new ForEachLambdaContext(query, this, mode);
            }

            return new JobContext(query, this, mode) { BurstCompileJobs = m_TranslationOptions.HasFlag(RoslynEcsTranslator.TranslationOptions.BurstCompile)};
        }

        protected override StatementSyntax OnPopContext()
        {
            throw new NotImplementedException("RootContext.OnPopContext");
        }

        public override ExpressionSyntax GetSingletonVariable(IVariableDeclarationModel variable)
        {
            if (m_SingletonDeclarationSyntax == null)
            {
                var init = RoslynBuilder.MethodInvocation(
                    SafeGuardNamingSystem.GetSingletonName,
                    null,
                    Enumerable.Empty<ArgumentSyntax>(),
                    new[] { IdentifierName(SingletonComponentTypeName) });
                m_SingletonDeclarationSyntax = RoslynBuilder.DeclareLocalVariable(
                    SingletonComponentTypeName,
                    k_SingletonVariableName,
                    init);
                AddStatement(m_SingletonDeclarationSyntax);
            }

            return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(k_SingletonVariableName),
                    IdentifierName(variable.VariableName));
        }

        internal override void RequestSingletonUpdate()
        {
            m_SingletonUpdateSyntax = ExpressionStatement(
                RoslynBuilder.MethodInvocation(
                    SafeGuardNamingSystem.SetSingletonName,
                    null,
                    new[] { Argument(IdentifierName(k_SingletonVariableName)) },
                    Enumerable.Empty<TypeSyntax>()));
        }

        public override IEntityManipulationTranslator GetEntityManipulationTranslator()
        {
            return m_EntityTranslator ?? (m_EntityTranslator = new EntityManipulationTranslator());
        }

        public override IEnumerable<ComponentDefinition> GetComponentDefinitions()
        {
            throw new NotImplementedException();
        }

        public override ExpressionSyntax GetOrDeclareCommandBuffer(bool isConcurrent)
        {
            const string endFrameBarrier = "m_EndFrameBarrier";
            DeclareManager<EndSimulationEntityCommandBufferSystem>(endFrameBarrier);

            if (isConcurrent)
            {
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(endFrameBarrier),
                                IdentifierName(nameof(EntityCommandBufferSystem.CreateCommandBuffer)))),
                        IdentifierName(nameof(EntityCommandBuffer.ToConcurrent))));
            }

            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(endFrameBarrier),
                    IdentifierName(nameof(EntityCommandBufferSystem.CreateCommandBuffer))));
        }

        public override string MakeUniqueName(string s)
        {
            if (m_TakenNames.Add(s))
                return s;

            var i = 0;
            var s2 = s + i;
            while (!m_TakenNames.Add(s2))
                s2 = s + ++i;
            return s2;
        }

        public override string GetComponentVariableName(IIteratorStackModel groupDeclaration, TypeHandle componentVariableType1)
        {
            throw new NotImplementedException();
        }

        public void AddMember(MemberDeclarationSyntax mds)
        {
            m_ClassMembers.Add(mds);
        }

        public ClassDeclarationSyntax Build(RoslynTranslator translator, VSGraphModel graphModel)
        {
            var baseClass = m_NeedToCompleteDependenciesFirst ? nameof(ComponentSystem) : nameof(JobComponentSystem);
            m_ClassDeclaration = m_ClassDeclaration.WithBaseList(
                BaseList(
                    SingletonSeparatedList<BaseTypeSyntax>(
                        SimpleBaseType(
                            IdentifierName(baseClass)))));

            foreach (var queryTracking in m_QueryHasStateTracking)
            {
                var trackingMembers = new List<MemberDeclarationSyntax>();
                if (queryTracking.Value.Tracking)
                    trackingMembers.Add(
                        FieldDeclaration(
                                VariableDeclaration(
                                        PredefinedType(
                                            Token(SyntaxKind.BoolKeyword)))
                                    .WithVariables(
                                        SingletonSeparatedList(
                                            VariableDeclarator(
                                                Identifier("Processed")))))
                            .WithModifiers(
                                TokenList(
                                    Token(SyntaxKind.InternalKeyword))));

                DeclareComponent<ISystemStateComponentData>(queryTracking.Value.ComponentName, trackingMembers);
            }

            foreach (var eventSystem in m_EventSystems)
            {
                //  ClearEvents<TestEvent2>.Initialize(World);
                InitializationStatements.Add(ExpressionStatement(InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            GenericName(
                                    Identifier("EventSystem"))
                                .WithTypeArgumentList(
                                    TypeArgumentList(
                                        SingletonSeparatedList<TypeSyntax>(
                                            IdentifierName(eventSystem.FullName)))),
                            IdentifierName("Initialize")))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(
                                    IdentifierName("World")))))));
            }

            foreach (var group in m_WrittenComponentsPerGroup)
                DeclareEntityQueries(group.Value);

            var singletonMembers = new List<FieldDeclarationSyntax>();
            var initArguments = new List<AssignmentExpressionSyntax>();
            foreach (var graphVariable in graphModel.GraphVariableModels
                .Where(g => g.VariableType == VariableType.GraphVariable))
            {
                singletonMembers.Add(graphVariable.DeclareField(translator, false));
                initArguments.Add(RoslynBuilder.Assignment(
                    IdentifierName(graphVariable.VariableName),
                    translator.Constant(graphVariable.InitializationModel.ObjectValue, m_Stencil))
                    as AssignmentExpressionSyntax);
            }

            if (singletonMembers.Any())
            {
                DeclareComponent<IComponentData>(SingletonComponentTypeName, singletonMembers);
                InitializationStatements.Add(ExpressionStatement(
                    RoslynBuilder.MethodInvocation(
                        nameof(EntityManager.CreateEntity),
                        IdentifierName(nameof(EntityManager)),
                        new[]
                        {
                            Argument(TypeOfExpression(IdentifierName(SingletonComponentTypeName)))
                        },
                        Enumerable.Empty<TypeSyntax>())));
                InitializationStatements.Add(
                    ExpressionStatement(
                        RoslynBuilder.MethodInvocation(
                            SafeGuardNamingSystem.SetSingletonName,
                            null,
                            new[]
                            {
                                Argument(RoslynBuilder.DeclareNewObject(
                                    IdentifierName(SingletonComponentTypeName),
                                    Enumerable.Empty<ArgumentSyntax>(),
                                    initArguments))
                            },
                            Enumerable.Empty<TypeSyntax>())));
            }

            // TODO : Remove this once there is real Systems' dependency tool
            var attributes = new List<AttributeListSyntax>();
            foreach (var assetModel in m_Stencil.UpdateAfter.Where(a => a.GraphModel.Stencil is EcsStencil))
                RegisterAttributes<UpdateAfterAttribute>(attributes, assetModel.Name);

            foreach (var assetModel in m_Stencil.UpdateBefore.Where(a => a.GraphModel.Stencil is EcsStencil))
                RegisterAttributes<UpdateBeforeAttribute>(attributes, assetModel.Name);

            m_ClassDeclaration = m_ClassDeclaration
                .AddAttributeLists(attributes.ToArray())
                .AddMembers(m_ClassMembers.OrderBy(x => x is FieldDeclarationSyntax ? 0 : 1).ToArray());

            if (m_InitializationStatements != null)
            {
                var onCreateManagerOverride = RoslynBuilder.DeclareMethod(
                    SafeGuardNamingSystem.OnCreateManagerName,
                    AccessibilityFlags.Protected | AccessibilityFlags.Override,
                    typeof(void));
                m_ClassDeclaration = m_ClassDeclaration
                    .AddMembers(
                        onCreateManagerOverride.WithBody(
                            Block(m_InitializationStatements)));
            }

            if (m_SingletonUpdateSyntax != null)
                AddStatement(m_SingletonUpdateSyntax);

            var onUpdateBlock = Block(m_UpdateStatements);
            return m_ClassDeclaration.AddMembers(
                RoslynEcsTranslator.MakeOnUpdateOverride(
                    onUpdateBlock,
                    m_NeedToCompleteDependenciesFirst,
                    m_CreatedManagers));
        }

        static void RegisterAttributes<T>(ICollection<AttributeListSyntax> list, string arg) where T : Attribute
        {
            list.Add(AttributeList(
                SeparatedList(
                    new[]
                    {
                        Attribute(IdentifierName(typeof(T).Name))
                            .WithArgumentList(
                                AttributeArgumentList(
                                    SingletonSeparatedList(
                                        AttributeArgument(
                                            TypeOfExpression(IdentifierName(arg))))))
                    })));
        }

        internal static ExpressionSyntax MakeInitComponentDataArrayExpression(
            RoslynEcsTranslator.IterationContext ctx,
            Type componentType)
        {
            return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(ctx.GroupName),
                        GenericName(
                                Identifier("ToComponentDataArray"))
                            .WithTypeArgumentList(
                                TypeArgumentList(
                                    SingletonSeparatedList(
                                        TypeSystem.BuildTypeSyntax(componentType))))))
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(nameof(Allocator)),
                                    IdentifierName(ctx.AllocatorType.ToString()))))))
                .NormalizeWhitespace();
        }

        static IEnumerable<(string, IEnumerable<ArgumentSyntax>)> BuildQueries(
            PerGroupCtxComponents ctx,
            Dictionary<ComponentQueryDeclarationModel, QueryStateComponent> queryHasStateTracking,
            IEnumerable<ArgumentSyntax> arguments,
            string contextGroupName)
        {
            IEnumerable<ArgumentSyntax> PrependComponentToArguments(QueryStateComponent trackingComponent, string methodName)
            {
                return Enumerable.Repeat(ComponentTypeDeclarationSyntax(IdentifierName(trackingComponent.ComponentName), methodName), 1)
                    .Concat(arguments);
            }
            IEnumerable<ArgumentSyntax> PrependComponentFromTypeToArguments(Type t, string methodName)
            {
                return Enumerable.Repeat(ComponentTypeDeclarationSyntax(t.ToTypeSyntax(), methodName), 1)
                    .Concat(arguments);
            }

            if (ctx.Context.Query is IPrivateIteratorStackModel updateMatching)
            {
                if(updateMatching.Mode == UpdateMode.OnEvent)
                {
                    var eventType = ((OnEventNodeModel)updateMatching).EventTypeHandle.Resolve(updateMatching.GraphModel.Stencil);
                    yield return (SendEventTranslator.MakeQueryIncludingEventName(ctx.Context, eventType),
                        PrependComponentFromTypeToArguments(
                            eventType, nameof(ComponentType.ReadOnly)));
                }

                if (queryHasStateTracking.TryGetValue(ctx.Context.Query.ComponentQueryDeclarationModel, out var trackingComponent))
                {
                    string methodName = null;
                    switch (updateMatching.Mode)
                    {
                        case UpdateMode.OnUpdate:
                        case UpdateMode.OnEvent:
                            methodName = nameof(ComponentType.ReadWrite);
                            break;
                        case UpdateMode.OnStart:
                            methodName = nameof(ComponentType.Exclude);
                            break;
                        case UpdateMode.OnEnd:
                            yield return (GetQueryAddStateName(contextGroupName), PrependComponentToArguments(trackingComponent, nameof(ComponentType.Exclude)));
                            methodName = nameof(ComponentType.ReadOnly);
                            break;
                    }

                    arguments = PrependComponentToArguments(trackingComponent, methodName);
                }

                // query_MissingEvents is only used in non-jobs context
                if((ctx.Context.TranslationOptions & RoslynEcsTranslator.TranslationOptions.UseJobs) == 0)
                {
                    foreach (Type eventType in ctx.WrittenEvents)
                    {
                        yield return (SendEventTranslator.MakeMissingEventQueryName(ctx.Context, eventType),
                            PrependComponentFromTypeToArguments(
                                eventType, nameof(ComponentType.Exclude)));
                    }
                }
            }
            yield return (contextGroupName, arguments);

        }

        // TODO not pretty
        internal static string GetQueryAddStateName(string contextGroupName)
        {
            return $"{contextGroupName}_AddState";
        }

        void DeclareEntityQueries(PerGroupCtxComponents ctx)
        {
            List<ComponentDefinition> flattenedComponentDefinitions = ctx.Context.FlattenedComponentDefinitions().ToList();

            IEnumerable<ArgumentSyntax> arguments = BuildArguments(ctx, flattenedComponentDefinitions);

            foreach (var (name, buildQuery) in BuildQueries(ctx, m_QueryHasStateTracking, arguments,  ctx.Context.GroupName))
            {
                var initValue = !buildQuery.Any()
                    ? (ExpressionSyntax)MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("EntityManager"),
                        IdentifierName("UniversalQuery"))
                    : InvocationExpression(IdentifierName(SafeGuardNamingSystem.EntityQueryMethodName))
                        .WithArgumentList(ArgumentList(SeparatedList(buildQuery)));

                DeclareAndInitField(typeof(EntityQuery), name, initValue: initValue);
            }
        }

        static ArgumentSyntax ComponentTypeDeclarationSyntax(TypeSyntax c, string declarationType)
        {
            return Argument(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(Identifier(TriviaList(Whitespace("    ")), nameof(ComponentType), TriviaList())),
                        GenericName(Identifier(declarationType))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(c))))));
        }

        IEnumerable<ArgumentSyntax> BuildArguments(PerGroupCtxComponents ctx, List<ComponentDefinition> flattenedComponentDefinitions)
        {
            IEnumerable<ArgumentSyntax> ComponentTypeHandlesDeclarationSyntax(IEnumerable<ComponentDefinition> componentDefinitions, string declarationType)
            {
                return componentDefinitions.Select(c => ComponentTypeDeclarationSyntax(c.TypeHandle.ToTypeSyntax(ctx.Context.Stencil), declarationType));
            }

            var excludedComponents = flattenedComponentDefinitions.Where(c => c.Subtract);
            var writtenComponents = flattenedComponentDefinitions.Where(c => !c.Subtract && ctx.WrittenComponents.Contains(c.TypeHandle));
            var readonlyComponents = flattenedComponentDefinitions.Where(c => !c.Subtract && !ctx.WrittenComponents.Contains(c.TypeHandle));

            IEnumerable<ArgumentSyntax> arguments = ComponentTypeHandlesDeclarationSyntax(excludedComponents, nameof(ComponentType.Exclude))
                .Concat(ComponentTypeHandlesDeclarationSyntax(writtenComponents, nameof(ComponentType.ReadWrite)))
                .Concat(ComponentTypeHandlesDeclarationSyntax(readonlyComponents, nameof(ComponentType.ReadOnly)));
            return arguments;
        }


        void DeclareAndInitField(Type t, string name, AccessibilityFlags accessibility = AccessibilityFlags.Private,
            ExpressionSyntax initValue = null)
        {
            var entityGroup = RoslynBuilder
                .DeclareField(t, name)
                .WithModifiers(TokenList(RoslynBuilder.AccessibilityToSyntaxToken(accessibility)));
            AddMember(entityGroup);

            if (initValue == null)
                return;

            StatementSyntax init = ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(name),
                        initValue))
                .NormalizeWhitespace();

            InitializationStatements.Add(init);
        }

        void DeclareManager<T>(string name) where T : ComponentSystemBase
        {
            var type = typeof(T);
            if (m_CreatedManagers.ContainsKey(type))
                return;

            m_CreatedManagers.Add(type, name);

            var initValue = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(nameof(World)),
                    GenericName(
                            Identifier(nameof(World.GetOrCreateSystem)))
                        .WithTypeArgumentList(
                            TypeArgumentList(
                                SingletonSeparatedList<TypeSyntax>(
                                    IdentifierName(type.Name))))));

            DeclareAndInitField(type, name, AccessibilityFlags.Private, initValue);
        }

        public override void DeclareComponent<T>(string componentName,
            IEnumerable<MemberDeclarationSyntax> members = null)
        {
            AddMember(RoslynEcsBuilder.DeclareComponent(componentName, typeof(T), members));
        }

        public override ExpressionSyntax GetEventBufferWriter(RoslynEcsTranslator.IterationContext iterationContext, ExpressionSyntax entity, Type eventType, out StatementSyntax bufferInitialization)
        {
            bufferInitialization = null;
            var bufferVariableName = SendEventTranslator.GetBufferVariableName(iterationContext, eventType);
            iterationContext.WrittenEventTypes.Add(eventType);
            if (GetEventSystem(iterationContext, eventType))
            {
                // var buffer = EntityManager.GetBuffer<EventType>(entity);
                var bufferInitValue = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("EntityManager"),
                            GenericName(
                                    Identifier("GetBuffer"))
                                .WithTypeArgumentList(
                                    TypeArgumentList(
                                        SingletonSeparatedList(
                                            eventType.ToTypeSyntax())))))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(entity)
                            )));
                bufferInitialization = RoslynBuilder.DeclareLocalVariable((Type)null, bufferVariableName, bufferInitValue, RoslynBuilder.VariableDeclarationType.InferredType);
            }
            return IdentifierName(bufferVariableName);
        }

        protected override StatementSyntax AddMissingEventBuffers(RoslynEcsTranslator.IterationContext iterationContext, StatementSyntax onPopContext)
        {
            BlockSyntax b = onPopContext is BlockSyntax bl ? bl : Block(onPopContext);
            var before = new List<StatementSyntax>();
            foreach (Type eventType in iterationContext.WrittenEventTypes)
            {
                // ClearEvents<EventType>.AddMissingBuffers(<target query>, EntityManager);
                before.Add(ExpressionStatement(
                    InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                GenericName(
                                        Identifier("EventSystem"))
                                    .WithTypeArgumentList(
                                        TypeArgumentList(
                                            SingletonSeparatedList(
                                                eventType.ToTypeSyntax()))),
                                IdentifierName("AddMissingBuffers")))
                        .WithArgumentList(
                            ArgumentList(
                                SeparatedList(
                                    new ArgumentSyntax[]
                                    {
                                        Argument(IdentifierName("Entities")),
                                        Argument(IdentifierName(SendEventTranslator.MakeMissingEventQueryName(iterationContext, eventType))),
                                        Argument(IdentifierName("EntityManager"))
                                    })))));
            }

            b = b.WithStatements(b.Statements.InsertRange(0, before));
            return b;
        }
    }
}
