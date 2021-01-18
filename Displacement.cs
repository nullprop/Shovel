using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shovel
{
    // Unbaked subdivided displacements are made of 4 meshes which connect in the middle,
    // but have their own vertices. (the middle edges have overlapping verts)
    // Indices start from the outermost corner of each mesh and increase sequentially in rows.
    // The first row goes to the right/clockwise from the initial corner (when seen from above).

    // Subdivision level 2 indices
    // 18---21--24   11---10---9
    // |    |    |   |    |    |
    // |    |    |   |    |    |
    // 19---22--25   14---13---12
    // |    |    |   |    |    |
    // |    |    |   |    |    |
    // 20---23--26   17---16---15

    // 33---34--35   8----5----2
    // |    |    |   |    |    |
    // |    |    |   |    |    |
    // 30---31--32   7----4----1
    // |    |    |   |    |    |
    // |    |    |   |    |    |
    // 27---28--29   6----3----0

    // Subdivision level 3 indices
    // 50--x---x---x---x    x---x---x--26---25
    // |   |   |   |   |    |   |   |   |   |
    // 51--x---x---x---x    x---x---x---x---x
    // |   |   |   |   |    |   |   |   |   |
    // x---x---x---x---x    x---x---x---x---x
    // |   |   |   |   |    |   |   |   |   |
    // x---x---x---x---x    x---x---x---x---x
    // |   |   |   |   |    |   |   |   |   |
    // x---x---x---x---x    x---x---x---x---x

    // x---x---x---x---x   24--19--14---9---4
    // |   |   |   |   |    |   |   |   |   |
    // x---x---x---x---x   23--18--13---8---3
    // |   |   |   |   |    |   |   |   |   |
    // x---x---x---x---x   22--17--12---7---2
    // |   |   |   |   |    |   |   |   |   |
    // x---x---x---x---x   21--16--11---6---1
    // |   |   |   |   |    |   |   |   |   |
    // 75--76--x---x---x   20--15--10---5---0

    // Vertices per mesh in each subdivision level
    // Level 1:                2^2  = 4
    // Level 2:  ((2*2)-1)^2 = 3^2  = 9
    // Level 3:  ((3*2)-1)^2 = 5^2  = 25
    // Level 4:  ((5*2)-1)^2 = 9^2  = 81
    // Level 5:  ((9*2)-1)^2 = 17^2 = 289

    // Total vertices in displacement (including overlaps)
    // Level 1: 16
    // Level 2: 36
    // Level 3: 100
    // Level 4: 324
    // Level 5: 1156

    // Image resolution (overlapping verts count as 1 pixel)
    // Level 1: 3x3
    // Level 2: 5x5
    // Level 3: 9x9
    // Level 4: 17x17
    // Level 5: 33x33

    class Displacement
    {
        public uint SubdivisionLevel;

        public Displacement( uint level )
        {
            SubdivisionLevel = level;
        }

        public uint GetPartVertexCount()
        {
            switch ( SubdivisionLevel )
            {
                case 1:
                    return 4;

                case 2:
                    return 9;

                case 3:
                    return 25;

                case 4:
                    return 81;

                case 5:
                    return 289;

                default:
                    throw new InvalidOperationException();
            }
        }

        public uint GetPartSizeInVertices()
        {
            switch ( SubdivisionLevel )
            {
                case 1:
                    return 2;

                case 2:
                    return 3;

                case 3:
                    return 5;

                case 4:
                    return 9;

                case 5:
                    return 17;

                default:
                    throw new InvalidOperationException();
            }
        }

        public uint GetTotalVertexCount()
        {
            return GetPartVertexCount() * 4;
        }

        public uint GetSizeInPixels()
        {
            switch ( SubdivisionLevel )
            {
                case 1:
                    return 3;

                case 2:
                    return 5;

                case 3:
                    return 9;

                case 4:
                    return 17;

                case 5:
                    return 33;

                default:
                    throw new InvalidOperationException();
            }
        }

        public List<uint> GetPixelIndices( uint x, uint y )
        {
            uint max = GetSizeInPixels() - 1;
            if ( x > max || y > max )
                throw new InvalidOperationException();

            List<uint> parts = GetPixelParts(x, y);
            List<uint> indices = new List<uint>();

            foreach ( var part in parts )
            {
                indices.Add( GetPartPixelIndex( part, x, y ) );
            }

            return indices;
        }

        public uint GetPartPixelIndex( uint part, uint x, uint y )
        {
            // 0 indexed size
            uint partSize = GetSizeInPixels() / 2;
            uint minX, maxX, minY, maxY;
            Tuple<int, int> rowDirection;
            Tuple<int, int> columnDirection;
            Tuple<int, int> indexPosition;

            // Part order
            // 2 - 1
            // |   |
            // 3 - 0
            switch ( part )
            {
                case 3:
                    minX = 0;
                    minY = 0;
                    maxX = partSize;
                    maxY = partSize;
                    // --->
                    // x-->
                    rowDirection = new Tuple<int, int>( 1, 0 );
                    columnDirection = new Tuple<int, int>( 0, 1 );
                    indexPosition = new Tuple<int, int>( 0, 0 );
                    break;

                case 2:
                    minX = 0;
                    minY = partSize;
                    maxX = partSize;
                    maxY = partSize * 2;
                    // x |
                    // | |
                    // v v
                    rowDirection = new Tuple<int, int>( 0, -1 );
                    columnDirection = new Tuple<int, int>( 1, 0 );
                    indexPosition = new Tuple<int, int>( 0, ( int )partSize * 2 );
                    break;

                case 1:
                    minX = partSize;
                    minY = partSize;
                    maxX = partSize * 2;
                    maxY = partSize * 2;
                    // <--x
                    // <---
                    rowDirection = new Tuple<int, int>( -1, 0 );
                    columnDirection = new Tuple<int, int>( 0, -1 );
                    indexPosition = new Tuple<int, int>( ( int )partSize * 2, ( int )partSize * 2 );
                    break;

                case 0:
                    minX = partSize;
                    minY = 0;
                    maxX = partSize * 2;
                    maxY = partSize;
                    // ^ ^
                    // | |
                    // | x
                    rowDirection = new Tuple<int, int>( 0, 1 );
                    columnDirection = new Tuple<int, int>( -1, 0 );
                    indexPosition = new Tuple<int, int>( ( int )partSize * 2, 0 );
                    break;

                default:
                    throw new InvalidOperationException();
            }

            if ( x < minX || y < minY || x > maxX || y > maxY )
                throw new InvalidOperationException();

            uint index = GetPartVertexCount() * part;
            uint maxIndex = index + GetPartVertexCount() - 1;
            uint minIndex = index;

            while (
                indexPosition.Item1 >= minX &&
                indexPosition.Item1 <= maxX &&
                indexPosition.Item2 >= minY &&
                indexPosition.Item2 <= maxY &&
                index <= maxIndex
            )
            {
                if ( indexPosition.Item1 == x && indexPosition.Item2 == y )
                {
                    return index;
                }

                if ( ( index + 1 ) % ( partSize + 1 ) == 0 && index > minIndex )
                {
                    // new column
                    indexPosition = new Tuple<int, int>(
                        indexPosition.Item1 + columnDirection.Item1 - rowDirection.Item1 * ( int )partSize,
                        indexPosition.Item2 + columnDirection.Item2 - rowDirection.Item2 * ( int )partSize
                    );
                }
                else
                {
                    // advance row
                    indexPosition = new Tuple<int, int>(
                        indexPosition.Item1 + rowDirection.Item1,
                        indexPosition.Item2 + rowDirection.Item2
                    );
                }
                index++;
            }

            throw new InvalidOperationException();
        }

        public List<uint> GetPixelParts( uint x, uint y )
        {
            uint max = GetSizeInPixels() - 1;
            if ( x > max || y > max )
                throw new InvalidOperationException();

            // Part order
            // 2 - 1
            // |   |
            // 3 - 0
            List<uint> parts = new List<uint>();

            uint partSize = max / 2;
            if ( x <= partSize && y <= partSize )
            {
                parts.Add( 3 );
            }

            if ( x <= partSize && y >= partSize )
            {
                parts.Add( 2 );
            }

            if ( x >= partSize && y >= partSize )
            {
                parts.Add( 1 );
            }

            if ( x >= partSize && y <= partSize )
            {
                parts.Add( 0 );
            }

            return parts;
        }
    }
}
