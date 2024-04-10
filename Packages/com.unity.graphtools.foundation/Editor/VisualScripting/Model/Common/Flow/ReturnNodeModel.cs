using System;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;

namespace UnityEditor.VisualScripting.Model
{
    [SearcherItem(typeof(ClassStencil), SearcherContext.Stack, "Control Flow/Return")]
    [BranchedNode]
    [Serializable]
    public class ReturnNodeModel : NodeModel, IHasMainInputPort
    {
        const string k_Title = "Return";

        public override string Title => k_Title;

        PortModel m_InputPort;
        public IPortModel InputPort => m_InputPort;

        protected override void OnDefineNode()
        {
            m_InputPort = AddDataInput<Unknown>("value");
        }

        public override void OnConnection(IPortModel selfConnectedPortModel, IPortModel otherConnectedPortModel)
        {
            base.OnConnection(selfConnectedPortModel, otherConnectedPortModel);
            var returnType = ParentStackModel?.OwningFunctionModel?.ReturnType ?? TypeHandle.Unknown;
            m_InputPort.DataType = returnType;
        }
    }
}
