namespace  Values.Lists
{
    public class FloatListNode : ListNode<float>
    {
        protected override float Copy(float item) => item;
    }
}