using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.NodeAssets;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.VisualScripting.Model
{
    [Serializable]
    public class FunctionRefCallNodeModel : NodeModel, IObjectReference, IExposeTitleProperty, IHasInstancePort,
        IFunctionCallModel
    {
        [SerializeField]
        FunctionAsset m_FunctionAsset;

        List<string> m_LastParametersAdded;

        public override string Title
        {
            get
            {
                if (Function)
                {
                    return (Function.GraphModel != GraphModel ? Function.GraphModel.Name + "." : string.Empty) +
                        Function.Title;
                }
                return $"<{base.Title}>";
            }
        }

        public FunctionModel Function
        {
            get => m_FunctionAsset.Node;
            set => m_FunctionAsset = (FunctionAsset)value.NodeAssetReference;
        }

        public Object ReferencedObject => m_FunctionAsset;
        public string TitlePropertyName => "m_Name";
        public IPortModel InstancePort { get; private set; }
        public IPortModel OutputPort { get; private set; }
        public IEnumerable<string> ParametersNames => m_LastParametersAdded;

        public IPortModel GetPortForParameter(string parameterName)
        {
            return InputsById.TryGetValue(parameterName, out var portModel) ? portModel : null;
        }

        protected override void OnDefineNode()
        {
            if (!m_FunctionAsset)
                return;

            InstancePort = Function.IsInstanceMethod
                ? AddInstanceInput(((VSGraphModel)Function.GraphModel).GenerateTypeHandle(Stencil))
                : null;

            m_LastParametersAdded = new List<string>(Function.FunctionParameterModels.Count());
            foreach (var parameter in Function.FunctionParameterModels)
            {
                AddDataInput(parameter.Name, parameter.DataType);
                m_LastParametersAdded.Add(parameter.Name);
            }

            var voidType = typeof(void).GenerateTypeHandle(Stencil);
            OutputPort = Function.ReturnType != voidType
                ? AddDataOutputPort("result", Function.ReturnType)
                : null;
        }
    }
}
