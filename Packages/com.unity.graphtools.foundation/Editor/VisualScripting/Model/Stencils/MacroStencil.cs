﻿using System;
using System.Collections.Generic;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEditor.VisualScripting.Model.Compilation;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    public class MacroStencil : Stencil
    {
        [SerializeField]
        Stencil m_Parent;

        ISearcherFilterProvider m_MacroSearcherFilterProvider;

        public override StencilCapabilityFlags Capabilities => StencilCapabilityFlags.SupportsMacros;
        public override IBuilder Builder => m_Parent == null ? null : m_Parent.Builder;

        public override void PreProcessGraph(VSGraphModel graphModel)
        {
            m_Parent.PreProcessGraph(graphModel);
        }

        public void SetParent(Type type, VSGraphAssetModel asset)
        {
            Assert.IsTrue(typeof(Stencil).IsAssignableFrom(type));
            m_Parent = (Stencil)CreateInstance(type);
            Utility.SaveAssetIntoObject(m_Parent, asset);
        }

        internal Type ParentType => m_Parent.GetType();

        public override IBlackboardProvider GetBlackboardProvider()
        {
            return m_BlackboardProvider ?? (m_BlackboardProvider = new BlackboardMacroProvider(this));
        }

        public override ISearcherFilterProvider GetSearcherFilterProvider()
        {
            return m_MacroSearcherFilterProvider ?? (m_MacroSearcherFilterProvider = new MacroSearcherFilterProvider(this));
        }

        public override ISearcherDatabaseProvider GetSearcherDatabaseProvider()
        {
            return m_Parent.GetSearcherDatabaseProvider();
        }

        public override List<ITypeMetadata> GetAssembliesTypesMetadata()
        {
            return m_Parent.GetAssembliesTypesMetadata();
        }

        public override ITranslator CreateTranslator()
        {
            return new NoOpTranslator();
        }
    }
}
