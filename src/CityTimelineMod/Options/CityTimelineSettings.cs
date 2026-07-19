using System;
using System.IO;
using System.Text;
using CityTimelineMod.Rendering;
using CityTimelineMod.Config;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using Newtonsoft.Json;
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
        private bool _showOverlayHud = true;

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
        private bool _runtimeRoadImportEnabled = false;
        private bool _runtimeRoadImportRunOnce = false;
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

        public CityTimelineSettings(IMod mod) : base(mod)
        {
            LoadFromRuntimeConfig();
        }

        [SettingsUIKeyboardBinding(
            BindingKeyboard.Z,
            nameof(KeyBindingToggleCohtmlHud),
            alt: true
        )]
        [SettingsUISection(SectionGeneral, GroupMain)]
        public ProxyBinding KeyBindingToggleCohtmlHud { get; set; }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool ModEnabled
        {
            get => _modEnabled;
            set => SetBool(ref _modEnabled, value, "modEnabled");
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
            set => _runtimeRoadImportEnabled = false;
        }

        [SettingsUIHidden]
        public bool RuntimeRoadImportRunOnce
        {
            get => false;
            set => _runtimeRoadImportRunOnce = false;
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
            _showOverlayHud = true;

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
            _runtimeRoadImportEnabled = false;
            _runtimeRoadImportRunOnce = false;
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

            LoadFromRuntimeConfig();

            _suppressConfigWrite = false;
        }

        public void LoadFromRuntimeConfig()
        {
            _suppressConfigWrite = true;

            try
            {
                var path = ResolveRuntimeConfigPath();

                if (File.Exists(path))
                {
                    var root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));

                    _modEnabled = GetBool(root, "modEnabled", _modEnabled);
                    _showOverlayHud = GetBool(root, "showOverlayHud", _showOverlayHud);

                    _renderRoads = GetBool(root, "renderRoads", _renderRoads);
                    _renderPaths = GetBool(root, "renderPaths", _renderPaths);
                    _renderWaterLines = GetBool(root, "renderWaterLines", _renderWaterLines);
                    _renderWaterAreas = GetBool(root, "renderWaterAreas", _renderWaterAreas);
                    _renderWaterAreaOutlines = GetBool(root, "renderWaterAreaOutlines", _renderWaterAreaOutlines);
                    _renderWaterAreaFillMeshes = GetBool(root, "renderWaterAreaFillMeshes", _renderWaterAreaFillMeshes);
                    _renderZoning = GetBool(root, "renderZoning", _renderZoning);
                    _renderMapBounds = GetBool(root, "renderMapBounds", _renderMapBounds);
                    _renderWorldMapBounds = GetBool(root, "renderWorldMapBounds", _renderWorldMapBounds);
                    _renderHeightMapBounds = GetBool(root, "renderHeightMapBounds", _renderHeightMapBounds);
                    _renderMapCenter = GetBool(root, "renderMapCenter", _renderMapCenter);
                    _verboseOverlayLogs = GetBool(root, "verboseOverlayLogs", _verboseOverlayLogs);

                    _roadAlpha = GetFloat(root, "roadAlpha", _roadAlpha);
                    _pathAlpha = GetFloat(root, "pathAlpha", _pathAlpha);
                    _waterLineAlpha = GetFloat(root, "waterLineAlpha", _waterLineAlpha);
                    _waterAreaOutlineAlpha = GetFloat(root, "waterAreaOutlineAlpha", _waterAreaOutlineAlpha);
                    _waterAreaFillAlpha = GetFloat(root, "waterAreaFillAlpha", _waterAreaFillAlpha);
                    _zoningAlpha = GetFloat(root, "zoningAlpha", _zoningAlpha);
                    _mapBoundsAlpha = GetFloat(root, "mapBoundsAlpha", _mapBoundsAlpha);
                    _worldMapBoundsAlpha = GetFloat(root, "worldMapBoundsAlpha", _worldMapBoundsAlpha);
                    _heightMapBoundsAlpha = GetFloat(root, "heightMapBoundsAlpha", _heightMapBoundsAlpha);
                    _mapCenterAlpha = GetFloat(root, "mapCenterAlpha", _mapCenterAlpha);

                    _roadYOffset = GetFloat(root, "roadYOffset", _roadYOffset);
                    _pathYOffset = GetFloat(root, "pathYOffset", _pathYOffset);
                    _waterLineYOffset = GetFloat(root, "waterLineYOffset", _waterLineYOffset);
                    _waterAreaOutlineYOffset = GetFloat(root, "waterAreaOutlineYOffset", _waterAreaOutlineYOffset);
                    _waterAreaFillYOffset = GetFloat(root, "waterAreaFillYOffset", _waterAreaFillYOffset);
                    _zoningFillYOffset = GetFloat(root, "zoningFillYOffset", _zoningFillYOffset);
                    _mapBoundsYOffset = GetFloat(root, "mapBoundsYOffset", _mapBoundsYOffset);

                    _roadGeometrySource = GetString(root, "roadGeometrySource", _roadGeometrySource);
                    _minimumRoadDebugTier = GetInt(root, "minimumRoadDebugTier", _minimumRoadDebugTier);
                    _runtimeRoadImportEnabled = GetBool(root, "runtimeRoadImportEnabled", _runtimeRoadImportEnabled);
                    _runtimeRoadImportRunOnce = GetBool(root, "runtimeRoadImportRunOnce", _runtimeRoadImportRunOnce);
                    _runtimeRoadImportMaxSegments = GetInt(root, "runtimeRoadImportMaxSegments", _runtimeRoadImportMaxSegments);
                    _runtimeRoadImportBatchSize = GetInt(root, "runtimeRoadImportBatchSize", _runtimeRoadImportBatchSize);
                    _runtimeRoadImportSelectionMode = GetString(root, "runtimeRoadImportSelectionMode", _runtimeRoadImportSelectionMode);
                    _runtimeRoadImportDistanceBucketMeters = GetFloat(root, "runtimeRoadImportDistanceBucketMeters", _runtimeRoadImportDistanceBucketMeters);
                    _runtimeRoadImportPriorityWeight = GetFloat(root, "runtimeRoadImportPriorityWeight", _runtimeRoadImportPriorityWeight);
                    _runtimeRoadImportSkipParkingAisles = GetBool(root, "runtimeRoadImportSkipParkingAisles", _runtimeRoadImportSkipParkingAisles);
                    _runtimeRoadImportSkipClearlyUnpaved = GetBool(root, "runtimeRoadImportSkipClearlyUnpaved", _runtimeRoadImportSkipClearlyUnpaved);

                    _roadColorDefault = GetString(root, "roadColorDefault", _roadColorDefault);
                    _roadColorMotorway = GetString(root, "roadColorMotorway", _roadColorMotorway);
                    _roadColorPrimary = GetString(root, "roadColorPrimary", _roadColorPrimary);
                    _roadColorSecondary = GetString(root, "roadColorSecondary", _roadColorSecondary);
                    _roadColorTertiary = GetString(root, "roadColorTertiary", _roadColorTertiary);
                    _roadColorLink = GetString(root, "roadColorLink", _roadColorLink);
                    _pathColor = GetString(root, "pathColor", _pathColor);

                    _waterLineColor = GetString(root, "waterLineColor", _waterLineColor);
                    _waterAreaOutlineColor = GetString(root, "waterAreaOutlineColor", _waterAreaOutlineColor);
                    _waterAreaFillColor = GetString(root, "waterAreaFillColor", _waterAreaFillColor);

                    _zoningResidentialLowColor = GetString(root, "zoningResidentialLowColor", _zoningResidentialLowColor);
                    _zoningResidentialMediumColor = GetString(root, "zoningResidentialMediumColor", _zoningResidentialMediumColor);
                    _zoningResidentialHighColor = GetString(root, "zoningResidentialHighColor", _zoningResidentialHighColor);
                    _zoningCommercialLowColor = GetString(root, "zoningCommercialLowColor", _zoningCommercialLowColor);
                    _zoningCommercialHighColor = GetString(root, "zoningCommercialHighColor", _zoningCommercialHighColor);
                    _zoningRetailColor = GetString(root, "zoningRetailColor", _zoningRetailColor);
                    _zoningIndustrialColor = GetString(root, "zoningIndustrialColor", _zoningIndustrialColor);
                    _zoningOfficeColor = GetString(root, "zoningOfficeColor", _zoningOfficeColor);
                    _zoningSurfaceColor = GetString(root, "zoningSurfaceColor", _zoningSurfaceColor);
                    _zoningRampColor = GetString(root, "zoningRampColor", _zoningRampColor);
                    _zoningMixedColor = GetString(root, "zoningMixedColor", _zoningMixedColor);
                    _zoningFallbackColor = GetString(root, "zoningFallbackColor", _zoningFallbackColor);

                    _worldMapBoundsColor = GetString(root, "worldMapBoundsColor", _worldMapBoundsColor);
                    _heightMapBoundsColor = GetString(root, "heightMapBoundsColor", _heightMapBoundsColor);
                    _mapCenterColor = GetString(root, "mapCenterColor", _mapCenterColor);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log("[CityTimelineMod] Failed to load runtime options from config.json: " + ex);
            }
            finally
            {
                _suppressConfigWrite = false;
            }
        }

        private void SetBool(ref bool field, bool value, string configKey)
        {
            if (field == value)
                return;

            field = value;

            if (!_suppressConfigWrite)
                SaveValueToRuntimeConfig(configKey, value);
        }

        private void SetFloat(ref float field, float value, string configKey)
        {
            if (Math.Abs(field - value) < 0.00001f)
                return;

            field = value;

            if (!_suppressConfigWrite)
                SaveValueToRuntimeConfig(configKey, value);
        }

        private void SetInt(ref int field, int value, string configKey)
        {
            if (field == value)
                return;

            field = value;

            if (!_suppressConfigWrite)
                SaveValueToRuntimeConfig(configKey, value);
        }

        private void SetString(ref string field, string value, string configKey)
        {
            value = value ?? "";

            if (string.Equals(field, value, StringComparison.Ordinal))
                return;

            field = value;

            if (!_suppressConfigWrite)
                SaveValueToRuntimeConfig(configKey, value);
        }

        private static bool GetBool(JObject root, string key, bool fallback)
        {
            if (root != null && root.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token))
            {
                try { return token.Value<bool>(); }
                catch { return fallback; }
            }

            return fallback;
        }

        private static float GetFloat(JObject root, string key, float fallback)
        {
            if (root != null && root.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token))
            {
                try { return token.Value<float>(); }
                catch { return fallback; }
            }

            return fallback;
        }

        private static int GetInt(JObject root, string key, int fallback)
        {
            if (root != null && root.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token))
            {
                try { return token.Value<int>(); }
                catch { return fallback; }
            }

            return fallback;
        }

        private static string GetString(JObject root, string key, string fallback)
        {
            if (root != null && root.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token))
            {
                try { return token.Value<string>() ?? fallback; }
                catch { return fallback; }
            }

            return fallback;
        }

        private static void SaveValueToRuntimeConfig(string key, object value)
        {
            try
            {
                var path = ResolveRuntimeConfigPath();
                var dir = Path.GetDirectoryName(path);

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                JObject root;

                if (File.Exists(path))
                    root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                else
                    root = new JObject();

                root[key] = JToken.FromObject(value);

                File.WriteAllText(path, root.ToString(Formatting.Indented), new UTF8Encoding(false));

                UnityEngine.Debug.Log("[CityTimelineMod] Option saved to config.json: " + key + "=" + value);

                GeoDebugOverlay.ApplyRuntimeConfigChange(key);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log("[CityTimelineMod] Failed to save option to config.json: " + key + " => " + ex);
            }
        }

        private static string ResolveRuntimeConfigPath()
        {
            return CityTimelineConfigStorage.ResolveWritableConfigPath();
        }
    }
}

