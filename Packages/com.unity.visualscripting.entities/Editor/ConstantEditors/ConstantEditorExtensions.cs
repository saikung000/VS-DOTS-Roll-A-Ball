using System;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEditor.UIElements;
using UnityEditor.VisualScripting.Model;
using UnityEngine;
using UnityEngine.UIElements;
using Packages.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.ConstantEditor;

namespace UnityEditor.VisualScripting.Entities.Editor
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    static class ConstantEditorExtensions
    {
        public static VisualElement BuildFloat2Editor(this IConstantEditorBuilder builder, ConstantNodeModel<Vector2, float2> v)
        {
            return builder.BuildVectorEditor(new EditableFloatVectorType(v, "m_NodeModel.value", new[] {"x", "y"}));
        }

        public static VisualElement BuildFloat3Editor(this IConstantEditorBuilder builder, ConstantNodeModel<Vector3, float3> v)
        {
            return builder.BuildVectorEditor(new EditableFloatVectorType(v, "m_NodeModel.value", new []{"x", "y", "z"}));
        }

        public static VisualElement BuildFloat4Editor(this IConstantEditorBuilder builder, ConstantNodeModel<Vector4, float4> v)
        {
            return builder.BuildVectorEditor(new EditableFloatVectorType(v, "m_NodeModel.value", new []{"x", "y", "z", "w"}));
        }

        static VisualElement BuildVectorEditor<T>(this IConstantEditorBuilder builder, IEditableVectorType<T> v)
        {
            var root = new VisualElement();
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "ConstantEditors.uss"));
            root.AddToClassList(v.UssClassName);
            if (v.Object != null)
            {
                var serializedObject = new SerializedObject(v.Object);
                foreach (var propertyName in v.PropertyNames)
                {
                    SerializedProperty p = serializedObject.FindProperty(propertyName);
                    var propertyField = new PropertyField(p);
                    propertyField.RegisterCallback<ChangeEvent<T>>(new EventCallback<IChangeEvent>(builder.OnValueChanged));
                    root.Add(propertyField);
                }
            }
            else
            {
                foreach (var propertyLabel in v.PropertyLabels)
                    root.Add(v.MakePreviewField(propertyLabel));
            }
            return root;
        }
    }
}
