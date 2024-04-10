using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEditor.VisualScripting.Model;
using UnityEngine;

namespace UnityEditor.VisualScripting.Entities.Editor
{
    public class EditableFloatVectorType : IEditableVectorType<float>
    {
        public string UssClassName => "vs-inline-float-editor";

        public IReadOnlyList<string> PropertyNames { get; }

        public IReadOnlyList<string> PropertyLabels { get; }

        ConstantNodeModel m_Model;
        public ScriptableObject Object => m_Model.NodeAssetReference;

        public EditableFloatVectorType(ConstantNodeModel model, string propertyPath, IEnumerable<string> propertyNames)
        {
            m_Model = model;

            PropertyNames = propertyNames.Select(p => propertyPath + "." + p).ToList();
            PropertyLabels = propertyNames.Select(p => p.ToUpper()).ToList();
        }

        public TextValueField<float> MakePreviewField(string propertyLabel)
        {
            return new FloatField(propertyLabel);
        }
    }
}
