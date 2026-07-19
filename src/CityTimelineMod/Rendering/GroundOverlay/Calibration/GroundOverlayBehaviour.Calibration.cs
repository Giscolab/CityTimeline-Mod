using System.Collections.Generic;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private void HandleCalibrationInput()
        {
            // Les raccourcis clavier de calibration sont désactivés.
            // Le calage doit passer par le panneau HUD interactif, avec déverrouillage explicite.
            return;
        }

        private void CycleCalibrationMode()
        {
            if (_activeCalibrationMode == CalibrationMode.Render)
                _activeCalibrationMode = CalibrationMode.Global;
            else
                _activeCalibrationMode = (CalibrationMode)((int)_activeCalibrationMode + 1);

            Log.Info("GroundOverlay calibration: active mode=" + GetCalibrationModeLabel(_activeCalibrationMode));
        }

        private void ApplyLiveCalibrationTransform()
        {
            transform.position = new Vector3(
                _liveCalibrationOffset.x,
                0f,
                _liveCalibrationOffset.z
            );

            transform.rotation = Quaternion.Euler(
                0f,
                _liveCalibrationRotationDegrees,
                0f
            );

            transform.localScale = new Vector3(
                _liveCalibrationScaleX,
                1f,
                _liveCalibrationScaleZ
            );
        }

        private void LogLiveCalibrationState()
        {
            Log.Info(
                "GroundOverlay calibration LIVE: " +
                "mode=" + GetCalibrationModeLabel(_activeCalibrationMode) +
                ", offsetX=" + _liveCalibrationOffset.x +
                ", offsetZ=" + _liveCalibrationOffset.z +
                ", rotationDelta=" + _liveCalibrationRotationDegrees +
                ", scaleXDelta=" + _liveCalibrationScaleX +
                ", scaleZDelta=" + _liveCalibrationScaleZ
            );
        }

        private void LogCalibrationState()
        {
            Log.Info(
                "GroundOverlay calibration STATE: " +
                "mode=" + GetCalibrationModeLabel(_activeCalibrationMode) +
                ", offsetX=" + _liveCalibrationOffset.x +
                ", offsetZ=" + _liveCalibrationOffset.z +
                ", rotationDelta=" + _liveCalibrationRotationDegrees +
                ", scaleXDelta=" + _liveCalibrationScaleX +
                ", scaleZDelta=" + _liveCalibrationScaleZ +
                ", worldOriginX=" + _config.WorldOriginX +
                ", worldOriginZ=" + _config.WorldOriginZ +
                ", worldScale=" + _config.WorldScale +
                ", overlayRotationDegrees=" + _config.OverlayRotationDegrees +
                ", overlayScaleX=" + _config.OverlayScaleX +
                ", overlayScaleZ=" + _config.OverlayScaleZ +
                ", pointStride=" + _config.PointStride +
                ", waterWidth=" + _config.SegmentWidth +
                ", waterHeight=" + _config.SegmentHeight +
                ", roadWidth=" + _config.RoadSegmentWidth +
                ", roadHeight=" + _config.RoadSegmentHeight +
                ", waterLines=" + (_waterLines != null ? _waterLines.Count : 0) +
                ", roadLines=" + (_roadLines != null ? _roadLines.Count : 0)
            );
        }

        private static string GetCalibrationModeLabel(CalibrationMode mode)
        {
            switch (mode)
            {
                case CalibrationMode.Global:
                    return "Global";
                case CalibrationMode.Water:
                    return "Eau";
                case CalibrationMode.Roads:
                    return "Routes";
                case CalibrationMode.Render:
                    return "Rendu";
                default:
                    return mode.ToString();
            }
        }

        private void SaveAndBakeLiveCalibration()
        {
            if (_config == null)
                return;

            _config.WorldOriginX += _liveCalibrationOffset.x;
            _config.WorldOriginZ += _liveCalibrationOffset.z;

            _config.OverlayRotationDegrees += _liveCalibrationRotationDegrees;
            _config.OverlayScaleX *= _liveCalibrationScaleX;
            _config.OverlayScaleZ *= _liveCalibrationScaleZ;

            _config.SaveCalibrationToConfig();

            Log.Info(
                "GroundOverlay calibration SAVED: " +
                "worldOriginX=" + _config.WorldOriginX +
                ", worldOriginZ=" + _config.WorldOriginZ +
                ", overlayRotationDegrees=" + _config.OverlayRotationDegrees +
                ", overlayScaleX=" + _config.OverlayScaleX +
                ", overlayScaleZ=" + _config.OverlayScaleZ
            );

            _liveCalibrationOffset = Vector3.zero;
            _liveCalibrationRotationDegrees = 0f;
            _liveCalibrationScaleX = 1f;
            _liveCalibrationScaleZ = 1f;

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (_config == null || !_config.EnableProgressiveOverlayRebuild)
                ClearOverlayChildren();

            CreateOverlayWithTiming("calibration-save");
        }

        private void ClearOverlayChildren()
        {
            var children = new List<GameObject>();

            for (var i = 0; i < transform.childCount; i++)
                children.Add(transform.GetChild(i).gameObject);

            foreach (var child in children)
            {
                // Meshes created by OverlayMeshFlusher are native Unity objects;
                // destroying only the GameObject does not release them promptly.
                // Built-in primitive meshes (for example "Cube") are shared and
                // deliberately excluded by the generated "_mesh" suffix.
                var filters = child.GetComponentsInChildren<MeshFilter>(true);
                foreach (var filter in filters)
                {
                    var mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null || string.IsNullOrEmpty(mesh.name) || !mesh.name.EndsWith("_mesh"))
                        continue;

                    filter.sharedMesh = null;
                    UnityEngine.Object.Destroy(mesh);
                }

                UnityEngine.Object.Destroy(child);
            }

            Log.Info("GroundOverlay calibration: cleared children=" + children.Count);
        }

        private bool CreateOrientedWaterCube(
            string name,
            Vector3 a,
            Vector3 b,
            Material material,
            float waterWidth,
            float waterHeight
        )
        {
            var mid = (a + b) * 0.5f;

            var delta = b - a;
            delta.y = 0f;

            var length = delta.magnitude;

            if (length < 0.1f)
                return false;

            var width = Mathf.Max(0.1f, waterWidth);
            var height = Mathf.Max(0.1f, waterHeight);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);

            cube.transform.position = new Vector3(
                mid.x,
                mid.y + height * 0.5f,
                mid.z
            );

            cube.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            cube.transform.localScale = new Vector3(
                width,
                height,
                length + width * 0.35f
            );

            OverlayPrimitiveUtil.RemoveCollider(cube);
            OverlayPrimitiveUtil.ApplyMaterial(cube, material);

            if (_debugWaterSegmentLogs < 10)
            {
                _debugWaterSegmentLogs++;
                Log.Info(
                    "GroundOverlay: CREATED oriented water cube " +
                    name +
                    " pos=" + cube.transform.position +
                    " scale=" + cube.transform.localScale +
                    " length=" + length
                );
            }

            return true;
        }

        private bool CreateWaterDebugCube(
            string name,
            Vector3 a,
            Vector3 b,
            Material material
        )
        {
            var mid = (a + b) * 0.5f;
            var delta = b - a;

            var sizeX = Mathf.Abs(delta.x) + _config.SegmentWidth;
            var sizeZ = Mathf.Abs(delta.z) + _config.SegmentWidth;

            sizeX = Mathf.Max(sizeX, _config.SegmentWidth);
            sizeZ = Mathf.Max(sizeZ, _config.SegmentWidth);

            var height = _config.SegmentHeight;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);

            cube.transform.position = new Vector3(
                mid.x,
                mid.y + height * 0.5f,
                mid.z
            );

            cube.transform.rotation = Quaternion.identity;
            cube.transform.localScale = new Vector3(sizeX, height, sizeZ);

            OverlayPrimitiveUtil.RemoveCollider(cube);
            OverlayPrimitiveUtil.ApplyMaterial(cube, material);

            if (_debugWaterSegmentLogs < 5)
            {
                _debugWaterSegmentLogs++;
                Log.Info(
                    "GroundOverlay: CREATED water debug cube " +
                    name +
                    " pos=" + cube.transform.position +
                    " scale=" + cube.transform.localScale
                );
            }

            return true;
        }

        private bool CreateOrientedRoadCube(
            string name,
            Vector3 a,
            Vector3 b,
            Material material,
            float roadWidth,
            float roadHeight
        )
        {
            var mid = (a + b) * 0.5f;

            var delta = b - a;
            delta.y = 0f;

            var length = delta.magnitude;

            if (length < 0.1f)
                return false;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);

            cube.transform.position = new Vector3(
                mid.x,
                mid.y + roadHeight * 0.5f,
                mid.z
            );

            cube.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            // Local X = largeur de route en mètres CS2
            // Local Y = hauteur debug
            // Local Z = longueur du segment
            //
            // On ajoute un léger recouvrement pour éviter les micro-coupures
            // entre deux segments successifs.
            cube.transform.localScale = new Vector3(
                roadWidth,
                roadHeight,
                length + roadWidth * 0.35f
            );

            OverlayPrimitiveUtil.RemoveCollider(cube);
            OverlayPrimitiveUtil.ApplyMaterial(cube, material);

            if (_debugRoadSegmentLogs < 10)
            {
                _debugRoadSegmentLogs++;
                Log.Info(
                    "GroundOverlay: CREATED oriented road cube " +
                    name +
                    " pos=" + cube.transform.position +
                    " scale=" + cube.transform.localScale +
                    " length=" + length
                );
            }

            return true;
        }

        private void CreateBeacon(string name, Vector3 basePosition, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);
            cube.transform.position = basePosition + Vector3.up * (_config.BeaconHeight * 0.5f);
            cube.transform.localScale = new Vector3(_config.BeaconWidth, _config.BeaconHeight, _config.BeaconWidth);

            OverlayPrimitiveUtil.RemoveCollider(cube);
            OverlayPrimitiveUtil.ApplyMaterial(cube, material);

            Log.Info("GroundOverlay: beacon created: " + name + " at " + cube.transform.position);
        }

        private void CreateTower(string name, Vector3 basePosition, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);
            cube.transform.position = basePosition + Vector3.up * (_config.TowerHeight * 0.5f);
            cube.transform.localScale = new Vector3(_config.TowerWidth, _config.TowerHeight, _config.TowerWidth);

            OverlayPrimitiveUtil.RemoveCollider(cube);
            OverlayPrimitiveUtil.ApplyMaterial(cube, material);

            Log.Info("GroundOverlay: tower created: " + name + " at " + cube.transform.position);
        }
    }
}
