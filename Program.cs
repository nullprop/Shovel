﻿using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Numerics;
using Datamodel;
using DM = Datamodel.Datamodel;

namespace Shovel
{
    class Program
    {
        static int Main( string[] args )
        {
            if ( args.Length != 3 || !float.TryParse( args[1], out float maxHeight ) )
            {
                return Usage();
            }
            string imageFile = args[0];
            string outFile = args[2];

            var bitmap = new Bitmap(imageFile);

            FileStream MapFile = File.Open( @"data/base.vmap", FileMode.Open );
            var dm = DM.Load(MapFile);

            var world = dm.AllElements.Single(e => e.ClassName == "CMapWorld");
            var mapMeshes = world.Get<ElementArray>("children");
            var baseMesh = mapMeshes[0];
            var baseMeshData = baseMesh.Get<Element>("meshData");

            int meshSize;
            {
                var vertexData = baseMeshData.Get<Element>("vertexData");
                var streams = vertexData.Get<ElementArray>("streams");
                var position = streams[0];
                var data = position.Get<Vector3Array>("data");
                meshSize = ( int )( data[2].X - data[0].X );
            }
            var sizePixels = GetSizeInPixels(baseMeshData);
            var pixelUnits = meshSize / sizePixels;

            var imageData = new float[bitmap.Width, bitmap.Height];
            for ( var x = 0; x < bitmap.Width; x++ )
            {
                for ( var y = 0; y < bitmap.Height; y++ )
                {
                    imageData[x, y] = bitmap.GetPixel( x, y ).GetBrightness() * Convert.MetersToUnits( maxHeight );
                }
            }
            imageData = Pad( imageData, sizePixels );

            var tilingX = 1 + (imageData.GetLength(0) - sizePixels) / (sizePixels - 1);
            var tilingY = 1 + (imageData.GetLength(1) - sizePixels) / (sizePixels - 1);

            var terrainSizeX = tilingX * meshSize;
            var terrainSizeY = tilingY * meshSize;

            for ( uint x = 0; x < tilingX; x++ )
            {
                var offsetX = x * (sizePixels - 1);
                for ( uint y = 0; y < tilingY; y++ )
                {
                    var offsetY = y * (sizePixels - 1);

                    Element mesh;
                    if ( x == 0 && y == 0 )
                    {
                        mesh = baseMesh;
                    }
                    else
                    {
                        mesh = dm.ImportElement( baseMesh, DM.ImportRecursionMode.Recursive, DM.ImportOverwriteMode.Copy );
                        mapMeshes.Add( mesh );
                    }

                    // Displacement
                    var meshData = mesh.Get<Element>("meshData");
                    WriteTile( meshData, imageData, offsetX, offsetY );

                    // Offset vertex positions and center terrain to grid
                    var vertexData = meshData.Get<Element>("vertexData");
                    var streams = vertexData.Get<ElementArray>("streams");
                    var position = streams[0];
                    var data = position.Get<Vector3Array>("data");
                    for ( var i = 0; i < data.Count; i++ )
                    {
                        Vector3 corner = new Vector3();
                        if ( i == 1 )
                        {
                            corner.X = meshSize;
                        }
                        else if ( i == 2 )
                        {
                            corner.X = meshSize;
                            corner.Y = meshSize;
                        }
                        else if ( i == 3 )
                        {
                            corner.Y = meshSize;
                        }

                        Vector3 vec = new Vector3(meshSize * x, meshSize * y, 0);
                        vec -= new Vector3( terrainSizeX * 0.5f, terrainSizeY * 0.5f, 0 );
                        vec += corner;
                        data[i] = vec;
                    }

                    // Offset origin
                    // FIXME: this doesn't get applied for some reason
                    var origin = mesh.Get<Vector3>("origin");
                    origin = ( data[0] + data[2] ) / 2;
                }
            }

            dm.Save( @$"{outFile}", "binary", 9 );

            dm.Dispose();
            MapFile.Dispose();

            return 0;
        }

        static int Usage()
        {
            Console.WriteLine( "Invalid args" );
            Console.WriteLine( "Usage: Shovel.exe <heightmap>.(BMP|GIF|EXIF|JPG|PNG|TIFF) <max_height (meters)> <map_out>.vmap" );
            return 1;
        }

        static uint GetSizeInPixels( Element meshData )
        {
            var subdivisionData = meshData.Get<Element>("subdivisionData");

            uint subdivisionLevel = 0;
            var levels = subdivisionData.Get<IntArray>("subdivisionLevels");
            for ( var i = 0; i < levels.Count; i++ )
            {
                if ( levels[i] > 0 )
                {
                    subdivisionLevel = ( uint )levels[i];
                    break;
                }
            }

            var disp = new Displacement(subdivisionLevel);
            return disp.GetSizeInPixels();
        }

        static void WriteTile( Element meshData, float[,] height, uint offsetX, uint offsetY )
        {
            var subdivisionData = meshData.Get<Element>("subdivisionData");

            uint subdivisionLevel = 0;
            var levels = subdivisionData.Get<IntArray>("subdivisionLevels");
            for ( var i = 0; i < levels.Count; i++ )
            {
                if ( levels[i] > 0 )
                {
                    subdivisionLevel = ( uint )levels[i];
                    break;
                }
            }

            var streams = subdivisionData.Get<ElementArray>("streams");
            var displacement = streams[1];
            var disp = new Displacement(subdivisionLevel);

            WriteHeight( displacement, disp, height, offsetX, offsetY );
        }

        static void WriteHeight( Element displacement, Displacement disp, float[,] height, uint offsetX, uint offsetY )
        {
            var size = disp.GetSizeInPixels();
            if ( height.GetLength( 0 ) < size + offsetX )
                throw new InvalidOperationException();
            if ( height.GetLength( 1 ) < size + offsetY )
                throw new InvalidOperationException();

            var data = displacement.Get<Vector3Array>("data");

            for ( var x = 0; x < size; x++ )
            {
                for ( var y = 0; y < size; y++ )
                {
                    var indices = disp.GetPixelIndices((uint)x, (uint)y);
                    foreach ( var index in indices )
                    {
                        Vector3 v = new Vector3(0, 0, height[x + offsetX, y + offsetY]);
                        if ( index < data.Count )
                        {
                            data[( int )index] = v;
                        }
                        else
                        {
                            data.Add( v );
                        }
                    }
                }
            }
        }

        static float[,] Pad( float[,] imageData, uint tileSize )
        {
            var minSize = tileSize;
            var increment = tileSize - 1;

            var sizeX = minSize;
            var sizeY = minSize;

            while ( sizeX < imageData.GetLength( 0 ) )
            {
                sizeX += increment;
            }

            while ( sizeY < imageData.GetLength( 1 ) )
            {
                sizeY += increment;
            }

            float[,] newArr = new float[sizeX, sizeY];

            for ( int x = 0; x < sizeX; x++ )
            {
                for ( int y = 0; y < sizeY; y++ )
                {
                    if ( x < imageData.GetLength( 0 ) && y < imageData.GetLength( 1 ) )
                    {
                        newArr[x, y] = imageData[x, y];
                    }
                    else
                    {
                        newArr[x, y] = 0;
                    }
                }
            }

            return newArr;
        }
    }
}
