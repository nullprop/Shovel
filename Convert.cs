using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shovel
{
    class Convert
    {
        public static float MeterUnits = 48.0f;

        public static float MetersToUnits( float meters )
        {
            return meters * MeterUnits;
        }

        public static float UnitsToMeters( float units )
        {
            return units / MeterUnits;
        }
    }
}
