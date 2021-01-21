using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Datamodel;
using DM = Datamodel.Datamodel;

namespace Shovel
{
    class Program
    {
        static void Main( string[] args )
        {
            FileStream MapFile = File.Open( @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\tools\steamvr_environments\content\steamtours_addons\copsandrobbers\maps\displacemen_test.vmap", FileMode.Open );
            var dm = DM.Load(MapFile);

            var world = dm.AllElements.Single(e => e.ClassName == "CMapWorld");
            var mapMeshes = world.Get<ElementArray>("children");
            var baseMesh = mapMeshes[0];
            var baseMeshData = baseMesh.Get<Element>("meshData");

            var pixelSize = GetPixelSize(baseMeshData);
            uint tilingX = 2;
            uint tilingY = 4;
            var noise = GetNoise((pixelSize - 1) * (Math.Max(tilingX, tilingY) + 1) + 1);

            for ( uint x = 0; x < tilingX; x++ )
            {
                var offsetX = x * (pixelSize - 1);
                for ( uint y = 0; y < tilingY; y++ )
                {
                    var offsetY = y * (pixelSize - 1);

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
                    WriteTile( meshData, noise, offsetX, offsetY );

                    // Offset origin
                    // FIXME: this doesn't get applied for some reason
                    var origin = mesh.Get<Vector3>("origin");
                    origin = new Vector3( 1024 * x, 1024 * y, 0 );

                    // Offset vertex positions
                    var vertexData = meshData.Get<Element>("vertexData");
                    var streams = vertexData.Get<ElementArray>("streams");
                    var position = streams[0];
                    var data = position.Get<Vector3Array>("data");
                    for ( var i = 0; i < data.Count; i++ )
                    {
                        Vector3 vec = data[i] + new Vector3(1024 * x, 1024 * y, 0);
                        data[i] = vec;
                    }
                }
            }

            Dump( dm, @"output.txt" );
            dm.Save( @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\tools\steamvr_environments\content\steamtours_addons\copsandrobbers\maps\displacement_test2.vmap", "binary", 9 );

            dm.Dispose();
            MapFile.Dispose();
        }

        static uint GetPixelSize( Element meshData )
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

            var disp = new Displacement(subdivisionLevel);
            var pixelSize = disp.GetSizeInPixels();

            var streams = subdivisionData.Get<ElementArray>("streams");
            var texCoord = streams[0];
            var displacement = streams[1];

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

        static float[,] GetNoise( uint size )
        {
            var noise = new float[size, size];
            var rand = new Random();

            for ( var x = 0; x < size; x++ )
            {
                for ( var y = 0; y < size; y++ )
                {
                    var height = (float)rand.NextDouble() * 64.0f;
                    noise[x, y] = height;
                }
            }

            return noise;
        }

        static void Dump( DM dm, string path )
        {
            FileStream OutFile = File.Open( path, FileMode.Create );
            StreamWriter OutStream = new StreamWriter(OutFile);

            dm.AllElements.ToList().ForEach( e =>
            {
                OutStream.WriteLine( "Object:  " );
                OutStream.WriteLine( $"  Name: {e.Name}" );
                OutStream.WriteLine( $"  ClassName: {e.ClassName}" );
                OutStream.WriteLine( $"  ID: {e.ID}" );
                OutStream.WriteLine( $"  Owner: {e.Owner}" );
                OutStream.WriteLine( $"  Stub: {e.Stub}" );
                OutStream.WriteLine( $"  Keys/Values:" );
                for ( var i = 0; i < e.Keys.Count; i++ )
                {
                    var val = e.Values.ElementAtOrDefault( i );
                    var key = e.Keys.ElementAtOrDefault( i );
                    OutStream.WriteLine( $"    {key} = {val}" );

                    if ( val == null )
                        continue;

                    if ( key == "asset_preview_thumbnail" )
                        continue;

                    if ( val is System.Collections.IList list )
                    {
                        if ( list.Count == 0 )
                        {
                            OutStream.WriteLine( $"      [empty]" );
                        }

                        foreach ( var el in list )
                        {
                            OutStream.WriteLine( $"      {el}" );
                        }
                    }
                }
                OutStream.WriteLine( "" );
            } );

            OutStream.Dispose();
            OutFile.Dispose();
        }
    }
}
