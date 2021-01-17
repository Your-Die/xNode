using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Values
{
    public abstract class ListNode<T> : ReferenceNode<List<T>>
    {
        [SerializeField] [Input] private bool copyEachElement;

        [SerializeField, FoldoutGroup("Preview Settings")]
        private string separator = ", ";
        [ShowInInspector, ReadOnly] private string preview;
        
        protected override List<T> Copy(List<T> list)
        {
            var output = this.copyEachElement 
                ? list.Select(this.Copy) 
                : list;
            
            return output.ToList();
        }

        protected abstract T Copy(T item);

        protected override void UpdatePreview(List<T> value)
        {
            this.preview = string.Join(this.separator, value);
        }
    }
}