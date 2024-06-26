using JetBrains.Annotations;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Model.Stencils;

namespace Packages.VisualScripting.Editor.Stencils
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    static class EcsGraphElementFactoryExtensions
    {
        public static GraphElement CreateOrderedStack(this INodeBuilder builder, Store store, IOrderedStack model)
        {
            var functionNode = new IteratorStackNode(store, (IPrivateIteratorStackModel)model, builder);
            return functionNode;
        }

        public static GraphElement CreateIteratorStack(this INodeBuilder builder, Store store, IIteratorStackModel model)
        {
            var iteratorStackNode = new IteratorStackNode(store, model, builder);
            return iteratorStackNode;
        }
		
        public static GraphElement CreateIteratorStack(this INodeBuilder builder, Store store, OnEntitiesEventBaseNodeModel model)
        {
            var iteratorStackNode = new IteratorStackNode(store, model, builder);
            return iteratorStackNode;
        }

        public static GraphElement CreateInstantiateNode(this INodeBuilder builder, Store store, InstantiateNodeModel model)
        {
            var functionNode = new InstantiateNode(model, store, builder.GraphView);
            return functionNode;
        }
    }
}
