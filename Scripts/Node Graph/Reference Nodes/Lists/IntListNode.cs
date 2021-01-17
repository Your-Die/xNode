namespace Values.Lists
{
    public class IntListNode : ListNode<int>
    {
        protected override int Copy(int item) => item;
    }
}