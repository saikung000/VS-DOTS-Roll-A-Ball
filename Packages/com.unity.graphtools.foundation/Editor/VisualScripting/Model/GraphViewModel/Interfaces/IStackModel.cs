using System;
using System.Collections.Generic;
using UnityEditor.VisualScripting.Model;
using UnityEngine;

namespace UnityEditor.VisualScripting.GraphViewModel
{
    public interface IStackModel : INodeModel
    {
        IReadOnlyList<INodeModel> NodeModels { get; }
        IFunctionModel OwningFunctionModel { get; }
        bool AcceptNode(Type nodeType);
        bool DelegatesOutputsToNode(out INodeModel del);
        void CleanUp();
        IReadOnlyList<IPortModel> InputPorts { get; }
        IReadOnlyList<IPortModel> OutputPorts { get; }
    }
}
