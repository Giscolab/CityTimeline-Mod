using System;
using CityTimelineMod.Rendering.Core;
using UnityEngine;

namespace CityTimelineMod.Rendering.Batching
{
    internal static class OverlayMeshFlusher
    {
        internal static int FlushRoadBatch(
            Transform parent,
            string batchKey,
            RoadMeshBatch batch,
            Action<string> logVerbose
        )
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = "road_batch_" + OverlayObjectNameUtil.SanitizeObjectName(batchKey) + "_" + batch.ChunkIndex;

            var obj = new GameObject(name);
            obj.transform.SetParent(parent, true);

            var mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = batch.Material;

            logVerbose(
                "GroundOverlay: road batch created: " + name +
                ", segments=" + batch.SegmentCount +
                ", vertices=" + batch.Vertices.Count +
                ", triangles=" + (batch.Triangles.Count / 3)
            );

            batch.ChunkIndex++;
            batch.SegmentCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();

            return 1;
        }

        internal static int FlushRoadArrowBatch(
            Transform parent,
            RoadArrowBatch batch
        )
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = "road_oneway_arrow_batch_" + batch.ChunkIndex;
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, true);

            var mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = batch.Material;

            batch.ChunkIndex++;
            batch.ArrowCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();
            return 1;
        }

        internal static int FlushZoningBatch(
            Transform parent,
            string materialKey,
            ZoningMeshBatch batch,
            Action<string> logVerbose
        )
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = "zoning_batch_" + OverlayObjectNameUtil.SanitizeObjectName(materialKey) + "_" + batch.ChunkIndex;

            var obj = new GameObject(name);
            obj.transform.SetParent(parent, true);

            var mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = batch.Material;

            logVerbose(
                "GroundOverlay: zoning batch created: " + name +
                ", polygons=" + batch.PolygonCount +
                ", vertices=" + batch.Vertices.Count +
                ", triangles=" + (batch.Triangles.Count / 3)
            );

            batch.ChunkIndex++;
            batch.PolygonCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();

            return 1;
        }

        internal static int FlushWaterAreaFillBatch(
            Transform parent,
            WaterAreaFillMeshBatch batch,
            Action<string> logVerbose
        )
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = "water_area_fill_batch_" + batch.ChunkIndex;

            var obj = new GameObject(name);
            obj.transform.SetParent(parent, true);

            var mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = batch.Material;

            logVerbose(
                "GroundOverlay: water area fill batch created: " + name +
                ", polygons=" + batch.PolygonCount +
                ", vertices=" + batch.Vertices.Count +
                ", triangles=" + (batch.Triangles.Count / 3)
            );

            batch.ChunkIndex++;
            batch.PolygonCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();

            return 1;
        }

        internal static int FlushWaterBatch(
            Transform parent,
            string namePrefix,
            WaterMeshBatch batch,
            Action<string> logVerbose
        )
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = OverlayObjectNameUtil.SanitizeObjectName(namePrefix) + "batch_" + batch.ChunkIndex;

            var obj = new GameObject(name);
            obj.transform.SetParent(parent, true);

            var mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = batch.Material;

            logVerbose(
                "GroundOverlay: water batch created: " + name +
                ", segments=" + batch.SegmentCount +
                ", vertices=" + batch.Vertices.Count +
                ", triangles=" + (batch.Triangles.Count / 3)
            );

            batch.ChunkIndex++;
            batch.SegmentCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();

            return 1;
        }
    }
}
