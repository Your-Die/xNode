using UnityEngine;

namespace Values.Conversions
{
    public class FloorFloatToIntNode : ConversionNode<float, int>
    {
        protected override int Convert(float input) => Mathf.FloorToInt(input);
    }
}