using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScripting.SmartSearch
{
    public class EcsSearcherFilter : SearcherFilter
    {
        public EcsSearcherFilter(SearcherContext context)
            : base(context) { }

        public EcsSearcherFilter WithComponentData(Stencil stencil)
        {
            this.RegisterType(data => typeof(IComponentData).IsAssignableFrom(data.Type.Resolve(stencil)));
            return this;
        }

        public EcsSearcherFilter WithComponentData(Stencil stencil, HashSet<TypeHandle> excluded)
        {
            if(excluded == null)
                throw new ArgumentException();

            this.RegisterType(data => typeof(IComponentData).IsAssignableFrom(data.Type.Resolve(stencil)) && !excluded.Contains(data.Type));
            return this;
        }

        public EcsSearcherFilter WithGameObjectComponents(Stencil stencil)
        {
            this.RegisterType(data => typeof(Component).IsAssignableFrom(data.Type.Resolve(stencil)));
            return this;
        }

        public EcsSearcherFilter WithSharedComponentData(Stencil stencil)
        {
            this.RegisterType(data => typeof(ISharedComponentData).IsAssignableFrom(data.Type.Resolve(stencil)));
            return this;
        }

        public EcsSearcherFilter WithComponents(IEnumerable<TypeHandle> types)
        {
            this.RegisterType(data => types.Contains(data.Type));
            return this;
        }
    }
}
