using System;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.VisualScripting.Model
{
    [Serializable]
    public class VariableNodeModel : NodeModel, IVariableModel, IRenamableModel, IObjectReference, IExposeTitleProperty
    {
        [SerializeField]
        VariableDeclarationModel m_DeclarationModel;

        public VariableType VariableType => DeclarationModel.VariableType;

        public TypeHandle DataType => DeclarationModel?.DataType ?? TypeHandle.Unknown;

        public override string Title => m_DeclarationModel == null ? "" : m_DeclarationModel.Title;

        public IVariableDeclarationModel DeclarationModel
        {
            get => m_DeclarationModel;
            set => m_DeclarationModel = (VariableDeclarationModel)value;
        }

        public Object ReferencedObject => m_DeclarationModel;
        public string TitlePropertyName => "m_Name";

        const string k_MainPortName = "MainPortName";

        PortModel m_MainPortModel;
        public IPortModel OutputPort => m_MainPortModel;

        public void UpdateTypeFromDeclaration()
        {
            if (DeclarationModel != null)
                m_MainPortModel.DataType = DeclarationModel.DataType;

            // update connected nodes' ports colors/types
            foreach (IPortModel connectedPortModel in m_MainPortModel.ConnectionPortModels)
                connectedPortModel.NodeModel.OnConnection(connectedPortModel, m_MainPortModel);
        }

        protected override void OnDefineNode()
        {
            // used by macro outputs
            if(m_DeclarationModel != null /* this node */ && m_DeclarationModel.Modifiers.HasFlag(ModifierFlags.WriteOnly))
                m_MainPortModel = AddDataInput(null, DataType, k_MainPortName);
            else
                m_MainPortModel = AddDataOutputPort(null, DataType, k_MainPortName);
        }

        public void Rename(string newName)
        {
            ((VariableDeclarationModel)DeclarationModel)?.SetNameFromUserName(newName);
        }

        public override CapabilityFlags Capabilities => base.Capabilities | CapabilityFlags.Renamable;
    }
}
