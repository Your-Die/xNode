namespace Values.Lists
{
    public class ObjectListNode : ListNode<object>
    {
        protected override object Copy(object item) => item;
    }
}