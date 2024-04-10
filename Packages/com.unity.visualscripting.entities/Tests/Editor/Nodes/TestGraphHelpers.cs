using System;
using System.Linq;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Nodes
{
    static class TestGraphHelpers
    {
        public static InstantiateNodeModel FindInstantiateNodeModel(this GraphModel graph)
        {
            return graph.NodeModels.OfType<IStackModel>().SelectMany(node => node.NodeModels.OfType<InstantiateNodeModel>()).First();
        }
    }
}
