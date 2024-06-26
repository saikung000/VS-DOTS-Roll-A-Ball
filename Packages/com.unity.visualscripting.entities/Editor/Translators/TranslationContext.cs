using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Collections;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine.Assertions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    public abstract class TranslationContext
    {
        public readonly TranslationContext Parent;
        public RoslynEcsTranslator.IterationContext IterationContext { get; protected set; }

        protected string EntityName { get; set; }

        protected TranslationContext(TranslationContext parent)
        {
            Parent = parent;
        }

        public abstract TranslationContext PushContext(IIteratorStackModel query, RoslynEcsTranslator roslynEcsTranslator, UpdateMode mode);
        protected abstract StatementSyntax OnPopContext();
        public abstract void AddStatement(StatementSyntax node);
        public abstract void AddEntityDeclaration(string variableName);

        public void AddCriterionCondition(RoslynEcsTranslator translator, string entityName,
            IEnumerable<CriteriaModel> criteriaModels)
        {
            GetEntityManipulationTranslator().BuildCriteria(
                translator,
                this,
                IdentifierName(entityName),
                criteriaModels);
        }

        public virtual ExpressionSyntax GetSingletonVariable(IVariableDeclarationModel variable)
        {
            return Parent.GetSingletonVariable(variable);
        }

        internal virtual void RequestSingletonUpdate()
        {
            Parent.RequestSingletonUpdate();
        }

        public virtual string GetJobIndexParameterName()
        {
            return Parent.GetJobIndexParameterName();
        }

        public virtual string GetOrDeclareComponentQuery(RoslynEcsTranslator.IterationContext iterationContext)
        {
            return Parent.GetOrDeclareComponentQuery(iterationContext);
        }

        public virtual string GetOrDeclareComponentArray(RoslynEcsTranslator.IterationContext ctx, ComponentDefinition componentDefinition, out LocalDeclarationStatementSyntax arrayInitialization, out StatementSyntax arrayDisposal)
        {
            return Parent.GetOrDeclareComponentArray(ctx, componentDefinition, out arrayInitialization, out arrayDisposal);
        }

        public abstract string GetComponentVariableName(IIteratorStackModel query, TypeHandle componentVariableType1);

        public virtual IEntityManipulationTranslator GetEntityManipulationTranslator()
        {
            return Parent.GetEntityManipulationTranslator();
        }

        public virtual IEnumerable<ComponentDefinition> GetComponentDefinitions()
        {
            return IterationContext.FlattenedComponentDefinitions();
        }

        public virtual ExpressionSyntax GetOrDeclareCommandBuffer(bool isConcurrent)
        {
            return Parent.GetOrDeclareCommandBuffer(isConcurrent);
        }

        public virtual string MakeUniqueName(string groupName) => Parent.MakeUniqueName(groupName);

        public virtual Allocator AllocatorType => Parent.AllocatorType;
        public virtual RoslynEcsTranslator.TranslationOptions TranslationOptions => Parent.TranslationOptions;

        public IEnumerable<StatementSyntax> PopContext()
        {
            Assert.IsNotNull(Parent, "cannot pop root context");
            yield return OnPopContext();
        }

        public abstract void RecordComponentAccess(RoslynEcsTranslator.IterationContext query, TypeHandle componentType, RoslynEcsTranslator.AccessMode mode);

        public virtual ExpressionSyntax GetCachedValue(string key, ExpressionSyntax value, TypeHandle modelReturnType, params IdentifierNameSyntax[] attributes)
        {
            return Parent.GetCachedValue(key, value, modelReturnType);
        }

        protected virtual StatementSyntax GetOrDeclareEntityArray(RoslynEcsTranslator.IterationContext iterationContext, out StatementSyntax arrayDisposal)
        {
            return Parent.GetOrDeclareEntityArray(iterationContext, out arrayDisposal);
        }

        protected virtual string IncludeTrackingSystemStateComponent(ComponentQueryDeclarationModel query, bool trackProcessed)
        {
            return Parent.IncludeTrackingSystemStateComponent(query, trackProcessed);
        }

        public virtual void DeclareComponent<T>(string componentName,
            IEnumerable<MemberDeclarationSyntax> members = null)
        {
            Parent.DeclareComponent<T>(componentName, members);
        }

        public virtual bool GetEventSystem(RoslynEcsTranslator.IterationContext iterationContext, Type eventType)
        {
            return Parent.GetEventSystem(iterationContext, eventType);
        }

        public virtual ExpressionSyntax GetEventBufferWriter(RoslynEcsTranslator.IterationContext iterationContext, ExpressionSyntax entity, Type eventType, out StatementSyntax bufferInitialization)
        {
            return Parent.GetEventBufferWriter(iterationContext, entity, eventType, out bufferInitialization);
        }

        protected virtual StatementSyntax AddMissingEventBuffers(RoslynEcsTranslator.IterationContext iterationContext, StatementSyntax onPopContext) => Parent.AddMissingEventBuffers(iterationContext, onPopContext);
    }
}
