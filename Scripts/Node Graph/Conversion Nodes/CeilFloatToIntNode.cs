using UnityEngine;

namespace Values.Conversions
{
    public class CeilFloatToIntNode : ConversionNode<float, int>
    {
        protected override int Convert(float input) => Mathf.CeilToInt(input);
    }
}