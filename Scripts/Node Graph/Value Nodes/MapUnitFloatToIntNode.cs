using UnityEngine;
using Values.Conversions;

namespace Values
{
    public class MapUnitFloatToIntNode : ConversionNode<float, int>
    {
        [SerializeField] [Input] private int min;
        [SerializeField] [Input] private int max;
        
        protected override int Convert(float input)
        {
            var difference = this.max - this.min;
            var value      = (int)(difference * input);

            return this.min + value;
        }
    }
}