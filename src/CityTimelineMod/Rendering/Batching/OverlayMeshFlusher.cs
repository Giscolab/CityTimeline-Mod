using System;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Railways;
using CityTimelineMod.Rendering.Services;
using UnityEngine;

namespace CityTimelineMod.Rendering.Batching
{
    internal sealed class OverlayOwnedMesh : MonoBehaviour
    {
        private Mesh _mesh;

        internal Mesh Create(string meshName)
        {
            if (_mesh != null)
                throw new InvalidOperationException("An owned overlay mesh is already registered.");

            _mesh = new Mesh();
            _mesh.name = meshName;
            return _mesh;
        }

        internal void DestroyOwnedMesh()
        {
            var mesh = _mesh;

            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);

            _mesh = null;
        }

        private void OnDestroy()
        {
            DestroyOwnedMesh();
        }
    }

    internal static class OverlayMeshFlusher
    {
        private static Mesh CreateOwnedMesh(GameObject owner, string meshName)
        {
            var filter = owner.AddComponent<MeshFilter>();
            var ownership = owner.AddComponent<OverlayOwnedMesh>();
            var mesh = ownership.Create(meshName);
            filter.sharedMesh = mesh;
            return mesh;
        }

        internal static int FlushServicePointBatch(
            Transform parent,
            string batchKey,
            ServicePointMeshBatch batch,
            Action<string> logVerbose
        )
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = "service_batch_" + OverlayObjectNameUtil.SanitizeObjectName(batchKey) + "_" + batch.ChunkIndex;
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, true);

            var mesh = CreateOwnedMesh(obj, name + "_mesh");
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;

            if (logVerbose != null)
            {
                logVerbose(
                    "GroundOverlay: service batch created: " + name +
                    ", points=" + batch.PointCount +
                    ", vertices=" + batch.Vertices.Count +
                    ", triangles=" + (batch.Triangles.Count / 3)
                );
            }

            batch.ChunkIndex++;
            batch.ClearGeometry();
            return 1;
        }

        internal static int FlushRailwayBatch(
            Transform parent,
            string batchKey,
            RailwayMeshBatch batch,
            Action<string> logVerbose
        )
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = "railway_batch_" + OverlayObjectNameUtil.SanitizeObjectName(batchKey) + "_" + batch.ChunkIndex;
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, true);

            var mesh = CreateOwnedMesh(obj, name + "_mesh");
            // A single long tunnel segment can expand into thousands of dash quads.
            // Force 32-bit indices so the mesh remains valid beyond 65,535 vertices.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;

            if (logVerbose != null)
            {
                logVerbose(
                    "GroundOverlay: railway batch created: " + name +
                    ", primitives=" + batch.PrimitiveCount +
                    ", vertices=" + batch.Vertices.Count +
                    ", triangles=" + (batch.Triangles.Count / 3)
                );
            }

            batch.ChunkIndex++;
            batch.PrimitiveCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();
            return 1;
        }

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

            var mesh = CreateOwnedMesh(obj, name + "_mesh");
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();

            // Only textured road batches populate UV0. Legacy solid-color
            // ribbons leave it empty and keep the previous mesh layout.
            if (batch.UV0.Count == batch.Vertices.Count)
            {
                mesh.uv = batch.UV0.ToArray();
            }
            else if (batch.UV0.Count != 0 && logVerbose != null)
            {
                logVerbose(
                    "GroundOverlay: road UV0 mismatch ignored. batch=" + batchKey +
                    ", vertices=" + batch.Vertices.Count +
                    ", uv0=" + batch.UV0.Count
                );
            }

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            // Recalculate tangents if UVs are present (needed for normal maps on textured tertiary roads).
            if (batch.UV0.Count == batch.Vertices.Count)
            {
                mesh.RecalculateTangents();
            }

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;

            logVerbose(
                "GroundOverlay: road batch created: " + name +
                ", segments=" + batch.SegmentCount +
                ", vertices=" + batch.Vertices.Count +
                ", triangles=" + (batch.Triangles.Count / 3) +
                ", uv0=" + batch.UV0.Count
            );

            batch.ChunkIndex++;
            batch.SegmentCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();
            batch.UV0.Clear();

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

            var mesh = CreateOwnedMesh(obj, name + "_mesh");
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;

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

            var mesh = CreateOwnedMesh(obj, name + "_mesh");
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;

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

            var mesh = CreateOwnedMesh(obj, name + "_mesh");
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;

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

            var mesh = CreateOwnedMesh(obj, name + "_mesh");
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;

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