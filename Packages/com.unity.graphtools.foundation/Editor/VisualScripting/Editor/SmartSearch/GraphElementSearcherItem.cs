using System;
using System.Collections.Generic;
using UnityEditor.Searcher;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEngine;

namespace UnityEditor.VisualScripting.Editor.SmartSearch
{
    public struct StackNodeCreationData
    {
        public readonly IStackModel StackModel;
        public readonly int Index;
        public readonly SpawnFlags SpawnFlags;

        public StackNodeCreationData(IStackModel stackModel, int index,
            SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            StackModel = stackModel;
            Index = index;
            SpawnFlags = spawnFlags;
        }
    }

    public struct GraphNodeCreationData
    {
        public readonly IGraphModel GraphModel;
        public readonly Vector2 Position;
        public readonly SpawnFlags SpawnFlags;

        public GraphNodeCreationData(IGraphModel graphModel, Vector2 position,
            SpawnFlags spawnFlags = SpawnFlags.Default)
        {
            GraphModel = graphModel;
            Position = position;
            SpawnFlags = spawnFlags;
        }
    }

    public class GraphNodeModelSearcherItem : SearcherItem, ISearcherItemDataProvider
    {
        public Func<GraphNodeCreationData, IGraphElementModel[]> CreateElements { get; }
        public ISearcherItemData Data { get; }

        public GraphNodeModelSearcherItem(
            ISearcherItemData data,
            Func<GraphNodeCreationData, IGraphElementModel> createElement,
            string name,
            string help = "",
            List<SearcherItem> children = null
        ) : base(name, help, children)
        {
            Data = data;
            CreateElements = d => new[] { createElement.Invoke(d) };
        }
    }

    public class StackNodeModelSearcherItem : SearcherItem, ISearcherItemDataProvider
    {
        public Func<StackNodeCreationData, IGraphElementModel[]> CreateElements { get; }
        public ISearcherItemData Data { get; }

        public StackNodeModelSearcherItem(
            ISearcherItemData data,
            Func<StackNodeCreationData, IGraphElementModel[]> createElements,
            string name,
            string help = "",
            List<SearcherItem> children = null
        ) : base(name, help, children)
        {
            Data = data;
            CreateElements = createElements;
        }

        public StackNodeModelSearcherItem(
            ISearcherItemData data,
            Func<StackNodeCreationData, IGraphElementModel> createElement,
            string name,
            string help = "",
            List<SearcherItem> children = null
        ) : base(name, help, children)
        {
            Data = data;
            CreateElements = d => new[] { createElement.Invoke(d) };
        }
    }
}
