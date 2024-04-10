using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;

namespace Packages.VisualScripting.Editor.Stencils
{
    static class GraphElementSearcherDatabaseEcsExtensions
    {
        public static GraphElementSearcherDatabase AddOnEventNodes(this GraphElementSearcherDatabase db)
        {
            var types = TypeCache.GetTypesWithAttribute<DotsEventAttribute>();
            foreach (var eventType in types)
            {
                var path = "Events";
                var type = typeof(OnEventNodeModel);
                var category = eventType.GetCustomAttribute<DotsEventAttribute>().Category;
                if (category != null)
                    path += $"/{category}";

                {
                    string name = "On " + eventType.Name;
                    db.Items.AddAtPath(new GraphNodeModelSearcherItem(
                        new NodeSearcherItemData(type),
                        data =>
                        {
                            var vsGraphModel = (VSGraphModel)data.GraphModel;
                            return vsGraphModel.CreateNode<OnEventNodeModel>(name, data.Position, data.SpawnFlags, n => n.EventTypeHandle = eventType.GenerateTypeHandle(db.Stencil));
                        },
                        name
                    ), path);
                }
                {
                    string name = "Send " + eventType.Name;
                    db.Items.AddAtPath(new StackNodeModelSearcherItem(
                        new NodeSearcherItemData(type),
                        data =>
                        {
                            var stackModel = ((StackBaseModel)data.StackModel);
                            return stackModel.CreateStackedNode<SendEventNodeModel>(name, data.Index, data.SpawnFlags, n => n.EventType = eventType.GenerateTypeHandle(db.Stencil));
                        },
                        name
                    ), path);
                }
            }

            return db;
        }
    }
}
