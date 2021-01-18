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
            Console.WriteLine( "Loading file...\n" );
            FileStream MapFile = File.Open( @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\tools\steamvr_environments\content\steamtours_addons\copsandrobbers\maps\displacemen_test.vmap", FileMode.Open );
            var dm = DM.Load(MapFile);
            FileStream OutFile = File.Open( @"output.txt", FileMode.Create );
            StreamWriter OutStream = new StreamWriter(OutFile);

            dm.AllElements.ToList().ForEach( e =>
            {
                if ( e.ClassName == "CDmePolygonMeshSubdivisionData" )
                {
                    var levels = e.Get<IntArray>("subdivisionLevels");
                    for ( var i = 0; i < levels.Count; i++ )
                    {
                        Console.WriteLine( levels[i] );
                        if ( levels[i] == 1 )
                            levels[i] = 3;
                    }
                }

                if ( e.Name == "displacement:0" )
                {
                    var data = e.Get<Vector3Array>("data");

                    // flatten
                    for ( var i = 0; i < data.Count; i++ )
                    {
                        Vector3 vec = data[i];
                        vec.Z = 0.0f;
                        data[i] = vec;
                    }

                    var disp = new Displacement(2);
                    var heightData = new float[5, 5]{
                        { 0, 0, 1, 0, 0 },
                        { 0, 0, 1, 0, 0 },
                        { 1, 1, 1, 1, 1 },
                        { 0, 0, 1, 0, 0 },
                        { 0, 0, 1, 0, 0 }
                    };
                    for ( var x = 0; x < 5; x++ )
                    {
                        for ( var y = 0; y < 5; y++ )
                        {
                            var height = heightData[4 - y, x] * 64.0f;
                            var indices = disp.GetPixelIndices((uint)x, (uint)y);
                            foreach ( var index in indices )
                            {
                                Vector3 v = data[(int)index];
                                v.Z = height;
                                data[( int )index] = v;
                            }
                        }
                    }
                }

                if ( e.Name == "texcoord:0" )
                {

                }

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

            dm.Save( @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\tools\steamvr_environments\content\steamtours_addons\copsandrobbers\maps\displacemen_test2.vmap", "binary", 9 );
            dm.Dispose();
            MapFile.Dispose();
            OutStream.Dispose();
        }
    }
}
