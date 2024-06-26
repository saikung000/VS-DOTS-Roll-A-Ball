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
using UnityEngine.Assertions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    sealed class JobContext : TranslationContext
    {
        public bool BurstCompileJobs { private get; set; }

        string m_JobName;
        List<StatementSyntax> m_UpdateStatements = new List<StatementSyntax>();
        List<MemberDeclarationSyntax> m_MemberDeclarations = new List<MemberDeclarationSyntax>();
        List<ExpressionSyntax> m_JobInitializers = new List<ExpressionSyntax>();
        HashSet<TypeHandle> m_WrittenComponents = new HashSet<TypeHandle>();
        HashSet<TypeHandle> m_ExcludedComponents;
        Dictionary<ComponentDefinition, string> m_DeclaredComponentArray = new Dictionary<ComponentDefinition, string>();
        Dictionary<string, IdentifierNameSyntax> m_Constants = new Dictionary<string, IdentifierNameSyntax>();
        Dictionary<bool, IdentifierNameSyntax> m_CommandBuffers = new Dictionary<bool, IdentifierNameSyntax>();
        IEntityManipulationTranslator m_EntityTranslator;
        bool m_UpdateSingletonRequested;
        bool m_ScheduleSingleThreaded;

        public JobContext(IIteratorStackModel query, RootContext parent, UpdateMode mode)
            : base(parent)
        {
            IterationContext = new RoslynEcsTranslator.IterationContext(this, query, parent.MakeUniqueName(query.ComponentQueryDeclarationModel.VariableName), mode);
            m_JobName = query is OnEventNodeModel onEventNodeModel
                ? parent.MakeUniqueName($"On_{onEventNodeModel.EventTypeHandle.Name(IterationContext.Stencil)}_Job")
                : parent.MakeUniqueName($"Update_{IterationContext.GroupName}_Job");
            m_ExcludedComponents = new HashSet<TypeHandle>(query.ComponentQueryDeclarationModel.Components.Where(c => c.Component.Subtract)
                .Select(c => c.Component.TypeHandle));
            GetOrDeclareComponentQuery(IterationContext);
        }

        public override Allocator AllocatorType => Allocator.TempJob;

        public override TranslationContext PushContext(IIteratorStackModel query, RoslynEcsTranslator roslynEcsTranslator, UpdateMode mode)
        {
            return new ForEachContext(query, this, mode);
        }

        public override void AddEntityDeclaration(string variableName)
        {
            EntityName = variableName;
        }

        public override string GetJobIndexParameterName()
        {
            return $"{m_JobName}Idx";
        }

        public override string GetOrDeclareComponentQuery(RoslynEcsTranslator.IterationContext ctx)
        {
            return Parent.GetOrDeclareComponentQuery(ctx);
        }

        public override void RecordComponentAccess(RoslynEcsTranslator.IterationContext group, TypeHandle componentType, RoslynEcsTranslator.AccessMode mode)
        {
            Parent.RecordComponentAccess(group, componentType, mode);
            if (IterationContext == group && mode == RoslynEcsTranslator.AccessMode.Write)
                m_WrittenComponents.Add(componentType);
        }

        public override ExpressionSyntax GetCachedValue(string key, ExpressionSyntax value, TypeHandle modelReturnType, params IdentifierNameSyntax[] attributes)
        {
            key = key.Replace(".", "_");
            if (m_Constants.TryGetValue(key, out var node))
                return node;

            string variableName = MakeUniqueName(key);

            var fieldDeclarationSyntax = RoslynBuilder.DeclareField(
                modelReturnType.Resolve(IterationContext.Stencil),
                variableName, AccessibilityFlags.Public);
            if(attributes.Any())
                fieldDeclarationSyntax = fieldDeclarationSyntax.AddAttributeLists(AttributeList(SeparatedList(attributes.Select(Attribute))));
            m_MemberDeclarations.Add(fieldDeclarationSyntax);

            IdentifierNameSyntax variable = IdentifierName(variableName);
            m_JobInitializers.Add(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                variable,
                value
            ));

            m_Constants.Add(key, variable);

            return variable;
        }

        public override string GetOrDeclareComponentArray(RoslynEcsTranslator.IterationContext ctx, ComponentDefinition componentDefinition, out LocalDeclarationStatementSyntax arrayInitialization, out StatementSyntax arrayDisposal)
        {
            arrayInitialization = null;
            arrayDisposal = null;

            if (m_DeclaredComponentArray.TryGetValue(componentDefinition, out var arrayName))
                return arrayName;

            Type componentType = componentDefinition.TypeHandle.Resolve(ctx.Stencil);
            Type arrayType = typeof(NativeArray<>).MakeGenericType(componentType);
            arrayName = ctx.GetComponentDataArrayName(componentType);
            m_DeclaredComponentArray.Add(componentDefinition, arrayName);

            FieldDeclarationSyntax field = RoslynBuilder.DeclareField(arrayType, arrayName, AccessibilityFlags.Public);
            m_MemberDeclarations.Add(AddAttribute(field, nameof(DeallocateOnJobCompletionAttribute)));

            m_JobInitializers.Add(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(arrayName),
                RootContext.MakeInitComponentDataArrayExpression(ctx, componentType)
            ));

            return ctx.GetComponentDataArrayName(componentType);
        }

        static FieldDeclarationSyntax AddAttribute(FieldDeclarationSyntax field, params string[] attributeNames)
        {
            return field.WithAttributeLists(
                SingletonList(
                    AttributeList(
                        SeparatedList(
                            attributeNames.Select(a => Attribute(IdentifierName(a)))))));
        }

        public override string GetComponentVariableName(IIteratorStackModel groupDeclaration, TypeHandle componentVariableType1)
        {
            Assert.AreEqual(groupDeclaration, IterationContext.Query);
            return IterationContext.GetComponentDataName(componentVariableType1.Resolve(IterationContext.Stencil));
        }

        protected override StatementSyntax GetOrDeclareEntityArray(RoslynEcsTranslator.IterationContext iterationContext, out StatementSyntax arrayDisposal)
        {
            m_MemberDeclarations.Insert(0, AddAttribute(
                RoslynBuilder.DeclareField(
                    typeof(NativeArray<Entity>),
                    iterationContext.EntitiesArrayName,
                    AccessibilityFlags.Public),
                nameof(DeallocateOnJobCompletionAttribute),
                nameof(NativeDisableParallelForRestrictionAttribute)));
            m_JobInitializers.Add(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(iterationContext.EntitiesArrayName),
                ForEachContext.MakeInitEntityArray(iterationContext)));
            arrayDisposal = null;
            return null; // nothing to declare in context
        }

        public override IEntityManipulationTranslator GetEntityManipulationTranslator()
        {
            return m_EntityTranslator ?? (m_EntityTranslator = new JobEntityManipulationTranslator(true));
        }

        public override ExpressionSyntax GetOrDeclareCommandBuffer(bool isConcurrent)
        {
            if (m_CommandBuffers.TryGetValue(isConcurrent, out var syntaxName))
                return syntaxName;

            var commandBufferName = isConcurrent ? "ConcurrentCommandBuffer" : "CommandBuffer";
            var commentBufferType = isConcurrent ? typeof(EntityCommandBuffer.Concurrent) : typeof(EntityCommandBuffer);

            syntaxName = IdentifierName(commandBufferName);
            m_CommandBuffers.Add(isConcurrent, syntaxName);

            var field = RoslynBuilder.DeclareField(
                commentBufferType,
                commandBufferName,
                AccessibilityFlags.Public);
            m_MemberDeclarations.Add(field);

            var assignment = AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(commandBufferName),
                Parent.GetOrDeclareCommandBuffer(isConcurrent));
            m_JobInitializers.Add(assignment);

            return syntaxName;
        }

        public override ExpressionSyntax GetEventBufferWriter(RoslynEcsTranslator.IterationContext iterationContext, ExpressionSyntax entity, Type eventType, out StatementSyntax bufferInitialization)
        {
            GetEventSystem(iterationContext, eventType);
            var bufferVariableName = SendEventTranslator.GetBufferVariableName(iterationContext, eventType);
            var buffersFromEntityVariableName = bufferVariableName + "s";
            var cmdBuffer = GetOrDeclareCommandBuffer(true);
            bufferInitialization = RoslynBuilder.DeclareLocalVariable((Type)null, bufferVariableName, variableDeclarationType: RoslynBuilder.VariableDeclarationType.InferredType, initValue: ConditionalExpression(
                InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(buffersFromEntityVariableName),
                            IdentifierName("Exists")))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(
                                    IdentifierName(EntityName))))),
                ElementAccessExpression(
                        IdentifierName(buffersFromEntityVariableName))
                    .WithArgumentList(
                        BracketedArgumentList(
                            SingletonSeparatedList(
                                Argument(
                                    IdentifierName(EntityName))))),
                InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            cmdBuffer,
                            GenericName(
                                    Identifier(nameof(EntityCommandBuffer.AddBuffer)))
                                .WithTypeArgumentList(
                                    TypeArgumentList(
                                        SingletonSeparatedList<TypeSyntax>(
                                            eventType.ToTypeSyntax())))))
                    .WithArgumentList(
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(
                                new SyntaxNodeOrToken[]{
                                    Argument(
                                        IdentifierName(GetJobIndexParameterName())),
                                    Token(SyntaxKind.CommaToken),
                                    Argument(
                                        IdentifierName(EntityName))})))));
            GetCachedValue(buffersFromEntityVariableName,
                InvocationExpression(
                    GenericName(
                            Identifier(nameof(JobComponentSystem.GetBufferFromEntity)))
                        .WithTypeArgumentList(
                            TypeArgumentList(
                                SingletonSeparatedList(
                                    eventType.ToTypeSyntax())))),
                typeof(BufferFromEntity<>).MakeGenericType(eventType).GenerateTypeHandle(iterationContext.Stencil));

            // The BufferFromEntity<EventStruct> is not parallel-write safe. We might have multiple iterations sending
            // events to the same separate entity, so we run the job on a single thread to be safe
            m_ScheduleSingleThreaded = true;
            return IdentifierName(bufferVariableName);
        }

        protected override StatementSyntax OnPopContext()
        {
            var rootContext = (RootContext)Parent;
            var build = Build();
            if (build != null)
            {
                rootContext.AddMember(build);
                return MakeJobSchedulingStatement();
            }

            return null;
        }

        public override void AddStatement(StatementSyntax node)
        {
            m_UpdateStatements.Add(node);
        }

        public override ExpressionSyntax GetSingletonVariable(IVariableDeclarationModel variable)
        {
            var exp = Parent.GetSingletonVariable(variable);
            var key = $"{RootContext.SingletonComponentTypeName}{variable.VariableName}";
            return GetCachedValue(key, exp, variable.DataType);
        }

        internal override void RequestSingletonUpdate()
        {
            m_UpdateSingletonRequested = true;
        }

        StructDeclarationSyntax Build()
        {
            if (IterationContext.UpdateMode == UpdateMode.OnEnd)
                throw new RoslynEcsTranslator.JobSystemNotCompatibleException("Exit stacks are not compatible with jobs yet");

            // Todo: disabled at the moment - job scheduling needs to use the enter query which uses an excluded component
            if (IterationContext.UpdateMode == UpdateMode.OnStart)
                throw new RoslynEcsTranslator.JobSystemNotCompatibleException("Enter stacks are not compatible with jobs yet");

            if (m_UpdateSingletonRequested)
                throw new RoslynEcsTranslator.JobSystemNotCompatibleException("Setting a blackboard variable is not compatible with jobs yet");

            var stateComponentName = IterationContext.UpdateMode == UpdateMode.OnUpdate || IterationContext.UpdateMode == UpdateMode.OnEvent
                ? null
                : IncludeTrackingSystemStateComponent(IterationContext.Query.ComponentQueryDeclarationModel, IterationContext.UpdateMode == UpdateMode.OnEnd);

            var functionParameters = new List<ParameterSyntax>
            {
                Parameter(Identifier(EntityName)).WithType(TypeSystem.BuildTypeSyntax(typeof(Entity))),
                Parameter(Identifier(GetJobIndexParameterName())).WithType(PredefinedType(Token(SyntaxKind.IntKeyword)))
            };

            var baseTypeSyntax = MakeJobBaseType(IterationContext, functionParameters, out Type eventType);
            if (baseTypeSyntax == null)
                return null;
            StructDeclarationSyntax structDeclaration = StructDeclaration(m_JobName);

            if (BurstCompileJobs)
                structDeclaration = structDeclaration.AddAttributeLists(
                    AttributeList().AddAttributes(
                        Attribute(IdentifierName("BurstCompile"))));

            structDeclaration = DeclareExcludedComponents(structDeclaration);

            var makeJobUpdateOverride = MakeJobUpdateOverride(functionParameters, m_UpdateStatements, stateComponentName, eventType);
            structDeclaration = structDeclaration
                .AddBaseListTypes(baseTypeSyntax)
                .WithMembers(List(m_MemberDeclarations))
                .AddMembers(makeJobUpdateOverride);
            return structDeclaration;

            StructDeclarationSyntax DeclareExcludedComponents(StructDeclarationSyntax declaration)
            {
                if (m_ExcludedComponents.Count == 0)
                    return declaration;

                List<AttributeArgumentSyntax> componentsToExclude = new List<AttributeArgumentSyntax>();
                foreach (var excludedComponent in m_ExcludedComponents)
                {
                    var arg = AttributeArgument(TypeOfExpression(excludedComponent.ToTypeSyntax(IterationContext.Stencil)));
                    componentsToExclude.Add(arg);
                }

                var syntaxNodes = new SyntaxNodeOrToken[componentsToExclude.Count * 2 - 1];
                syntaxNodes[0] = componentsToExclude[0];
                for (var index = 1; index < componentsToExclude.Count; index++)
                {
                    syntaxNodes[index * 2 - 1] = Token(SyntaxKind.CommaToken);
                    syntaxNodes[index * 2] = componentsToExclude[index];
                }

                declaration = declaration.AddAttributeLists(
                    AttributeList().AddAttributes(
                        Attribute(IdentifierName("ExcludeComponent"))
                            .WithArgumentList(
                                AttributeArgumentList(SeparatedList<AttributeArgumentSyntax>(syntaxNodes)))));

                return declaration;
            }
        }

        BaseTypeSyntax MakeJobBaseType(RoslynEcsTranslator.IterationContext iterationContext, List<ParameterSyntax> functionParameters, out Type eventType)
        {
            var genericArguments = new List<TypeSyntax>();

            eventType = null;
            int componentCount = 0;
            int bufferCount = 0;

            if (iterationContext.UpdateMode != UpdateMode.OnEnd)
            {
                if (iterationContext.UpdateMode == UpdateMode.OnEvent) // add DynamicBuffer<eventType>
                {
                    eventType = ((OnEventNodeModel)iterationContext.Query).EventTypeHandle.Resolve(iterationContext.Stencil);
                    var eventTypeSyntax = TypeSystem.BuildTypeSyntax(eventType);
                    var bufferType = GenericName("DynamicBuffer")
                        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(eventTypeSyntax)));
                    var parameterSyntax = Parameter(
                            Identifier(OnEventNodeModel.GetBufferName(eventType)))
                        .WithType(bufferType);
                    AddReadOnlyAttribute(ref parameterSyntax);

                    bufferCount = 1;

                    functionParameters.Add(parameterSyntax);
                    genericArguments.Add(eventTypeSyntax);
                }


                foreach (ComponentDefinition definition in iterationContext.FlattenedComponentDefinitions().Where(def => !def.Subtract))
                {
                    if (!RoslynEcsTranslatorExtensions.ShouldGenerateComponentAccess(definition.TypeHandle, true, out Type resolvedType, iterationContext.Stencil, out bool isShared, out bool isGameObjectComponent) ||
                        isGameObjectComponent)
                        continue;

                    if (isShared)
                    {
                        throw new RoslynEcsTranslator.JobSystemNotCompatibleException("Shared Components are not supported in jobs yet");
                    }

                    genericArguments.Add(TypeSystem.BuildTypeSyntax(resolvedType));
                    var parameterSyntax = Parameter(
                            Identifier(GetComponentVariableName(iterationContext.Query, definition.TypeHandle)))
                        .WithModifiers(TokenList(Token(SyntaxKind.RefKeyword)))
                        .WithType(TypeSystem.BuildTypeSyntax(resolvedType));
                    if (!m_WrittenComponents.Contains(definition.TypeHandle))
                        AddReadOnlyAttribute(ref parameterSyntax);
                    functionParameters.Add(
                        parameterSyntax);
                    componentCount++;
                }

                if (genericArguments.Count == 0)
                    return null;
            }

            string CleanGenericName(string s)
            {
                var index = s.IndexOf('`');
                return index == -1 ? s : s.Substring(0, index);
            }

            // IJobForEachWitEntity_EBBCCC
            string suffix = componentCount > 0 || bufferCount > 0
                ? $"_E{new String('B', bufferCount)}{new String('C', componentCount)}"
                : "";

            return SimpleBaseType(
                GenericName(
                        Identifier(CleanGenericName(typeof(IJobForEachWithEntity<>).Name) + suffix))
                    .WithTypeArgumentList(
                        TypeArgumentList(
                            SeparatedList(
                                genericArguments))));

            ParameterSyntax AddReadOnlyAttribute(ref ParameterSyntax parameterSyntax)
            {
                return parameterSyntax = parameterSyntax.WithAttributeLists(
                    SingletonList(
                        AttributeList(
                            SingletonSeparatedList(
                                Attribute(
                                    IdentifierName(nameof(ReadOnlyAttribute)))))));
            }
        }

        StatementSyntax MakeJobSchedulingStatement()
        {
            var jobCreationExpression = ObjectCreationExpression(
                    IdentifierName(m_JobName))
                .WithArgumentList(ArgumentList())
                .WithInitializer(InitializerExpression(SyntaxKind.ObjectInitializerExpression,
                    SeparatedList(m_JobInitializers)));
            var iterationContextGroupName = IterationContext.UpdateMode == UpdateMode.OnEvent
                ? SendEventTranslator.MakeQueryIncludingEventName(IterationContext, ((OnEventNodeModel)IterationContext.Query).EventTypeHandle.Resolve(IterationContext.Stencil))
                : IterationContext.GroupName;
            return ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(RoslynEcsTranslator.InputDeps),
                        InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(nameof(JobForEachExtensions)),
                                    IdentifierName(m_ScheduleSingleThreaded ? nameof(JobForEachExtensions.ScheduleSingle) : nameof(JobForEachExtensions.Schedule))))
                            .WithArgumentList(
                                ArgumentList(
                                    SeparatedList(
                                        new[]
                                        {
                                            Argument(jobCreationExpression),
                                            Argument(IdentifierName(iterationContextGroupName)),
                                            Argument(IdentifierName(RoslynEcsTranslator.InputDeps))
                                        })))))
                .NormalizeWhitespace();
        }

        MemberDeclarationSyntax MakeJobUpdateOverride(List<ParameterSyntax> functionParameters, List<StatementSyntax> updateStatements, string trackingComponentName, Type eventType)
        {
            var blockSyntax = Block(updateStatements);
            switch (IterationContext.UpdateMode) {
                case UpdateMode.OnStart:
                    blockSyntax = blockSyntax.AddStatements(GetEntityManipulationTranslator().AddComponent(
                        this, IdentifierName(EntityName),
                        DefaultExpression(IdentifierName(trackingComponentName)),
                        IdentifierName(trackingComponentName),
                        false).ToArray());
                    break;
                case UpdateMode.OnEvent:
                    Assert.IsNotNull(eventType);
                    const string eventIndexName = "event_idx";
                    string eventBufferName = OnEventNodeModel.GetBufferName(eventType);
                    var eventDeclaration = RoslynBuilder.DeclareLocalVariable(eventType, "ev" /* TODO hardcoded event name*/,
                        ElementAccessExpression(
                                IdentifierName(eventBufferName))
                            .WithArgumentList(
                                BracketedArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            IdentifierName(eventIndexName))))));
                    blockSyntax = Block(ForStatement(Block(blockSyntax.Statements.Insert(0, eventDeclaration)))
                        .WithDeclaration(
                            VariableDeclaration(
                                    PredefinedType(
                                        Token(SyntaxKind.IntKeyword)))
                                .WithVariables(
                                    SingletonSeparatedList(
                                        VariableDeclarator(
                                                Identifier(eventIndexName))
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    LiteralExpression(
                                                        SyntaxKind.NumericLiteralExpression,
                                                        Literal(0)))))))
                        .WithCondition(
                            BinaryExpression(
                                SyntaxKind.LessThanExpression,
                                IdentifierName(eventIndexName),
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(eventBufferName),
                                    IdentifierName("Length"))))
                        .WithIncrementors(
                            SingletonSeparatedList<ExpressionSyntax>(
                                PostfixUnaryExpression(
                                    SyntaxKind.PostIncrementExpression,
                                    IdentifierName(eventIndexName)))));

                    break;
            }

            var executeMethod = MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier("Execute"))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(
                    ParameterList(
                        SeparatedList(functionParameters)))
                .WithBody(blockSyntax);

            return executeMethod;
        }

        protected override StatementSyntax AddMissingEventBuffers(RoslynEcsTranslator.IterationContext iterationContext, StatementSyntax onPopContext) => onPopContext;
    }
}
