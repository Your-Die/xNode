namespace Values.Conversions
{
    public class IntToFloatNode : ConversionNode<int, float>
    {
        protected override float Convert(int input) => input;
    }
}