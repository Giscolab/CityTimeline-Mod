// CTM CONTRACT GUARD: Never disable the 57 km LargeMap + PlayableWorld contract unless explicitly requested.
using System;
using CityTimelineMod.Rendering;
using CityTimelineMod.Config;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Options
{
    [FileLocation("CityTimelineMod")]
    [SettingsUIGroupOrder(new string[] {
        GroupMain,
        GroupWorldMapBounds,
        GroupHeightMapBounds,
        GroupMapCenter,
        GroupRoutes,
        GroupPaths,
        GroupWater,
        GroupZoning
    })]
    public sealed class CityTimelineSettings : ModSetting
    {
        public const string SectionGeneral = "general";
        public const string SectionNetwork = "network";
        public const string SectionWater = "water";
        public const string SectionZoning = "zoning";

        public const string GroupMain = "main";
        public const string GroupRoutes = "routes";
        public const string GroupPaths = "paths";
        public const string GroupWater = "water";
        public const string GroupZoning = "zoning";
        public const string GroupBounds = "bounds";
        public const string GroupWorldMapBounds = "worldmapbounds";
        public const string GroupHeightMapBounds = "heightmapbounds";
        public const string GroupMapCenter = "mapcenter";
private const string ModFolderName = "CityTimelineMod";

        private bool _suppressConfigWrite;

        private bool _modEnabled = true;
        private bool _largeMapEnabled = true;
        private bool _playableWorldEnabled = true;
        private bool _showOverlayHud = false;

        private bool _renderRoads = true;
        private bool _renderPaths = true;
        private bool _renderWaterLines = true;
        private bool _renderWaterAreas = true;
        private bool _renderWaterAreaOutlines = true;
        private bool _renderWaterAreaFillMeshes = true;
        private bool _renderZoning = true;
        private bool _renderMapBounds = true;
        private bool _renderWorldMapBounds = true;
        private bool _renderHeightMapBounds = true;
        private bool _renderMapCenter = true;
        private bool _verboseOverlayLogs = false;

        private float _roadAlpha = 0.2f;
        private float _pathAlpha = 0.35f;
        private float _waterLineAlpha = 0.2f;
        private float _waterAreaOutlineAlpha = 0.2f;
        private float _waterAreaFillAlpha = 0.05f;
        private float _zoningAlpha = 0.05f;
        private float _mapBoundsAlpha = 0.85f;
        private float _worldMapBoundsAlpha = 0.85f;
        private float _heightMapBoundsAlpha = 0.85f;
        private float _mapCenterAlpha = 0.85f;

        private float _roadYOffset = 0.25f;
        private float _pathYOffset = 0.28f;
        private float _waterLineYOffset = 0.35f;
        private float _waterAreaOutlineYOffset = 0.3f;
        private float _waterAreaFillYOffset = 0.03f;
        private float _zoningFillYOffset = 0.05f;
        private float _mapBoundsYOffset = 0.75f;

        private string _roadGeometrySource = "driveable";
        private int _minimumRoadDebugTier = 0;
        private int _runtimeRoadImportMaxSegments = 0;
        private int _runtimeRoadImportBatchSize = 256;
        private string _runtimeRoadImportSelectionMode = "staged-cell-priority";
        private float _runtimeRoadImportDistanceBucketMeters = 500f;
        private float _runtimeRoadImportPriorityWeight = 1f;
        private bool _runtimeRoadImportSkipParkingAisles = true;
        private bool _runtimeRoadImportSkipClearlyUnpaved = true;

        private string _roadColorDefault = "grisClair";
        private string _roadColorMotorway = "rouge";
        private string _roadColorPrimary = "orange";
        private string _roadColorSecondary = "jaune";
        private string _roadColorTertiary = "blanc";
        private string _roadColorLink = "magenta";
        private string _pathColor = "cyan";

        private string _waterLineColor = "bleuClair";
        private string _waterAreaOutlineColor = "bleuFonce";
        private string _waterAreaFillColor = "bleu";

        private string _zoningResidentialLowColor = "vertClair";
        private string _zoningResidentialMediumColor = "vertMoyen";
        private string _zoningResidentialHighColor = "vertFonce";
        private string _zoningCommercialLowColor = "bleuCommercialClair";
        private string _zoningCommercialHighColor = "bleuCommercial";
        private string _zoningRetailColor = "bleuDetail";
        private string _zoningIndustrialColor = "ambre";
        private string _zoningOfficeColor = "violet";
        private string _zoningSurfaceColor = "vertParkingClair";
        private string _zoningRampColor = "vertParking";
        private string _zoningMixedColor = "turquoise";
        private string _zoningFallbackColor = "blanc";

        private string _worldMapBoundsColor = "blancLimites";
        private string _heightMapBoundsColor = "magentaHeightmap";
        private string _mapCenterColor = "rougeCentre";

        internal bool RuntimeConfigReliable { get; private set; }

        public CityTimelineSettings(IMod mod) : base(mod)
        {
        }

[SettingsUIKeyboardBinding(
    BindingKeyboard.Z,
    nameof(KeyBindingToggleCohtmlHud),
    alt: true
)]
[SettingsUISection(SectionGeneral, GroupMain)]
public ProxyBinding KeyBindingToggleCohtmlHud { get; set; }

[SettingsUIKeyboardBinding(
    BindingKeyboard.H,
    nameof(KeyBindingToggleOverlayHud),
    alt: true
)]
[SettingsUISection(SectionGeneral, GroupMain)]
public ProxyBinding KeyBindingToggleOverlayHud { get; set; }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool ModEnabled
        {
            get => _modEnabled;
            set => SetBool(ref _modEnabled, value, "modEnabled");
        }

        [SettingsUIHidden]
        public bool LargeMapEnabled
        {
            get => _largeMapEnabled;
            set => SetBool(ref _largeMapEnabled, value, "largeMapEnabled");
        }

        [SettingsUIHidden]
        public bool PlayableWorldEnabled
        {
            get => _playableWorldEnabled;
            set => SetBool(ref _playableWorldEnabled, value, "playableWorldEnabled");
        }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool ShowOverlayHud
        {
            get => _showOverlayHud;
            set => SetBool(ref _showOverlayHud, value, "showOverlayHud");
        }

        [SettingsUISection(SectionGeneral, GroupWorldMapBounds)]
        public bool RenderWorldMapBounds
        {
            get => _renderWorldMapBounds;
            set => SetBool(ref _renderWorldMapBounds, value, "renderWorldMapBounds");
        }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f)]
        [SettingsUISection(SectionGeneral, GroupWorldMapBounds)]
        public float WorldMapBoundsAlpha
        {
            get => _worldMapBoundsAlpha;
            set => SetFloat(ref _worldMapBoundsAlpha, value, "worldMapBoundsAlpha");
        }

        [SettingsUISection(SectionGeneral, GroupHeightMapBounds)]
        public bool RenderHeightMapBounds
        {
            get => _renderHeightMapBounds;
            set => SetBool(ref _renderHeightMapBounds, value, "renderHeightMapBounds");
        }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f)]
        [SettingsUISection(SectionGeneral, GroupHeightMapBounds)]
        public float HeightMapBoundsAlpha
        {
            get => _heightMapBoundsAlpha;
            set => SetFloat(ref _heightMapBoundsAlpha, value, "heightMapBoundsAlpha");
        }

        [SettingsUISection(SectionGeneral, GroupMapCenter)]
        public bool RenderMapCenter
        {
            get => _renderMapCenter;
            set => SetBool(ref _renderMapCenter, value, "renderMapCenter");
        }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f)]
        [SettingsUISection(SectionGeneral, GroupMapCenter)]
        public float MapCenterAlpha
        {
            get => _mapCenterAlpha;
            set => SetFloat(ref _mapCenterAlpha, value, "mapCenterAlpha");
        }
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public bool RenderRoads
        {
            get => _renderRoads;
            set => SetBool(ref _renderRoads, value, "renderRoads");
        }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f)]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public float RoadAlpha
        {
            get => _roadAlpha;
            set => SetFloat(ref _roadAlpha, value, "roadAlpha");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public string RoadGeometrySource
        {
            get => _roadGeometrySource;
            set => SetString(ref _roadGeometrySource, value, "roadGeometrySource");
        }

        [SettingsUISlider(min = 0f, max = 4f, step = 1f)]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public int MinimumRoadDebugTier
        {
            get => _minimumRoadDebugTier;
            set => SetInt(ref _minimumRoadDebugTier, value, "minimumRoadDebugTier");
        }

        [SettingsUIHidden]
        public bool RuntimeRoadImportEnabled
        {
            get => false;
            set { }
        }

        [SettingsUIHidden]
        public bool RuntimeRoadImportRunOnce
        {
            get => false;
            set { }
        }

        [SettingsUISlider(min = 0f, max = 500000f, step = 100f)]
        [SettingsUIHidden]
        public int RuntimeRoadImportMaxSegments
        {
            get => _runtimeRoadImportMaxSegments;
            set => SetInt(ref _runtimeRoadImportMaxSegments, value, "runtimeRoadImportMaxSegments");
        }

        [SettingsUISlider(min = 1f, max = 4096f, step = 1f)]
        [SettingsUIHidden]
        public int RuntimeRoadImportBatchSize
        {
            get => _runtimeRoadImportBatchSize;
            set => SetInt(ref _runtimeRoadImportBatchSize, value, "runtimeRoadImportBatchSize");
        }

        [SettingsUITextInput]
        [SettingsUIHidden]
        public string RuntimeRoadImportSelectionMode
        {
            get => _runtimeRoadImportSelectionMode;
            set => SetString(ref _runtimeRoadImportSelectionMode, value, "runtimeRoadImportSelectionMode");
        }

        [SettingsUISlider(min = 50f, max = 5000f, step = 50f)]
        [SettingsUIHidden]
        public float RuntimeRoadImportDistanceBucketMeters
        {
            get => _runtimeRoadImportDistanceBucketMeters;
            set => SetFloat(ref _runtimeRoadImportDistanceBucketMeters, value, "runtimeRoadImportDistanceBucketMeters");
        }

        [SettingsUISlider(min = 0f, max = 3f, step = 0.05f)]
        [SettingsUIHidden]
        public float RuntimeRoadImportPriorityWeight
        {
            get => _runtimeRoadImportPriorityWeight;
            set => SetFloat(ref _runtimeRoadImportPriorityWeight, value, "runtimeRoadImportPriorityWeight");
        }

        [SettingsUIHidden]
        public bool RuntimeRoadImportSkipParkingAisles
        {
            get => _runtimeRoadImportSkipParkingAisles;
            set => SetBool(ref _runtimeRoadImportSkipParkingAisles, value, "runtimeRoadImportSkipParkingAisles");
        }

        [SettingsUIHidden]
        public bool RuntimeRoadImportSkipClearlyUnpaved
        {
            get => _runtimeRoadImportSkipClearlyUnpaved;
            set => SetBool(ref _runtimeRoadImportSkipClearlyUnpaved, value, "runtimeRoadImportSkipClearlyUnpaved");
        }

        [SettingsUIButton]
        [SettingsUIHidden]
        public bool ImportRuntimeRoadsNow
        {
            set { }
        }

        [SettingsUIButton]
        [SettingsUIHidden]
        public bool UndoRuntimeRoadImport
        {
            set { }
        }

        [SettingsUIButton]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
[SettingsUITextInput]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public string RoadColorDefault
        {
            get => _roadColorDefault;
            set => SetString(ref _roadColorDefault, value, "roadColorDefault");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public string RoadColorMotorway
        {
            get => _roadColorMotorway;
            set => SetString(ref _roadColorMotorway, value, "roadColorMotorway");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public string RoadColorPrimary
        {
            get => _roadColorPrimary;
            set => SetString(ref _roadColorPrimary, value, "roadColorPrimary");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public string RoadColorSecondary
        {
            get => _roadColorSecondary;
            set => SetString(ref _roadColorSecondary, value, "roadColorSecondary");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public string RoadColorTertiary
        {
            get => _roadColorTertiary;
            set => SetString(ref _roadColorTertiary, value, "roadColorTertiary");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionNetwork, GroupRoutes)]
        public string RoadColorLink
        {
            get => _roadColorLink;
            set => SetString(ref _roadColorLink, value, "roadColorLink");
        }

        [SettingsUISection(SectionNetwork, GroupPaths)]
        public bool RenderPaths
        {
            get => _renderPaths;
            set => SetBool(ref _renderPaths, value, "renderPaths");
        }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f)]
        [SettingsUISection(SectionNetwork, GroupPaths)]
        public float PathAlpha
        {
            get => _pathAlpha;
            set => SetFloat(ref _pathAlpha, value, "pathAlpha");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionNetwork, GroupPaths)]
        public string PathColor
        {
            get => _pathColor;
            set => SetString(ref _pathColor, value, "pathColor");
        }

        [SettingsUISection(SectionWater, GroupWater)]
        public bool RenderWaterLines
        {
            get => _renderWaterLines;
            set => SetBool(ref _renderWaterLines, value, "renderWaterLines");
        }

        [SettingsUISection(SectionWater, GroupWater)]
        public bool RenderWaterAreas
        {
            get => _renderWaterAreas;
            set => SetBool(ref _renderWaterAreas, value, "renderWaterAreas");
        }

        [SettingsUISection(SectionWater, GroupWater)]
        public bool RenderWaterAreaOutlines
        {
            get => _renderWaterAreaOutlines;
            set => SetBool(ref _renderWaterAreaOutlines, value, "renderWaterAreaOutlines");
        }

        [SettingsUISection(SectionWater, GroupWater)]
        public bool RenderWaterAreaFillMeshes
        {
            get => _renderWaterAreaFillMeshes;
            set => SetBool(ref _renderWaterAreaFillMeshes, value, "renderWaterAreaFillMeshes");
        }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f)]
        [SettingsUISection(SectionWater, GroupWater)]
        public float WaterLineAlpha
        {
            get => _waterLineAlpha;
            set => SetFloat(ref _waterLineAlpha, value, "waterLineAlpha");
        }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f)]
        [SettingsUISection(SectionWater, GroupWater)]
        public float WaterAreaOutlineAlpha
        {
            get => _waterAreaOutlineAlpha;
            set => SetFloat(ref _waterAreaOutlineAlpha, value, "waterAreaOutlineAlpha");
        }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f)]
        [SettingsUISection(SectionWater, GroupWater)]
        public float WaterAreaFillAlpha
        {
            get => _waterAreaFillAlpha;
            set => SetFloat(ref _waterAreaFillAlpha, value, "waterAreaFillAlpha");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionWater, GroupWater)]
        public string WaterLineColor
        {
            get => _waterLineColor;
            set => SetString(ref _waterLineColor, value, "waterLineColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionWater, GroupWater)]
        public string WaterAreaOutlineColor
        {
            get => _waterAreaOutlineColor;
            set => SetString(ref _waterAreaOutlineColor, value, "waterAreaOutlineColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionWater, GroupWater)]
        public string WaterAreaFillColor
        {
            get => _waterAreaFillColor;
            set => SetString(ref _waterAreaFillColor, value, "waterAreaFillColor");
        }

        [SettingsUISection(SectionZoning, GroupZoning)]
        public bool RenderZoning
        {
            get => _renderZoning;
            set => SetBool(ref _renderZoning, value, "renderZoning");
        }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f)]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public float ZoningAlpha
        {
            get => _zoningAlpha;
            set => SetFloat(ref _zoningAlpha, value, "zoningAlpha");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningResidentialLowColor
        {
            get => _zoningResidentialLowColor;
            set => SetString(ref _zoningResidentialLowColor, value, "zoningResidentialLowColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningResidentialMediumColor
        {
            get => _zoningResidentialMediumColor;
            set => SetString(ref _zoningResidentialMediumColor, value, "zoningResidentialMediumColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningResidentialHighColor
        {
            get => _zoningResidentialHighColor;
            set => SetString(ref _zoningResidentialHighColor, value, "zoningResidentialHighColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningCommercialLowColor
        {
            get => _zoningCommercialLowColor;
            set => SetString(ref _zoningCommercialLowColor, value, "zoningCommercialLowColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningCommercialHighColor
        {
            get => _zoningCommercialHighColor;
            set => SetString(ref _zoningCommercialHighColor, value, "zoningCommercialHighColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningRetailColor
        {
            get => _zoningRetailColor;
            set => SetString(ref _zoningRetailColor, value, "zoningRetailColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningIndustrialColor
        {
            get => _zoningIndustrialColor;
            set => SetString(ref _zoningIndustrialColor, value, "zoningIndustrialColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningOfficeColor
        {
            get => _zoningOfficeColor;
            set => SetString(ref _zoningOfficeColor, value, "zoningOfficeColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningSurfaceColor
        {
            get => _zoningSurfaceColor;
            set => SetString(ref _zoningSurfaceColor, value, "zoningSurfaceColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningRampColor
        {
            get => _zoningRampColor;
            set => SetString(ref _zoningRampColor, value, "zoningRampColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningMixedColor
        {
            get => _zoningMixedColor;
            set => SetString(ref _zoningMixedColor, value, "zoningMixedColor");
        }

        [SettingsUITextInput]
        [SettingsUISection(SectionZoning, GroupZoning)]
        public string ZoningFallbackColor
        {
            get => _zoningFallbackColor;
            set => SetString(ref _zoningFallbackColor, value, "zoningFallbackColor");
        }

        [SettingsUIHidden]
        public bool RenderMapBounds
        {
            get => _renderMapBounds;
            set => SetBool(ref _renderMapBounds, value, "renderMapBounds");
        }

        [SettingsUIHidden]
        public float MapBoundsAlpha
        {
            get => _mapBoundsAlpha;
            set => SetFloat(ref _mapBoundsAlpha, value, "mapBoundsAlpha");
        }

        [SettingsUIHidden]
        public string WorldMapBoundsColor
        {
            get => _worldMapBoundsColor;
            set => SetString(ref _worldMapBoundsColor, value, "worldMapBoundsColor");
        }

        [SettingsUIHidden]
        public string HeightMapBoundsColor
        {
            get => _heightMapBoundsColor;
            set => SetString(ref _heightMapBoundsColor, value, "heightMapBoundsColor");
        }

        [SettingsUIHidden]
        public string MapCenterColor
        {
            get => _mapCenterColor;
            set => SetString(ref _mapCenterColor, value, "mapCenterColor");
        }

        [SettingsUIHidden]
        public bool VerboseOverlayLogs
        {
            get => _verboseOverlayLogs;
            set => SetBool(ref _verboseOverlayLogs, value, "verboseOverlayLogs");
        }

        [SettingsUIHidden]
        public float RoadYOffset { get => _roadYOffset; set => SetFloat(ref _roadYOffset, value, "roadYOffset"); }

        [SettingsUIHidden]
        public float PathYOffset { get => _pathYOffset; set => SetFloat(ref _pathYOffset, value, "pathYOffset"); }

        [SettingsUIHidden]
        public float WaterLineYOffset { get => _waterLineYOffset; set => SetFloat(ref _waterLineYOffset, value, "waterLineYOffset"); }

        [SettingsUIHidden]
        public float WaterAreaOutlineYOffset { get => _waterAreaOutlineYOffset; set => SetFloat(ref _waterAreaOutlineYOffset, value, "waterAreaOutlineYOffset"); }

        [SettingsUIHidden]
        public float WaterAreaFillYOffset { get => _waterAreaFillYOffset; set => SetFloat(ref _waterAreaFillYOffset, value, "waterAreaFillYOffset"); }

        [SettingsUIHidden]
        public float ZoningFillYOffset { get => _zoningFillYOffset; set => SetFloat(ref _zoningFillYOffset, value, "zoningFillYOffset"); }

        [SettingsUIHidden]
        public float MapBoundsYOffset { get => _mapBoundsYOffset; set => SetFloat(ref _mapBoundsYOffset, value, "mapBoundsYOffset"); }

        public override void SetDefaults()
        {
            _suppressConfigWrite = true;

            _modEnabled = true;
            _largeMapEnabled = true;
            _playableWorldEnabled = true;
            _showOverlayHud = false;

            _renderRoads = true;
            _renderPaths = true;
            _renderWaterLines = true;
            _renderWaterAreas = true;
            _renderWaterAreaOutlines = true;
            _renderWaterAreaFillMeshes = true;
            _renderZoning = true;
            _renderMapBounds = true;
            _renderWorldMapBounds = true;
            _renderHeightMapBounds = true;
            _renderMapCenter = true;
            _verboseOverlayLogs = false;

            _roadAlpha = 0.2f;
            _pathAlpha = 0.35f;
            _waterLineAlpha = 0.2f;
            _waterAreaOutlineAlpha = 0.2f;
            _waterAreaFillAlpha = 0.05f;
            _zoningAlpha = 0.05f;
            _mapBoundsAlpha = 0.85f;
            _worldMapBoundsAlpha = 0.85f;
            _heightMapBoundsAlpha = 0.85f;
            _mapCenterAlpha = 0.85f;

            _roadYOffset = 0.25f;
            _pathYOffset = 0.28f;
            _waterLineYOffset = 0.35f;
            _waterAreaOutlineYOffset = 0.3f;
            _waterAreaFillYOffset = 0.03f;
            _zoningFillYOffset = 0.05f;
            _mapBoundsYOffset = 0.75f;

            _roadGeometrySource = "driveable";
            _minimumRoadDebugTier = 0;
            _runtimeRoadImportMaxSegments = 0;
            _runtimeRoadImportBatchSize = 256;
            _runtimeRoadImportSelectionMode = "staged-cell-priority";
            _runtimeRoadImportDistanceBucketMeters = 500f;
            _runtimeRoadImportPriorityWeight = 1f;
            _runtimeRoadImportSkipParkingAisles = true;
            _runtimeRoadImportSkipClearlyUnpaved = true;

            _roadColorDefault = "grisClair";
            _roadColorMotorway = "rouge";
            _roadColorPrimary = "orange";
            _roadColorSecondary = "jaune";
            _roadColorTertiary = "blanc";
            _roadColorLink = "magenta";
            _pathColor = "cyan";

            _waterLineColor = "bleuClair";
            _waterAreaOutlineColor = "bleuFonce";
            _waterAreaFillColor = "bleu";

            _zoningResidentialLowColor = "vertClair";
            _zoningResidentialMediumColor = "vertMoyen";
            _zoningResidentialHighColor = "vertFonce";
            _zoningCommercialLowColor = "bleuCommercialClair";
            _zoningCommercialHighColor = "bleuCommercial";
            _zoningRetailColor = "bleuDetail";
            _zoningIndustrialColor = "ambre";
            _zoningOfficeColor = "violet";
            _zoningSurfaceColor = "vertParkingClair";
            _zoningRampColor = "vertParking";
            _zoningMixedColor = "turquoise";
            _zoningFallbackColor = "blanc";

            _worldMapBoundsColor = "blancLimites";
            _heightMapBoundsColor = "magentaHeightmap";
            _mapCenterColor = "rougeCentre";

            RuntimeConfigReliable = false;

            _suppressConfigWrite = false;
        }

        internal void ApplyRuntimeSnapshot(GeoOverlayConfig snapshot)
        {
            _suppressConfigWrite = true;

            try
            {
                RuntimeConfigReliable = snapshot != null && snapshot.IsReliable;
                if (snapshot == null)
                {
                    _modEnabled = false;
                    _largeMapEnabled = false;
                    _playableWorldEnabled = false;
                    return;
                }

                _modEnabled = snapshot.ModEnabled;
                _largeMapEnabled = snapshot.LargeMapEnabled;
                _playableWorldEnabled = snapshot.PlayableWorldEnabled;

                // An unreliable snapshot is constructed fail-closed with all
                // lifecycle flags false. Publish those safe values in the
                // official options UI without applying partially parsed visuals.
                if (!RuntimeConfigReliable)
                    return;

                _showOverlayHud = snapshot.ShowOverlayHud;

                _renderRoads = snapshot.RenderRoads;
                _renderPaths = snapshot.RenderPaths;
                _renderWaterLines = snapshot.RenderWaterLines;
                _renderWaterAreas = snapshot.RenderWaterAreas;
                _renderWaterAreaOutlines = snapshot.RenderWaterAreaOutlines;
                _renderWaterAreaFillMeshes = snapshot.RenderWaterAreaFillMeshes;
                _renderZoning = snapshot.RenderZoning;
                _renderMapBounds = snapshot.RenderMapBounds;
                _renderWorldMapBounds = snapshot.RenderWorldMapBounds;
                _renderHeightMapBounds = snapshot.RenderHeightMapBounds;
                _renderMapCenter = snapshot.RenderMapCenter;
                _verboseOverlayLogs = snapshot.VerboseOverlayLogs;

                _roadAlpha = snapshot.RoadAlpha;
                _pathAlpha = snapshot.PathAlpha;
                _waterLineAlpha = snapshot.WaterLineAlpha;
                _waterAreaOutlineAlpha = snapshot.WaterAreaOutlineAlpha;
                _waterAreaFillAlpha = snapshot.WaterAreaFillAlpha;
                _zoningAlpha = snapshot.ZoningAlpha;
                _mapBoundsAlpha = snapshot.MapBoundsAlpha;
                _worldMapBoundsAlpha = snapshot.WorldMapBoundsAlpha;
                _heightMapBoundsAlpha = snapshot.HeightMapBoundsAlpha;
                _mapCenterAlpha = snapshot.MapCenterAlpha;

                _roadYOffset = snapshot.RoadYOffset;
                _pathYOffset = snapshot.PathYOffset;
                _waterLineYOffset = snapshot.WaterLineYOffset;
                _waterAreaOutlineYOffset = snapshot.WaterAreaOutlineYOffset;
                _waterAreaFillYOffset = snapshot.WaterAreaFillYOffset;
                _zoningFillYOffset = snapshot.ZoningFillYOffset;
                _mapBoundsYOffset = snapshot.MapBoundsYOffset;

                _roadGeometrySource = snapshot.RoadGeometrySource;
                _minimumRoadDebugTier = snapshot.MinimumRoadDebugTier;
                _runtimeRoadImportMaxSegments = snapshot.RuntimeRoadImportMaxSegments;
                _runtimeRoadImportBatchSize = snapshot.RuntimeRoadImportBatchSize;
                _runtimeRoadImportSelectionMode = snapshot.RuntimeRoadImportSelectionMode;
                _runtimeRoadImportDistanceBucketMeters = snapshot.RuntimeRoadImportDistanceBucketMeters;
                _runtimeRoadImportPriorityWeight = snapshot.RuntimeRoadImportPriorityWeight;
                _runtimeRoadImportSkipParkingAisles = snapshot.RuntimeRoadImportSkipParkingAisles;
                _runtimeRoadImportSkipClearlyUnpaved = snapshot.RuntimeRoadImportSkipClearlyUnpaved;

                _roadColorDefault = snapshot.RoadColorDefault;
                _roadColorMotorway = snapshot.RoadColorMotorway;
                _roadColorPrimary = snapshot.RoadColorPrimary;
                _roadColorSecondary = snapshot.RoadColorSecondary;
                _roadColorTertiary = snapshot.RoadColorTertiary;
                _roadColorLink = snapshot.RoadColorLink;
                _pathColor = snapshot.PathColor;

                _waterLineColor = snapshot.WaterLineColor;
                _waterAreaOutlineColor = snapshot.WaterAreaOutlineColor;
                _waterAreaFillColor = snapshot.WaterAreaFillColor;

                _zoningResidentialLowColor = snapshot.ZoningResidentialLowColor;
                _zoningResidentialMediumColor = snapshot.ZoningResidentialMediumColor;
                _zoningResidentialHighColor = snapshot.ZoningResidentialHighColor;
                _zoningCommercialLowColor = snapshot.ZoningCommercialLowColor;
                _zoningCommercialHighColor = snapshot.ZoningCommercialHighColor;
                _zoningRetailColor = snapshot.ZoningRetailColor;
                _zoningIndustrialColor = snapshot.ZoningIndustrialColor;
                _zoningOfficeColor = snapshot.ZoningOfficeColor;
                _zoningSurfaceColor = snapshot.ZoningSurfaceColor;
                _zoningRampColor = snapshot.ZoningRampColor;
                _zoningMixedColor = snapshot.ZoningMixedColor;
                _zoningFallbackColor = snapshot.ZoningFallbackColor;

                _worldMapBoundsColor = snapshot.WorldMapBoundsColor;
                _heightMapBoundsColor = snapshot.HeightMapBoundsColor;
                _mapCenterColor = snapshot.MapCenterColor;
            }
            finally
            {
                _suppressConfigWrite = false;
            }
        }

        internal void ApplyRuntimeVisualSnapshot(GeoOverlayConfig snapshot)
        {
            if (snapshot == null || !snapshot.IsReliable)
                return;

            // Lifecycle decisions are restart-scoped. A visual reload may
            // refresh official option values, but it must not change the three
            // values that authorized the current runtime session.
            var modEnabled = _modEnabled;
            var largeMapEnabled = _largeMapEnabled;
            var playableWorldEnabled = _playableWorldEnabled;

            try
            {
                ApplyRuntimeSnapshot(snapshot);
            }
            finally
            {
                _modEnabled = modEnabled;
                _largeMapEnabled = largeMapEnabled;
                _playableWorldEnabled = playableWorldEnabled;
            }
        }

        private void SetBool(ref bool field, bool value, string configKey)
        {
            if (field == value)
                return;

            var previous = field;
            field = value;

            if (!_suppressConfigWrite && !SaveValueToRuntimeConfig(configKey, value))
                field = previous;
        }

        private void SetFloat(ref float field, float value, string configKey)
        {
            if (Math.Abs(field - value) < 0.00001f)
                return;

            var previous = field;
            field = value;

            if (!_suppressConfigWrite && !SaveValueToRuntimeConfig(configKey, value))
                field = previous;
        }

        private void SetInt(ref int field, int value, string configKey)
        {
            if (field == value)
                return;

            var previous = field;
            field = value;

            if (!_suppressConfigWrite && !SaveValueToRuntimeConfig(configKey, value))
                field = previous;
        }

        private void SetString(ref string field, string value, string configKey)
        {
            value = value ?? "";

            if (string.Equals(field, value, StringComparison.Ordinal))
                return;

            var previous = field;
            field = value;

            if (!_suppressConfigWrite && !SaveValueToRuntimeConfig(configKey, value))
                field = previous;
        }

        private bool SaveValueToRuntimeConfig(string key, object value)
        {
            try
            {
                var runtimeSnapshot = Mod.RuntimeConfig;
                if (runtimeSnapshot == null)
                {
                    UnityEngine.Debug.Log(
                        "[CityTimelineMod] Option save blocked because the runtime snapshot is unavailable: " +
                        key
                    );
                    return false;
                }

                var path = ResolveRuntimeConfigPath();
                JObject updatedRoot;
                string updateError;
                if (!GeoOverlayConfig.TryUpdateRuntimeConfigFile(
                    path,
                    root =>
                    {
                        var replacement = JToken.FromObject(value);
                        JToken existing;
                        if (root.TryGetValue(
                            key,
                            StringComparison.OrdinalIgnoreCase,
                            out existing
                        ))
                        {
                            existing.Replace(replacement);
                        }
                        else
                        {
                            root[key] = replacement;
                        }
                    },
                    out updatedRoot,
                    out updateError
                ))
                {
                    UnityEngine.Debug.Log(
                        "[CityTimelineMod] Failed to save option to config.json: " +
                        key + " => " + updateError
                    );
                    return false;
                }

                UnityEngine.Debug.Log("[CityTimelineMod] Option saved to config.json: " + key + "=" + value);

                if (GeoOverlayConfig.IsRestartScopedLifecycleOption(key))
                {
                    UnityEngine.Debug.Log(
                        "[CityTimelineMod] Lifecycle option saved; restart required before it takes effect. " +
                        "The current runtime snapshot is unchanged: " + key + "=" + value
                    );
                    return true;
                }

                if (!runtimeSnapshot.ApplyRuntimeOption(key, value))
                {
                    UnityEngine.Debug.Log(
                        "[CityTimelineMod] Runtime snapshot rejected saved option: " + key
                    );
                    return false;
                }

                GeoDebugOverlay.ApplyRuntimeConfigChange(key, value);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log("[CityTimelineMod] Failed to save option to config.json: " + key + " => " + ex);
                return false;
            }
        }

        private static string ResolveRuntimeConfigPath()
        {
            return CityTimelineConfigStorage.ResolveWritableConfigPath();
        }
    }
}
