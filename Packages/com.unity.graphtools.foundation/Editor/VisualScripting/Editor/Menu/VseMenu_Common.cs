using System;
using UnityEditor.UIElements;
using UnityEditor.VisualScripting.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.VisualScripting.Editor
{
    partial class VseMenu
    {
        ToolbarButton m_SaveAllButton;
        ToolbarButton m_BuildAllButton;
        ToolbarButton m_RefreshUIButton;
        ToolbarButton m_ViewInCodeViewerButton;
        ToolbarButton m_ResetBlackboardButton;

        void CreateCommonMenu()
        {
            m_SaveAllButton = this.MandatoryQ<ToolbarButton>("saveAllButton");
            m_SaveAllButton.tooltip = "Save All";
            m_SaveAllButton.ChangeClickEvent(OnSaveAllButton);

            m_BuildAllButton = this.MandatoryQ<ToolbarButton>("buildAllButton");
            m_BuildAllButton.tooltip = "Build All";
            m_BuildAllButton.ChangeClickEvent(OnBuildAllButton);

            m_ResetBlackboardButton = this.MandatoryQ<ToolbarButton>("resetBlackboardButton");
            m_ResetBlackboardButton.tooltip = "Reset Blackboard Position & Size";
            m_ResetBlackboardButton.ChangeClickEvent(OnResetBlackboardButton);

            m_RefreshUIButton = this.MandatoryQ<ToolbarButton>("refreshButton");
            m_RefreshUIButton.tooltip = "Refresh UI";
            m_RefreshUIButton.ChangeClickEvent(() => m_Store.Dispatch(new RefreshUIAction(UpdateFlags.All)));

            m_ViewInCodeViewerButton = this.MandatoryQ<ToolbarButton>("viewInCodeViewerButton");
            m_ViewInCodeViewerButton.tooltip = "Code Viewer";
            m_ViewInCodeViewerButton.ChangeClickEvent(OnViewInCodeViewerButton);
        }

        void OnResetBlackboardButton()
        {
            m_GraphView.UIController.ResetBlackboard();
        }

        protected virtual void UpdateCommonMenu(VSPreferences prefs, bool enabled)
        {
            m_SaveAllButton.SetEnabled(enabled);
            m_BuildAllButton.SetEnabled(enabled);
            m_ViewInCodeViewerButton.SetEnabled(enabled);
        }

        static void OnSaveAllButton()
        {
            AssetDatabase.SaveAssets();
        }

        void OnBuildAllButton()
        {
            try
            {
                m_Store.Dispatch(new BuildAllEditorAction());
            }
            catch (Exception e) // so the button doesn't get stuck
            {
                Debug.LogException(e);
            }
        }

        void OnViewInCodeViewerButton()
        {
            var compilationResult = m_Store.GetState()?.CompilationResultModel?.GetLastResult();
            if (compilationResult == null)
            {
                Debug.LogWarning("Compilation returned empty results");
                return;
            }

            VseUtility.UpdateCodeViewer(show: true, sourceIndex: m_GraphView.window.ToggleCodeViewPhase,
                compilationResult: compilationResult,
                selectionDelegate: lineMetadata =>
                {
                    if (lineMetadata == null)
                        return;

                    int nodeInstanceId = (int)lineMetadata;
                    m_Store.Dispatch(new PanToNodeAction(nodeInstanceId));
                });
        }
    }
}
