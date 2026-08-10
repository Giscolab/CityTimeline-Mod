using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Runtime.CompilerServices;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace CityTimelineMod.Roads
{
    internal sealed class RoadPrefabResolver
    {
        private readonly EntityManager _entityManager;
        private readonly PrefabSystem _prefabSystem;
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly Dictionary<int, string> _namesByEntityIndex = new Dictionary<int, string>();
        private readonly Dictionary<int, PrefabBase> _prefabByEntityIndex = new Dictionary<int, PrefabBase>();
private readonly Dictionary<int, PrefabBase> _prefabByPrefabIndex = new Dictionary<int, PrefabBase>();
private readonly Dictionary<string, Entity> _entityByExactName = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
private readonly HashSet<string> _loggedExactResolutionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
private readonly HashSet<string> _loggedScoreFallbackKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Entity _defaultOneWayPrefab = Entity.Null;
        private Entity _defaultTwoWayPrefab = Entity.Null;
        private bool _initialized;

        internal RoadPrefabResolver(EntityManager entityManager, PrefabSystem prefabSystem)
        {
            _entityManager = entityManager;
            _prefabSystem = prefabSystem;
        }

        internal int CandidateCount
        {
            get { return _candidates.Count; }
        }

        internal void DumpPrefabDiagnostics(int maxPrefabs)
        {
            if (!TryInitialize())
            {
                Util.Log.Info("[RoadPrefabStats] unavailable: resolver initialization failed.");
                return;
            }

            var limit = Math.Max(0, Math.Min(maxPrefabs, _candidates.Count));

            Util.Log.Info(
                "[RoadPrefabStats] candidates=" + _candidates.Count +
                ", logging=" + limit +
                ", defaultOneWay=" + GetPrefabName(_defaultOneWayPrefab) +
                ", defaultTwoWay=" + GetPrefabName(_defaultTwoWayPrefab)
            );

            for (var i = 0; i < limit; i++)
            {
                var candidate = _candidates[i];

                Util.Log.Info(
                    "[RoadPrefabStats] #" + i +
                    " entity=" + candidate.Entity.Index +
                    " name=" + SafePrefabName(candidate.Name) +
                    " lanes=" + candidate.LaneCount +
                    " oneway=" + candidate.IsOneWay +
                    " speed=" + candidate.SpeedLimit.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture) +
                    " zoning=" + candidate.HasZoning +
                    " trafficLights=" + candidate.PreferTrafficLights +
                    " highwayRules=" + candidate.UseHighwayRules +
                    " bridge=" + candidate.HasBridgeData +
                    " auxiliary=" + candidate.HasAuxiliaryNet +
                    " family=" + ClassifyCandidateFamily(candidate) +
                    " flags=" + SafePrefabName(candidate.FlagsText) +
                    " prefabType=" + (candidate.PrefabBase != null ? candidate.PrefabBase.GetType().FullName : "<null>")
                );

                Util.Log.Info(
                    "[RoadPrefabStats] #" + i +
                    " components=" + DumpEntityComponents(candidate.Entity)
                );

                if (_entityManager.Exists(candidate.Entity) && _entityManager.HasComponent<Game.Prefabs.RoadData>(candidate.Entity))
                {
                    var roadData = _entityManager.GetComponentData<Game.Prefabs.RoadData>(candidate.Entity);
                    Util.Log.Info(
                        "[RoadPrefabStats] #" + i +
                        " RoadData=" + DumpValueFields(roadData, 24)
                    );
                }
            }
        }


        internal void DumpExactPrefabNameDiagnostics(int maxPrefabs)
        {
            if (!TryInitialize())
            {
                Util.Log.Info("[RoadPrefabNames] unavailable: resolver initialization failed.");
                return;
            }

            var exactByIndex = new Dictionary<int, PrefabBase>();

            foreach (var kv in _prefabByPrefabIndex)
                exactByIndex[kv.Key] = kv.Value;

            var reflectionRoads = CacheRoadPrefabsFromPrefabSystemReflection(exactByIndex);
            var limit = Math.Max(0, Math.Min(maxPrefabs, _candidates.Count));
            var exactResolved = 0;

            Util.Log.Info(
                "[RoadPrefabNames] candidates=" + _candidates.Count +
                ", logging=" + limit +
                ", exactIndexMap=" + exactByIndex.Count +
                ", reflectionRoads=" + reflectionRoads +
                ", defaultOneWay=" + GetPrefabName(_defaultOneWayPrefab) +
                ", defaultTwoWay=" + GetPrefabName(_defaultTwoWayPrefab)
            );

            for (var i = 0; i < limit; i++)
            {
                var candidate = _candidates[i];
                var prefabIndex = -1;

                if (_entityManager.Exists(candidate.Entity) && _entityManager.HasComponent<PrefabData>(candidate.Entity))
                {
                    var prefabData = _entityManager.GetComponentData<PrefabData>(candidate.Entity);
                    prefabIndex = prefabData.m_Index;
                }

                PrefabBase exactPrefab = null;

                if (candidate.PrefabBase != null)
                    exactPrefab = candidate.PrefabBase;
                else if (prefabIndex >= 0)
                    exactByIndex.TryGetValue(prefabIndex, out exactPrefab);

                if (exactPrefab != null && !string.IsNullOrWhiteSpace(exactPrefab.name))
                    exactResolved++;

                Util.Log.Info(
                    "[RoadPrefabNames] #" + i +
                    " entity=" + candidate.Entity.Index +
                    " idx=" + prefabIndex +
                    " internal=" + SafePrefabName(candidate.Name) +
                    " exact=" + SafePrefabName(exactPrefab != null ? exactPrefab.name : "<unresolved>") +
                    " exactType=" + SafePrefabName(exactPrefab != null ? exactPrefab.GetType().FullName : "<null>") +
                    " lanes=" + candidate.LaneCount +
                    " oneway=" + candidate.IsOneWay +
                    " speed=" + candidate.SpeedLimit.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture) +
                    " zoning=" + candidate.HasZoning +
                    " trafficLights=" + candidate.PreferTrafficLights +
                    " highwayRules=" + candidate.UseHighwayRules +
                    " family=" + ClassifyCandidateFamily(candidate) +
                    " flags=" + SafePrefabName(candidate.FlagsText)
                );

                if (exactPrefab != null)
                    Util.Log.Info("[RoadPrefabNames] #" + i + " prefabFields=" + DumpObjectFields(exactPrefab, 32));

                Util.Log.Info("[RoadPrefabNames] #" + i + " PrefabData=" + DumpComponentByFullName(candidate.Entity, "Game.Prefabs.PrefabData", 24));
                Util.Log.Info("[RoadPrefabNames] #" + i + " LoadedIndex=" + DumpComponentByFullName(candidate.Entity, "Game.Prefabs.LoadedIndex", 24));
                Util.Log.Info("[RoadPrefabNames] #" + i + " UIObjectData=" + DumpComponentByFullName(candidate.Entity, "Game.Prefabs.UIObjectData", 32));
                Util.Log.Info("[RoadPrefabNames] #" + i + " ObjectData=" + DumpComponentByFullName(candidate.Entity, "Game.Prefabs.ObjectData", 32));
                Util.Log.Info("[RoadPrefabNames] #" + i + " RoadData=" + DumpComponentByFullName(candidate.Entity, "Game.Prefabs.RoadData", 32));
                Util.Log.Info("[RoadPrefabNames] #" + i + " NetData=" + DumpComponentByFullName(candidate.Entity, "Game.Prefabs.NetData", 32));
            }

            Util.Log.Info(
                "[RoadPrefabNames] summary candidates=" + _candidates.Count +
                ", logged=" + limit +
                ", exactResolved=" + exactResolved +
                ", unresolved=" + Math.Max(0, limit - exactResolved) +
                ", exactIndexMap=" + exactByIndex.Count +
                ", reflectionRoads=" + reflectionRoads
            );
        }

        internal bool TryInitialize()
        {
            if (_initialized && _candidates.Count > 0)
                return true;

            _initialized = true;
            _candidates.Clear();
            _namesByEntityIndex.Clear();
            _prefabByEntityIndex.Clear();
_prefabByPrefabIndex.Clear();
_entityByExactName.Clear();
_loggedExactResolutionKeys.Clear();
_loggedScoreFallbackKeys.Clear();
            _defaultOneWayPrefab = Entity.Null;
            _defaultTwoWayPrefab = Entity.Null;

            CacheContentRoadPrefabs();

            EntityQuery query = default(EntityQuery);
            NativeArray<Entity> entities = default(NativeArray<Entity>);
            var queryCreated = false;

            try
            {
                query = _entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PrefabData>(),
                    ComponentType.ReadOnly<Game.Prefabs.RoadData>()
                );
                queryCreated = true;

                entities = query.ToEntityArray(Allocator.Temp);

                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];

                    if (!_entityManager.Exists(entity))
                        continue;

                    var prefabData = _entityManager.GetComponentData<PrefabData>(entity);
                    var prefabBase = ResolvePrefabBase(entity, prefabData.m_Index);
                    var prefabName = prefabBase != null && !string.IsNullOrWhiteSpace(prefabBase.name)
                        ? prefabBase.name
                        : "Road[idx=" + prefabData.m_Index + "]";

                    CuratedRoadProfile profile;
                    var hasProfile = TryGetCuratedProfile(prefabName, out profile);

                    var lanes = hasProfile ? profile.Lanes : 0;
                    var oneWay = hasProfile && profile.OneWay;
                    var serviceOnly = hasProfile && profile.ServiceOnly;

                    var candidate = CreateCandidate(entity, prefabBase, prefabName, lanes, oneWay, serviceOnly);

_candidates.Add(candidate);
_namesByEntityIndex[entity.Index] = prefabName;
if (!string.IsNullOrWhiteSpace(prefabName) && !_entityByExactName.ContainsKey(prefabName))
    _entityByExactName.Add(prefabName, entity);

                    if (prefabBase != null)
                    {
                        _prefabByEntityIndex[entity.Index] = prefabBase;

                        int resolvedIndex;
                        if (TryGetPrefabIndex(prefabBase, out resolvedIndex) && resolvedIndex >= 0)
                            _prefabByPrefabIndex[resolvedIndex] = prefabBase;
                    }
                }

                _defaultOneWayPrefab = FindExact("Small Road Oneway - 1 lane");
                _defaultTwoWayPrefab = FindExact("Small Road");

                if (_defaultOneWayPrefab == Entity.Null)
                    _defaultOneWayPrefab = FindDefault(true);

                if (_defaultTwoWayPrefab == Entity.Null)
                    _defaultTwoWayPrefab = FindDefault(false);

                if (_defaultOneWayPrefab == Entity.Null && _candidates.Count > 0)
                    _defaultOneWayPrefab = _candidates[0].Entity;

                if (_defaultTwoWayPrefab == Entity.Null && _candidates.Count > 0)
                    _defaultTwoWayPrefab = _candidates[0].Entity;

                Util.Log.Info(
                    "[RoadImport] RoadPrefabResolver initialized. candidates=" + _candidates.Count +
                    " defaultOneWay=" + GetPrefabName(_defaultOneWayPrefab) +
                    " defaultTwoWay=" + GetPrefabName(_defaultTwoWayPrefab)
                );

                return _candidates.Count > 0;
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] RoadPrefabResolver initialization failed: " + ex);
                return false;
            }
            finally
            {
                if (entities.IsCreated)
                    entities.Dispose();

                if (queryCreated)
                    query.Dispose();
            }
        }

        internal Entity ResolveForRoad(RoadImportMetadata meta)
        {
            if (meta == null)
                return ResolveDefault(false);

            return ResolveForRoad(meta.Highway, meta.TargetLaneCount, meta.Oneway, meta.Surface, meta.Maxspeed, meta.Bridge || meta.Tunnel);
        }

        internal Entity ResolveForRoad(string highway, int? lanes, bool oneWay)
        {
            return ResolveForRoad(highway, lanes, oneWay, "", "", false);
        }

        private Entity ResolveForRoad(string highway, int? lanes, bool oneWay, string surface, string maxspeed, bool elevatedOrTunnel)
        {
            if (_candidates.Count == 0)
                return Entity.Null;

            var category = NormalizeHighway(highway);
            var targetLanes = ResolveTargetLaneCount(category, lanes, oneWay);
            var targetSpeed = ResolveTargetSpeed(category, targetLanes, oneWay, maxspeed);
var wantsUnpaved = IsUnpavedSurface(surface);

Entity exact;
string exactName;

if (TryResolveObservedVanillaRoad(category, targetLanes, oneWay, wantsUnpaved, out exact, out exactName))
{
    LogExactPrefabResolution(category, targetLanes, oneWay, wantsUnpaved, exactName, exact);
    return exact;
}
            var bestScore = int.MaxValue;
            var best = Entity.Null;

            for (var i = 0; i < _candidates.Count; i++)
            {
                var score = ScoreCandidate(_candidates[i], category, targetLanes, targetSpeed, oneWay, wantsUnpaved, elevatedOrTunnel);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = _candidates[i].Entity;
                }
            }

if (best != Entity.Null)
{
    LogScoreFallbackResolution(category, targetLanes, oneWay, wantsUnpaved, best, bestScore);
    return best;
}

return ResolveDefault(oneWay);
        }

        internal Entity ResolveHighwayForConnection(bool oneWay, int? lanes)
        {
            if (_candidates.Count == 0)
                return Entity.Null;

            var targetLanes = lanes.HasValue && lanes.Value > 0
                ? Math.Min(5, Math.Max(1, lanes.Value))
                : (oneWay ? 3 : 2);

            Entity exact;
            string exactName;

            if (TryResolveObservedHighwayPrefab(targetLanes, oneWay, out exact, out exactName))
            {
                LogExactPrefabResolution("motorway-connection", targetLanes, oneWay, false, exactName, exact);
                return exact;
            }

            var targetSpeed = ResolveTargetSpeed("motorway", targetLanes, oneWay, "", false);
            var bestScore = int.MaxValue;
            var best = Entity.Null;

            for (var i = 0; i < _candidates.Count; i++)
            {
                var score = ScoreCandidate(_candidates[i], "motorway", targetLanes, targetSpeed, oneWay, false, false);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = _candidates[i].Entity;
                }
            }

            if (best != Entity.Null)
            {
                LogScoreFallbackResolution("motorway-connection", targetLanes, oneWay, false, best, bestScore);
                return best;
            }

            return ResolveDefault(oneWay);
        }

        internal Entity ResolveDefault(bool oneWay)
        {
            var preferred = oneWay ? _defaultOneWayPrefab : _defaultTwoWayPrefab;
            if (preferred != Entity.Null)
                return preferred;

            return _candidates.Count > 0 ? _candidates[0].Entity : Entity.Null;
        }

        internal string GetPrefabName(Entity prefabEntity)
        {
            if (prefabEntity == Entity.Null)
                return "<null>";

            string name;
            return _namesByEntityIndex.TryGetValue(prefabEntity.Index, out name) ? name : "<entity:" + prefabEntity.Index + ">";
        }

        internal PrefabBase GetPrefabBase(Entity prefabEntity)
        {
            if (prefabEntity == Entity.Null)
                return null;

            PrefabBase prefab;
            return _prefabByEntityIndex.TryGetValue(prefabEntity.Index, out prefab) ? prefab : null;
        }

        internal static string ResolvePrefabHint(RoadImportMetadata meta)
        {
            if (meta == null)
                return "unknown";

            if (meta.Roundabout)
                return "roundabout";

            var category = NormalizeHighway(meta.Highway);
            var targetLanes = ResolveTargetLaneCount(category, meta.TargetLaneCount, meta.Oneway);
            var wantsUnpaved = IsUnpavedSurface(meta.Surface);

            return ResolveObservedVanillaHint(category, targetLanes, meta.Oneway, wantsUnpaved);
        }

        private void CacheContentRoadPrefabs()
        {
            if (_prefabSystem == null)
                return;

            try
            {
                foreach (var contentPrefab in _prefabSystem.GetAvailableContentPrefabs())
                {
                    var prefab = contentPrefab as PrefabBase;

                    if (prefab == null || prefab.GetType().Name != "RoadPrefab")
                        continue;

                    int index;
                    if (TryGetPrefabIndex(prefab, out index) && index >= 0)
                        _prefabByPrefabIndex[index] = prefab;
                }

                Util.Log.Info("[RoadImport] Cached road content prefabs=" + _prefabByPrefabIndex.Count);
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] Could not enumerate content road prefabs: " + ex.Message);
            }
        }

        private PrefabBase ResolvePrefabBase(Entity entity, int prefabIndex)
        {
            var prefab = ResolvePrefabBaseFromPrefabSystemEntity(entity);

            if (prefab != null)
            {
                if (!_prefabByEntityIndex.ContainsKey(entity.Index))
                    _prefabByEntityIndex[entity.Index] = prefab;

                int resolvedIndex;
                if (TryGetPrefabIndex(prefab, out resolvedIndex) && resolvedIndex >= 0)
                    _prefabByPrefabIndex[resolvedIndex] = prefab;
                else if (prefabIndex >= 0)
                    _prefabByPrefabIndex[prefabIndex] = prefab;

                return prefab;
            }

            PrefabBase cached;
            return _prefabByPrefabIndex.TryGetValue(prefabIndex, out cached) ? cached : null;
        }

        private PrefabBase ResolvePrefabBaseFromPrefabSystemEntity(Entity entity)
        {
            if (_prefabSystem == null || entity == Entity.Null)
                return null;

            var systemType = _prefabSystem.GetType();
            var methods = systemType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];

                if (!string.Equals(method.Name, "TryGetPrefab", StringComparison.Ordinal))
                    continue;

                var parameters = method.GetParameters();

                if (parameters.Length != 2)
                    continue;

                if (parameters[0].ParameterType != typeof(Entity))
                    continue;

                if (!parameters[1].ParameterType.IsByRef)
                    continue;

                MethodInfo invokeMethod = method;

                if (method.IsGenericMethodDefinition)
                {
                    try
                    {
                        invokeMethod = method.MakeGenericMethod(typeof(PrefabBase));
                    }
                    catch
                    {
                        continue;
                    }
                }

                try
                {
                    object[] args = { entity, null };
                    var result = invokeMethod.Invoke(_prefabSystem, args);
                    var ok = result is bool && (bool)result;
                    var prefab = args[1] as PrefabBase;

                    if (ok && prefab != null)
                        return prefab;
                }
                catch
                {
                }
            }

            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];

                if (!string.Equals(method.Name, "GetPrefab", StringComparison.Ordinal))
                    continue;

                var parameters = method.GetParameters();

                if (parameters.Length != 1)
                    continue;

                if (parameters[0].ParameterType != typeof(Entity))
                    continue;

                MethodInfo invokeMethod = method;

                if (method.IsGenericMethodDefinition)
                {
                    try
                    {
                        invokeMethod = method.MakeGenericMethod(typeof(PrefabBase));
                    }
                    catch
                    {
                        continue;
                    }
                }

                try
                {
                    var prefab = invokeMethod.Invoke(_prefabSystem, new object[] { entity }) as PrefabBase;

                    if (prefab != null)
                        return prefab;
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool TryGetPrefabIndex(PrefabBase prefab, out int index)
        {
            index = -1;

            if (prefab == null)
                return false;

            var type = prefab.GetType();

            while (type != null)
            {
                var field = type.GetField("m_Index", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && TryConvertToInt(field.GetValue(prefab), out index))
                    return true;

                var property = type.GetProperty("index", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                    type.GetProperty("Index", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        if (TryConvertToInt(property.GetValue(prefab, null), out index))
                            return true;
                    }
                    catch
                    {
                    }
                }

                type = type.BaseType;
            }

            return false;
        }

        private static bool TryConvertToInt(object value, out int result)
        {
            result = -1;

            if (value == null)
                return false;

            try
            {
                result = Convert.ToInt32(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Entity FindExact(string name)
        {
            for (var i = 0; i < _candidates.Count; i++)
            {
                if (string.Equals(_candidates[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return _candidates[i].Entity;
            }

            return Entity.Null;
        }

        private Entity FindDefault(bool preferOneWay)
        {
            var targetLanes = preferOneWay ? 1 : 2;
            var targetSpeed = ResolveTargetSpeed("residential", targetLanes, preferOneWay, "", false);
            var bestScore = int.MaxValue;
            var best = Entity.Null;

            for (var i = 0; i < _candidates.Count; i++)
            {
                var score = ScoreCandidate(_candidates[i], "residential", targetLanes, targetSpeed, preferOneWay, false, false);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = _candidates[i].Entity;
                }
            }

            return best;
        }

private bool TryResolveObservedVanillaRoad(string category, int targetLanes, bool oneWay, bool wantsUnpaved, out Entity entity, out string matchedName)
{
    entity = Entity.Null;
    matchedName = null;

    var preferred = ResolveObservedVanillaHint(category, targetLanes, oneWay, wantsUnpaved);

    if (TryFindFirstExact(out entity, out matchedName, preferred))
        return true;

    if (wantsUnpaved || category == "track")
    {
        if (oneWay)
            return TryFindFirstExact(out entity, out matchedName, "Gravel Road Oneway", "Gravel Road", "Alley Oneway", "Small Road Oneway - 1 lane");

        return TryFindFirstExact(out entity, out matchedName, "Gravel Road", "Alley", "Small Road");
    }

    if (category == "motorway")
        return TryResolveObservedHighwayPrefab(targetLanes, oneWay, out entity, out matchedName);

    if (category == "link")
    {
        if (oneWay)
        {
            if (targetLanes <= 1)
                return TryFindFirstExact(out entity, out matchedName, "Highway Oneway - 1 lane", "Highway Oneway - 2 lanes");

            return TryFindFirstExact(out entity, out matchedName, "Highway Oneway - 2 lanes", "Highway Oneway - 1 lane", "Highway Oneway - 3 lanes");
        }

        return TryFindFirstExact(out entity, out matchedName, "Highway Twoway - 2 lanes", "Highway Twoway - 3 lanes");
    }

    if (category == "primary")
    {
        if (oneWay)
        {
            if (targetLanes >= 6)
                return TryFindFirstExact(out entity, out matchedName, "Large Road Oneway - 7 lanes", "Large Road Oneway", "Medium Road Oneway - 5 lanes", "Medium Road Oneway");

            if (targetLanes >= 4)
                return TryFindFirstExact(out entity, out matchedName, "Medium Road Oneway - 5 lanes", "Medium Road Oneway", "Small Road Oneway - 3 lanes");

            if (targetLanes >= 3)
                return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway - 3 lanes", "Small Road Oneway");

            if (targetLanes <= 1)
                return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway - 1 lane", "Small Road Oneway");

            return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway", "Small Road Oneway - 1 lane");
        }

        if (targetLanes >= 6)
            return TryFindFirstExact(out entity, out matchedName, "Large Road", "Large Road Divided", "Medium Road");

        if (targetLanes >= 4)
            return TryFindFirstExact(out entity, out matchedName, "Medium Road", "Medium Road Divided", "Large Road", "Small Road");

        return TryFindFirstExact(out entity, out matchedName, "Small Road", "Medium Road");
    }

    if (category == "secondary")
    {
        if (oneWay)
        {
            if (targetLanes >= 4)
                return TryFindFirstExact(out entity, out matchedName, "Medium Road Oneway", "Medium Road Oneway - 5 lanes", "Small Road Oneway - 3 lanes");

            if (targetLanes >= 3)
                return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway - 3 lanes", "Small Road Oneway");

            if (targetLanes <= 1)
                return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway - 1 lane", "Small Road Oneway");

            return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway", "Small Road Oneway - 1 lane");
        }

        if (targetLanes >= 4)
            return TryFindFirstExact(out entity, out matchedName, "Medium Road", "Medium Road Divided", "Small Road");

        return TryFindFirstExact(out entity, out matchedName, "Small Road", "Medium Road");
    }

    if (category == "tertiary")
    {
        if (oneWay)
        {
            if (targetLanes >= 3)
                return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway - 3 lanes", "Small Road Oneway");

            if (targetLanes <= 1)
                return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway - 1 lane", "Small Road Oneway");

            return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway", "Small Road Oneway - 1 lane");
        }

        if (targetLanes >= 4)
            return TryFindFirstExact(out entity, out matchedName, "Medium Road", "Small Road");

        return TryFindFirstExact(out entity, out matchedName, "Small Road", "Medium Road");
    }

    if (category == "service")
    {
        if (oneWay)
            return TryFindFirstExact(out entity, out matchedName, "Alley Oneway", "Small Road Oneway - 1 lane", "Small Road Oneway");

        return TryFindFirstExact(out entity, out matchedName, "Alley", "Small Road", "Gravel Road");
    }

    if (category == "living_street")
    {
        if (oneWay)
            return TryFindFirstExact(out entity, out matchedName, "Alley Oneway", "Pedestrian Street Small", "Small Road Oneway - 1 lane");

        return TryFindFirstExact(out entity, out matchedName, "Alley", "Pedestrian Street", "Small Road");
    }

    if (category == "residential" || category == "unclassified")
    {
        if (oneWay)
        {
            if (targetLanes >= 3)
                return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway - 3 lanes", "Small Road Oneway");

            if (targetLanes <= 1)
                return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway - 1 lane", "Small Road Oneway");

            return TryFindFirstExact(out entity, out matchedName, "Small Road Oneway", "Small Road Oneway - 1 lane");
        }

        return TryFindFirstExact(out entity, out matchedName, "Small Road", "Gravel Road");
    }

    return false;
}

private bool TryResolveObservedHighwayPrefab(int targetLanes, bool oneWay, out Entity entity, out string matchedName)
{
    entity = Entity.Null;
    matchedName = null;

    if (oneWay)
    {
        if (targetLanes <= 1)
            return TryFindFirstExact(out entity, out matchedName, "Highway Oneway - 1 lane", "Highway Oneway - 2 lanes");

        if (targetLanes == 2)
            return TryFindFirstExact(out entity, out matchedName, "Highway Oneway - 2 lanes", "Highway Oneway - 1 lane", "Highway Oneway - 3 lanes");

        if (targetLanes == 3)
            return TryFindFirstExact(out entity, out matchedName, "Highway Oneway - 3 lanes", "Highway Oneway - 2 lanes", "Highway Oneway - 4 lanes");

        if (targetLanes == 4)
            return TryFindFirstExact(out entity, out matchedName, "Highway Oneway - 4 lanes", "Highway Oneway - 3 lanes", "Highway Oneway - 5 lanes");

        return TryFindFirstExact(out entity, out matchedName, "Highway Oneway - 5 lanes", "Highway Oneway - 4 lanes", "Highway Oneway - 3 lanes");
    }

    if (targetLanes <= 2)
        return TryFindFirstExact(out entity, out matchedName, "Highway Twoway - 2 lanes", "Highway Twoway - 3 lanes");

    if (targetLanes == 3)
        return TryFindFirstExact(out entity, out matchedName, "Highway Twoway - 3 lanes", "Highway Twoway - 2 lanes", "Highway Twoway - 4 lanes");

    return TryFindFirstExact(out entity, out matchedName, "Highway Twoway - 4 lanes", "Highway Twoway - 3 lanes", "Highway Twoway - 2 lanes");
}

private bool TryFindFirstExact(out Entity entity, out string matchedName, params string[] names)
{
    entity = Entity.Null;
    matchedName = null;

    if (names == null)
        return false;

    for (var i = 0; i < names.Length; i++)
    {
        var name = names[i];

        if (string.IsNullOrWhiteSpace(name))
            continue;

        Entity found;

        if (_entityByExactName.TryGetValue(name, out found) && found != Entity.Null)
        {
            entity = found;
            matchedName = name;
            return true;
        }
    }

    return false;
}

private void LogExactPrefabResolution(string category, int targetLanes, bool oneWay, bool wantsUnpaved, string exactName, Entity entity)
{
    if (_loggedExactResolutionKeys.Count >= 200)
        return;

    var key = BuildResolutionLogKey(category, targetLanes, oneWay, wantsUnpaved);

    if (!_loggedExactResolutionKeys.Add(key))
        return;

    Util.Log.Info(
        "[RoadPrefabResolver] exact CS2 prefab: category=" + SafePrefabName(category) +
        ", lanes=" + targetLanes +
        ", oneway=" + oneWay +
        ", unpaved=" + wantsUnpaved +
        " -> prefab=" + SafePrefabName(exactName) +
        " entity=" + entity.Index
    );
}

private void LogScoreFallbackResolution(string category, int targetLanes, bool oneWay, bool wantsUnpaved, Entity entity, int score)
{
    if (_loggedScoreFallbackKeys.Count >= 200)
        return;

    var key = BuildResolutionLogKey(category, targetLanes, oneWay, wantsUnpaved);

    if (!_loggedScoreFallbackKeys.Add(key))
        return;

    Util.Log.Info(
        "[RoadPrefabResolver] score fallback: category=" + SafePrefabName(category) +
        ", lanes=" + targetLanes +
        ", oneway=" + oneWay +
        ", unpaved=" + wantsUnpaved +
        " -> prefab=" + GetPrefabName(entity) +
        " entity=" + entity.Index +
        " score=" + score
    );
}

private static string BuildResolutionLogKey(string category, int targetLanes, bool oneWay, bool wantsUnpaved)
{
    return
        SafePrefabName(category) + "|" +
        targetLanes + "|" +
        oneWay + "|" +
        wantsUnpaved;
}

private static string ResolveObservedVanillaHint(string category, int targetLanes, bool oneWay, bool wantsUnpaved)
{
    if (wantsUnpaved || category == "track")
        return oneWay ? "Gravel Road Oneway" : "Gravel Road";

    if (category == "motorway")
        return ResolveHighwayVanillaHint(targetLanes, oneWay);

    if (category == "link")
        return oneWay
            ? (targetLanes <= 1 ? "Highway Oneway - 1 lane" : "Highway Oneway - 2 lanes")
            : "Highway Twoway - 2 lanes";

    if (category == "primary")
    {
        if (oneWay)
        {
            if (targetLanes >= 6)
                return "Large Road Oneway";

            if (targetLanes >= 4)
                return "Medium Road Oneway";

            if (targetLanes >= 3)
                return "Small Road Oneway - 3 lanes";

            if (targetLanes <= 1)
                return "Small Road Oneway - 1 lane";

            return "Small Road Oneway";
        }

        if (targetLanes >= 6)
            return "Large Road";

        if (targetLanes >= 4)
            return "Medium Road";

        return "Small Road";
    }

    if (category == "secondary")
    {
        if (oneWay)
        {
            if (targetLanes >= 4)
                return "Medium Road Oneway";

            if (targetLanes >= 3)
                return "Small Road Oneway - 3 lanes";

            if (targetLanes <= 1)
                return "Small Road Oneway - 1 lane";

            return "Small Road Oneway";
        }

        return targetLanes >= 4 ? "Medium Road" : "Small Road";
    }

    if (category == "tertiary")
    {
        if (oneWay)
        {
            if (targetLanes >= 3)
                return "Small Road Oneway - 3 lanes";

            if (targetLanes <= 1)
                return "Small Road Oneway - 1 lane";

            return "Small Road Oneway";
        }

        return targetLanes >= 4 ? "Medium Road" : "Small Road";
    }

    if (category == "service")
        return oneWay ? "Alley Oneway" : "Alley";

    if (category == "living_street")
        return oneWay ? "Alley Oneway" : "Alley";

    if (category == "residential" || category == "unclassified")
    {
        if (oneWay)
        {
            if (targetLanes >= 3)
                return "Small Road Oneway - 3 lanes";

            if (targetLanes <= 1)
                return "Small Road Oneway - 1 lane";

            return "Small Road Oneway";
        }

        return "Small Road";
    }

    return oneWay ? "Small Road Oneway - 1 lane" : "Small Road";
}

private static string ResolveHighwayVanillaHint(int targetLanes, bool oneWay)
{
    if (oneWay)
    {
        if (targetLanes <= 1)
            return "Highway Oneway - 1 lane";

        if (targetLanes == 2)
            return "Highway Oneway - 2 lanes";

        if (targetLanes == 3)
            return "Highway Oneway - 3 lanes";

        if (targetLanes == 4)
            return "Highway Oneway - 4 lanes";

        return "Highway Oneway - 5 lanes";
    }

    if (targetLanes <= 2)
        return "Highway Twoway - 2 lanes";

    if (targetLanes == 3)
        return "Highway Twoway - 3 lanes";

    return "Highway Twoway - 4 lanes";
}

private static string NormalizeHighway(string highway)
        {
            var value = string.IsNullOrWhiteSpace(highway) ? "" : highway.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(value))
                return "residential";

            if (value.EndsWith("_link", StringComparison.OrdinalIgnoreCase))
                return "link";

            if (value == "trunk")
                return "motorway";

            if (value == "motorway")
                return "motorway";

            if (value == "primary")
                return "primary";

            if (value == "secondary")
                return "secondary";

            if (value == "tertiary")
                return "tertiary";

            if (value == "residential")
                return "residential";

            if (value == "unclassified")
                return "unclassified";

            if (value == "living_street")
                return "living_street";

            if (value == "service")
                return "service";

            if (value == "track")
                return "track";

            if (value == "road")
                return "residential";

            return "residential";
        }

        private static bool IsMotorwayFamily(string category)
        {
            return category == "motorway" || category == "trunk";
        }

        private static int ResolveTargetLaneCount(string category, int? lanes, bool oneWay)
        {
            if (lanes.HasValue && lanes.Value > 0)
                return Math.Min(6, Math.Max(1, lanes.Value));

            switch (category)
            {
                case "motorway":
                case "trunk":
                    return oneWay ? 3 : 4;

                case "link":
                    return oneWay ? 1 : 2;

                case "primary":
                    return oneWay ? 2 : 4;

                case "secondary":
                    return oneWay ? 2 : 3;

                case "tertiary":
                    return oneWay ? 2 : 2;

                case "service":
                case "living_street":
                case "track":
                    return oneWay ? 1 : 2;

                case "residential":
                case "unclassified":
                    return oneWay ? 1 : 2;

                default:
                    return oneWay ? 1 : 2;
            }
        }

        private static int GuessLaneCountFromName(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return 2;

            var name = prefabName.ToLowerInvariant();

if (name.Contains("8 lane") || name.Contains("8 lanes"))
    return 8;

if (name.Contains("7 lane") || name.Contains("7 lanes"))
    return 7;

if (name.Contains("6 lane") || name.Contains("6 lanes"))
    return 6;

if (name.Contains("5 lane") || name.Contains("5 lanes"))
    return 5;

if (name.Contains("4 lane") || name.Contains("4 lanes"))
    return 4;

            if (name.Contains("3 lane") || name.Contains("3 lanes"))
                return 3;

            if (name.Contains("2 lane") || name.Contains("2 lanes"))
                return 2;

            if (name.Contains("1 lane"))
                return 1;

if (name.Contains("xl road"))
    return 8;

if (name.Contains("large road"))
    return 6;

            if (name.Contains("medium road"))
                return 4;

            return 2;
        }

        private static bool GuessOneWayFromName(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return false;

            return prefabName.IndexOf("Oneway", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefabName.IndexOf("One Way", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefabName.IndexOf("One-Way", StringComparison.OrdinalIgnoreCase) >= 0;
        }


        private Candidate CreateCandidate(Entity entity, PrefabBase prefabBase, string prefabName, int curatedLanes, bool curatedOneWay, bool curatedServiceOnly)
        {
            var speed = 0.0;
            var flagsText = "";
            var hasZoning = false;
            var preferTrafficLights = false;
            var useHighwayRules = false;
            var defaultIsForward = false;

            if (_entityManager.Exists(entity) && _entityManager.HasComponent<Game.Prefabs.RoadData>(entity))
            {
                var roadData = _entityManager.GetComponentData<Game.Prefabs.RoadData>(entity);
                speed = (double)roadData.m_SpeedLimit;
                flagsText = roadData.m_Flags.ToString();
                hasZoning = roadData.m_ZoneBlockPrefab != Entity.Null || ContainsFlag(flagsText, "EnableZoning");
                preferTrafficLights = ContainsFlag(flagsText, "PreferTrafficLights");
                useHighwayRules = ContainsFlag(flagsText, "UseHighwayRules");
                defaultIsForward = ContainsFlag(flagsText, "DefaultIsForward");
            }

            var hasBridgeData = HasComponentByName(entity, "Game.Prefabs.BridgeData");
            var hasAuxiliaryNet = HasComponentByName(entity, "Game.Prefabs.AuxiliaryNet");
            var hasAssetPackElement = HasComponentByName(entity, "Game.Prefabs.AssetPackElement");
            var hasContentPrerequisite = HasComponentByName(entity, "Game.Prefabs.ContentPrerequisiteData");
            var isLocked = HasComponentByName(entity, "Game.Prefabs.Locked");

            var isHighway = useHighwayRules || prefabName.IndexOf("Highway", StringComparison.OrdinalIgnoreCase) >= 0;
            var isOneWay = curatedOneWay || GuessOneWayFromName(prefabName) || defaultIsForward;
            var serviceOnly = curatedServiceOnly || prefabName.IndexOf("Alley", StringComparison.OrdinalIgnoreCase) >= 0;
            var laneCount = curatedLanes > 0 ? curatedLanes : InferLaneCount(prefabName, speed, isHighway, hasZoning, preferTrafficLights, isOneWay);

            return new Candidate
            {
                Entity = entity,
                PrefabBase = prefabBase,
                Name = prefabName,
                LaneCount = laneCount,
                IsOneWay = isOneWay,
                ServiceOnly = serviceOnly,
                IsHighway = isHighway,
                SpeedLimit = speed,
                FlagsText = flagsText,
                HasZoning = hasZoning,
                PreferTrafficLights = preferTrafficLights,
                UseHighwayRules = useHighwayRules,
                DefaultIsForward = defaultIsForward,
                HasBridgeData = hasBridgeData,
                HasAuxiliaryNet = hasAuxiliaryNet,
                HasAssetPackElement = hasAssetPackElement,
                HasContentPrerequisite = hasContentPrerequisite,
                IsLocked = isLocked,
            };
        }

        private int ScoreCandidate(Candidate candidate, string category, int targetLanes, double targetSpeed, bool oneWay, bool wantsUnpaved, bool elevatedOrTunnel)
        {
            var score = 0;
            var motorway = IsMotorwayFamily(category);
            var link = category == "link";
            var urban = !motorway && !link;
            var arterial = category == "tertiary" || category == "secondary" || category == "primary";
            var local = category == "service" || category == "living_street" || category == "track" || category == "residential" || category == "unclassified";

            if (!elevatedOrTunnel && candidate.HasBridgeData)
                score += 5000;

            if (candidate.HasAuxiliaryNet)
                score += 8000;

            if (candidate.IsLocked)
                score += 10000;

            if (candidate.HasContentPrerequisite)
                score += 1500;

            if (candidate.HasAssetPackElement)
                score += 150;

            if (motorway)
            {
                if (!candidate.UseHighwayRules)
                    score += 5000;

                if (candidate.HasZoning)
                    score += 3500;

                if (candidate.PreferTrafficLights)
                    score += 800;

                if (candidate.SpeedLimit < 44.0)
                    score += 2500;

                if (candidate.SpeedLimit >= 54.0)
                    score -= 300;
            }
            else if (link)
            {
                if (!candidate.UseHighwayRules)
                    score += 4200;

                if (candidate.HasZoning)
                    score += 3200;

                if (candidate.PreferTrafficLights)
                    score += 900;

                if (!candidate.IsOneWay)
                    score += 900;

                if (candidate.LaneCount > 2)
                    score += 500;

                if (candidate.SpeedLimit < 40.0)
                    score += 1200;

                if (candidate.SpeedLimit > 56.0)
                    score += 350;
            }
            else
            {
                if (candidate.UseHighwayRules)
                    score += 4200;

                if (candidate.IsHighway)
                    score += 2600;

                if (!candidate.HasZoning && category != "service" && category != "track")
                    score += 850;
            }

            if (category == "service")
            {
                if (candidate.UseHighwayRules)
                    score += 4000;

                if (candidate.PreferTrafficLights)
                    score += 900;

                if (targetLanes <= 1)
                {
                    if (candidate.SpeedLimit > 17.0)
                        score += 850;
                }
                else
                {
                    if (candidate.SpeedLimit > 23.5)
                        score += 700;

                    if (candidate.SpeedLimit < 15.0)
                        score += 350;
                }

                if (candidate.HasZoning)
                    score -= 80;
            }
            else if (category == "track")
            {
                if (candidate.UseHighwayRules)
                    score += 5000;

                if (candidate.PreferTrafficLights)
                    score += 1000;

                if (candidate.SpeedLimit > 17.0)
                    score += 900;

                if (!candidate.HasZoning)
                    score -= 120;
            }
            else if (category == "living_street")
            {
                if (candidate.UseHighwayRules)
                    score += 5000;

                if (candidate.PreferTrafficLights)
                    score += 600;

                if (candidate.SpeedLimit > 17.0)
                    score += 850;

                if (candidate.SpeedLimit <= 12.0)
                    score -= 250;
            }
            else if (category == "residential" || category == "unclassified")
            {
                if (candidate.UseHighwayRules)
                    score += 5000;

                if (candidate.SpeedLimit > 23.5)
                    score += 650;

                if (candidate.SpeedLimit < 15.0 && targetLanes >= 2)
                    score += 300;

                if (candidate.PreferTrafficLights)
                    score += 180;

                if (candidate.HasZoning)
                    score -= 120;
            }
            else if (category == "tertiary")
            {
                if (candidate.UseHighwayRules)
                    score += 4500;

                if (candidate.SpeedLimit < 20.0)
                    score += 650;

                if (candidate.SpeedLimit > 30.0)
                    score += 350;

                if (candidate.PreferTrafficLights)
                    score -= 150;

                if (candidate.HasZoning)
                    score -= 80;
            }
            else if (category == "secondary")
            {
                if (candidate.UseHighwayRules)
                    score += 4200;

                if (!candidate.PreferTrafficLights)
                    score += 400;

                if (candidate.SpeedLimit < 24.0)
                    score += 750;

                if (candidate.SpeedLimit > 34.0)
                    score += 300;

                if (candidate.HasZoning)
                    score -= 60;
            }
            else if (category == "primary")
            {
                if (candidate.UseHighwayRules)
                    score += 3200;

                if (!candidate.PreferTrafficLights && !candidate.HasZoning)
                    score += 700;

                if (candidate.SpeedLimit < 26.0)
                    score += 900;

                if (candidate.SpeedLimit > 45.0)
                    score += 500;

                if (candidate.PreferTrafficLights)
                    score -= 180;
            }

            if (candidate.ServiceOnly && category != "service" && category != "living_street" && category != "track")
                score += 2200;

            if (oneWay)
            {
                if (candidate.IsOneWay)
                    score -= 350;
                else
                    score += 850;
            }
            else
            {
                if (candidate.IsOneWay)
                    score += 500;
                else
                    score -= 250;
            }

            score += Math.Abs(candidate.LaneCount - targetLanes) * 240;

            if (candidate.SpeedLimit > 0.1 && targetSpeed > 0.1)
                score += (int)(Math.Abs(candidate.SpeedLimit - targetSpeed) * 75.0);

            if (urban && candidate.HasZoning)
                score -= 100;

            if (arterial && candidate.PreferTrafficLights)
                score -= 140;

            if (local && candidate.PreferTrafficLights)
                score += 120;

            if (motorway && candidate.SpeedLimit >= 54.0)
                score -= 180;

            if (!wantsUnpaved && candidate.Name.IndexOf("gravel", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 2500;

            if (wantsUnpaved)
            {
                if (candidate.SpeedLimit <= 17.0 && !candidate.PreferTrafficLights && !candidate.UseHighwayRules)
                    score -= 200;

                if (candidate.UseHighwayRules)
                    score += 3500;
            }

            return score;
        }

        private bool HasComponentByName(Entity entity, string fullName)
        {
            if (entity == Entity.Null || !_entityManager.Exists(entity))
                return false;

            try
            {
                using (var types = _entityManager.GetComponentTypes(entity, Allocator.Temp))
                {
                    for (var i = 0; i < types.Length; i++)
                    {
                        var managedType = types[i].GetManagedType();
                        var name = managedType != null ? managedType.FullName : types[i].ToString();

                        if (string.Equals(name, fullName, StringComparison.Ordinal))
                            return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static int InferLaneCount(string prefabName, double speed, bool isHighway, bool hasZoning, bool preferTrafficLights, bool oneWay)
        {
            var fromName = GuessLaneCountFromName(prefabName);

            if (!string.IsNullOrWhiteSpace(prefabName) && !prefabName.StartsWith("Road[idx=", StringComparison.OrdinalIgnoreCase))
                return fromName;

            if (isHighway)
            {
                if (speed >= 65.0)
                    return 4;

                if (speed >= 54.0)
                    return 3;

                return oneWay ? 2 : 2;
            }

            if (speed >= 32.0)
                return 4;

            if (speed >= 26.0)
                return preferTrafficLights ? 3 : 2;

            if (speed >= 20.0)
                return 2;

            return oneWay ? 1 : 2;
        }

        private static double ResolveTargetSpeed(string category, int targetLanes, bool oneWay, string maxspeed, bool allowMaxspeed)
        {
            double parsed;

            if (allowMaxspeed && TryParseMaxspeedCs2Speed(maxspeed, out parsed) && parsed > 1.0)
                return Clamp(parsed, 11.11111, 66.66667);

            switch (category)
            {
                case "track":
                    return 16.66667; // 30 km/h

                case "service":
                    return oneWay || targetLanes <= 1 ? 16.66667 : 22.22222; // 30-40 km/h

                case "living_street":
                    return 11.11111; // 20 km/h

                case "residential":
                case "unclassified":
                    return targetLanes <= 1 ? 16.66667 : 22.22222; // 30-40 km/h

                case "tertiary":
                    return 22.22222; // 40 km/h

                case "secondary":
                    return 27.77778; // 50 km/h

                case "primary":
                    return targetLanes >= 4 ? 33.33334 : 27.77778; // 50-60 km/h

                case "link":
                    return targetLanes <= 1 ? 44.44445 : 55.55556; // 80-100 km/h

                case "motorway":
                case "trunk":
                    return targetLanes >= 4 ? 66.66667 : 55.55556; // 100-120 km/h

                default:
                    return 22.22222; // 40 km/h
            }
        }

        private static double ResolveTargetSpeed(string category, int targetLanes, bool oneWay, string maxspeed)
        {
            return ResolveTargetSpeed(category, targetLanes, oneWay, maxspeed, true);
        }

        private static bool TryParseMaxspeedCs2Speed(string maxspeed, out double cs2Speed)
        {
            cs2Speed = 0.0;

            if (string.IsNullOrWhiteSpace(maxspeed))
                return false;

            var text = maxspeed.Trim().ToLowerInvariant();

            if (text == "walk" || text == "signals" || text == "none" || text == "variable")
                return false;

            var numeric = new StringBuilder();

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];

                if ((ch >= '0' && ch <= '9') || ch == '.' || ch == ',')
                {
                    numeric.Append(ch == ',' ? '.' : ch);
                }
                else if (numeric.Length > 0)
                {
                    break;
                }
            }

            double value;

            if (!double.TryParse(numeric.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) || value <= 0)
                return false;

            // OSM maxspeed is usually km/h. CS2 RoadData.m_SpeedLimit values observed in-game are:
            // 20 km/h -> 11.11111
            // 30 km/h -> 16.66667
            // 40 km/h -> 22.22222
            // 50 km/h -> 27.77778
            // 60 km/h -> 33.33334
            // 80 km/h -> 44.44445
            // 100 km/h -> 55.55556
            // 120 km/h -> 66.66667
            // So internal speed = km/h / 1.8, not km/h / 3.6.
            if (text.IndexOf("mph", StringComparison.OrdinalIgnoreCase) >= 0)
                value *= 1.609344;

            cs2Speed = value / 1.8;
            return true;
        }

        private static bool IsUnpavedSurface(string surface)
        {
            if (string.IsNullOrWhiteSpace(surface))
                return false;

            var value = surface.Trim().ToLowerInvariant();
            return value.Contains("gravel") ||
                   value.Contains("unpaved") ||
                   value.Contains("ground") ||
                   value.Contains("dirt") ||
                   value.Contains("earth") ||
                   value.Contains("grass") ||
                   value.Contains("sand");
        }

        private static bool ContainsFlag(string flagsText, string flag)
        {
            return !string.IsNullOrWhiteSpace(flagsText) &&
                   flagsText.IndexOf(flag, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        private static string ClassifyCandidateFamily(Candidate candidate)
        {
            if (candidate.UseHighwayRules)
                return candidate.SpeedLimit >= 60.0 ? "highway-fast" : "highway";

            if (candidate.HasZoning && candidate.PreferTrafficLights)
                return candidate.SpeedLimit >= 32.0 ? "urban-arterial-large" : "urban-arterial";

            if (candidate.HasZoning)
                return candidate.SpeedLimit <= 16.7 ? "urban-local-slow" : "urban-local";

            if (candidate.PreferTrafficLights)
                return "nonzoned-arterial";

            return "road";
        }

        private int CacheRoadPrefabsFromPrefabSystemReflection(Dictionary<int, PrefabBase> output)
        {
            if (_prefabSystem == null || output == null)
                return 0;

            var before = output.Count;

            try
            {
                foreach (var contentPrefab in _prefabSystem.GetAvailableContentPrefabs())
                {
                    var prefab = contentPrefab as PrefabBase;
                    AddRoadPrefabToExactMap(output, prefab);
                }
            }
            catch
            {
            }

            try
            {
                var visited = new HashSet<object>(ReferenceObjectComparer.Instance);
                ScanObjectForRoadPrefabs(_prefabSystem, output, visited, 0, 0);
            }
            catch (Exception ex)
            {
                Util.Log.Info("[RoadPrefabNames] reflection scan failed: " + ex.Message);
            }

            return Math.Max(0, output.Count - before);
        }

        private static void ScanObjectForRoadPrefabs(object value, Dictionary<int, PrefabBase> output, HashSet<object> visited, int depth, int itemCount)
        {
            if (value == null || output == null || visited == null || depth > 5 || itemCount > 50000)
                return;

            var prefab = value as PrefabBase;
            if (prefab != null)
            {
                AddRoadPrefabToExactMap(output, prefab);
                return;
            }

            var type = value.GetType();

            if (IsSimpleReflectionObject(type))
                return;

            if (!type.IsValueType)
            {
                if (visited.Contains(value))
                    return;

                visited.Add(value);
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                var count = 0;

                foreach (var item in enumerable)
                {
                    if (count++ > 50000)
                        break;

                    ScanObjectForRoadPrefabs(item, output, visited, depth + 1, itemCount + count);
                }
            }

            FieldInfo[] fields;

            try
            {
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch
            {
                return;
            }

            for (var i = 0; i < fields.Length; i++)
            {
                object fieldValue;

                try
                {
                    fieldValue = fields[i].GetValue(value);
                }
                catch
                {
                    continue;
                }

                ScanObjectForRoadPrefabs(fieldValue, output, visited, depth + 1, itemCount + i);
            }
        }

        private static bool IsSimpleReflectionObject(Type type)
        {
            if (type == null)
                return true;

            if (type.IsPrimitive || type.IsEnum)
                return true;

            if (type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Type))
                return true;

            return false;
        }

        private static void AddRoadPrefabToExactMap(Dictionary<int, PrefabBase> output, PrefabBase prefab)
        {
            if (output == null || prefab == null)
                return;

            if (!string.Equals(prefab.GetType().Name, "RoadPrefab", StringComparison.OrdinalIgnoreCase))
                return;

            int index;
            if (TryGetPrefabIndex(prefab, out index) && index >= 0 && !output.ContainsKey(index))
                output[index] = prefab;
        }

        private string DumpComponentByFullName(Entity entity, string fullName, int maxFields)
        {
            if (entity == Entity.Null || !_entityManager.Exists(entity) || string.IsNullOrWhiteSpace(fullName))
                return "<missing>";

            try
            {
                using (var types = _entityManager.GetComponentTypes(entity, Allocator.Temp))
                {
                    for (var i = 0; i < types.Length; i++)
                    {
                        var managedType = types[i].GetManagedType();
                        if (managedType == null)
                            continue;

                        if (!string.Equals(managedType.FullName, fullName, StringComparison.Ordinal) &&
                            !string.Equals(managedType.Name, fullName, StringComparison.Ordinal))
                            continue;

                        var method = ResolveGetComponentDataMethod();
                        if (method == null)
                            return "<GetComponentData-not-found>";

                        var generic = method.MakeGenericMethod(managedType);
                        var value = generic.Invoke(_entityManager, new object[] { entity });
                        return DumpValueFields(value, maxFields);
                    }
                }

                return "<not-present>";
            }
            catch (Exception ex)
            {
                return "<dump-failed: " + ex.Message + ">";
            }
        }

        private static MethodInfo ResolveGetComponentDataMethod()
        {
            var methods = typeof(EntityManager).GetMethods(BindingFlags.Instance | BindingFlags.Public);

            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];

                if (!string.Equals(method.Name, "GetComponentData", StringComparison.Ordinal) || !method.IsGenericMethodDefinition)
                    continue;

                var parameters = method.GetParameters();

                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Entity))
                    return method;
            }

            return null;
        }

        private static string DumpObjectFields(object value, int maxFields)
        {
            if (value == null)
                return "<null>";

            try
            {
                var type = value.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var sb = new StringBuilder();
                var count = 0;

                sb.Append("type=");
                sb.Append(SafeDiagnosticValue(type.FullName));

                for (var i = 0; i < fields.Length && count < maxFields; i++)
                {
                    sb.Append("; ");
                    sb.Append(fields[i].Name);
                    sb.Append("=");
                    sb.Append(SafeDiagnosticValue(fields[i].GetValue(value)));
                    count++;
                }

                for (var i = 0; i < properties.Length && count < maxFields; i++)
                {
                    var property = properties[i];

                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                        continue;

                    object propertyValue;

                    try
                    {
                        propertyValue = property.GetValue(value, null);
                    }
                    catch
                    {
                        continue;
                    }

                    sb.Append("; ");
                    sb.Append(property.Name);
                    sb.Append("=");
                    sb.Append(SafeDiagnosticValue(propertyValue));
                    count++;
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "<object-dump-failed: " + ex.Message + ">";
            }
        }

        private sealed class ReferenceObjectComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceObjectComparer Instance = new ReferenceObjectComparer();

            public new bool Equals(object x, object y)
            {
                return object.ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static bool TryGetCuratedProfile(string prefabName, out CuratedRoadProfile profile)
        {
            profile = default(CuratedRoadProfile);

            if (string.IsNullOrWhiteSpace(prefabName))
                return false;

            for (var i = 0; i < CuratedRoadProfiles.Length; i++)
            {
                var candidate = CuratedRoadProfiles[i];

                if (string.Equals(prefabName, candidate.Name, StringComparison.OrdinalIgnoreCase))
                {
                    profile = candidate;
                    return true;
                }
            }

            return false;
        }

private static readonly CuratedRoadProfile[] CuratedRoadProfiles =
{
    new CuratedRoadProfile("Alley", 2, false, true),
    new CuratedRoadProfile("Alley Oneway", 2, true, true),
    new CuratedRoadProfile("Alley - Double Sided Parking", 2, false, true),
    new CuratedRoadProfile("Alley - Double Sided Parking Angled", 2, false, true),
    new CuratedRoadProfile("Alley Oneway - Double Sided Parking", 2, true, true),
    new CuratedRoadProfile("Alley Oneway - Double Sided Parking Angled", 2, true, true),

    new CuratedRoadProfile("Small Road", 2, false, false),
    new CuratedRoadProfile("Small Road Asymmetric", 3, false, false),
    new CuratedRoadProfile("Small Road - Double Sided Parking", 2, false, false),
    new CuratedRoadProfile("Small Road - Double Sided Parking Angled", 2, false, false),
    new CuratedRoadProfile("Small Road Divided - Double Sided Parking", 2, false, false),
    new CuratedRoadProfile("Small Road Divided - Double Sided Parking Angled", 2, false, false),
    new CuratedRoadProfile("Small Road Oneway", 2, true, false),
    new CuratedRoadProfile("Small Road Oneway - 1 lane", 1, true, false),
    new CuratedRoadProfile("Small Road Oneway - 3 lanes", 3, true, false),
    new CuratedRoadProfile("Small Road Oneway - Single Sided Parking", 2, true, false),
    new CuratedRoadProfile("Small Road Oneway - Single Sided Parking Angled", 2, true, false),

    new CuratedRoadProfile("Medium Road", 4, false, false),
    new CuratedRoadProfile("Medium Road Divided", 4, false, false),
    new CuratedRoadProfile("Medium Road Asymmetric", 4, false, false),
    new CuratedRoadProfile("Medium Road Asymmetric - 1+3 lanes", 4, false, false),
    new CuratedRoadProfile("Medium Road Asymmetric - 2+3 lanes", 5, false, false),
    new CuratedRoadProfile("Medium Road Oneway", 4, true, false),
    new CuratedRoadProfile("Medium Road Oneway - 5 lanes", 5, true, false),
    new CuratedRoadProfile("Medium Road - Double Sided Parking", 4, false, false),
    new CuratedRoadProfile("Medium Road - Double Sided Parking Angled", 4, false, false),

    new CuratedRoadProfile("Large Road", 6, false, false),
    new CuratedRoadProfile("Large Road Divided", 6, false, false),
    new CuratedRoadProfile("Large Road Asymmetric", 7, false, false),
    new CuratedRoadProfile("Large Road Asymmetric - 2+4 lanes", 6, false, false),
    new CuratedRoadProfile("Large Road Asymmetric - 2+5 lanes", 7, false, false),
    new CuratedRoadProfile("Large Road Oneway", 6, true, false),
    new CuratedRoadProfile("Large Road Oneway - 7 lanes", 7, true, false),

    new CuratedRoadProfile("XL Road Divided", 8, false, false),

    new CuratedRoadProfile("Gravel Road", 2, false, false),
    new CuratedRoadProfile("Gravel Road Oneway", 2, true, false),

    new CuratedRoadProfile("Pedestrian Street", 2, false, true),
    new CuratedRoadProfile("Pedestrian Street Small", 2, true, true),

    new CuratedRoadProfile("Highway Oneway - 1 lane", 1, true, false),
    new CuratedRoadProfile("Highway Oneway - 2 lanes", 2, true, false),
    new CuratedRoadProfile("Highway Oneway - 3 lanes", 3, true, false),
    new CuratedRoadProfile("Highway Oneway - 4 lanes", 4, true, false),
    new CuratedRoadProfile("Highway Oneway - 5 lanes", 5, true, false),

    new CuratedRoadProfile("Highway Twoway - 2 lanes", 2, false, false),
    new CuratedRoadProfile("Highway Twoway - 3 lanes", 3, false, false),
    new CuratedRoadProfile("Highway Twoway - 4 lanes", 4, false, false),
};

        private string DumpEntityComponents(Entity entity)
        {
            if (entity == Entity.Null || !_entityManager.Exists(entity))
                return "<missing>";

            try
            {
                using (var types = _entityManager.GetComponentTypes(entity, Allocator.Temp))
                {
                    var names = new List<string>();

                    for (var i = 0; i < types.Length; i++)
                    {
                        var managedType = types[i].GetManagedType();
                        names.Add(managedType != null ? managedType.FullName : types[i].ToString());
                    }

                    names.Sort(StringComparer.Ordinal);
                    return string.Join(" | ", names.ToArray());
                }
            }
            catch (Exception ex)
            {
                return "<component-dump-failed: " + ex.Message + ">";
            }
        }

        private static string DumpValueFields(object value, int maxFields)
        {
            if (value == null)
                return "<null>";

            try
            {
                var type = value.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var sb = new StringBuilder();
                var count = 0;

                for (var i = 0; i < fields.Length && count < maxFields; i++)
                {
                    if (count > 0)
                        sb.Append("; ");

                    sb.Append(fields[i].Name);
                    sb.Append("=");
                    sb.Append(SafeDiagnosticValue(fields[i].GetValue(value)));
                    count++;
                }

                for (var i = 0; i < properties.Length && count < maxFields; i++)
                {
                    var property = properties[i];

                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                        continue;

                    object propertyValue;

                    try
                    {
                        propertyValue = property.GetValue(value, null);
                    }
                    catch
                    {
                        continue;
                    }

                    if (count > 0)
                        sb.Append("; ");

                    sb.Append(property.Name);
                    sb.Append("=");
                    sb.Append(SafeDiagnosticValue(propertyValue));
                    count++;
                }

                if (count == 0)
                    return "<no-fields>";

                if (fields.Length + properties.Length > count)
                    sb.Append("; ...");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "<field-dump-failed: " + ex.Message + ">";
            }
        }

        private static string GuessFamilyFromName(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return "unknown";

            var name = prefabName.ToLowerInvariant();

            if (name.Contains("highway"))
                return "highway";

            if (name.Contains("large road"))
                return "large-road";

            if (name.Contains("medium road"))
                return "medium-road";

            if (name.Contains("small road"))
                return "small-road";

            if (name.Contains("alley"))
                return "service-or-alley";

            if (name.Contains("gravel"))
                return "gravel";

            return "road";
        }

        private static string SafePrefabName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Replace("\r", " ").Replace("\n", " ");
        }

        private static string SafeDiagnosticValue(object value)
        {
            if (value == null)
                return "<null>";

            var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            text = text.Replace("\r", " ").Replace("\n", " ");

            if (text.Length > 120)
                text = text.Substring(0, 120) + "...";

            return text;
        }

        private struct Candidate
        {
            public Entity Entity;
            public PrefabBase PrefabBase;
            public string Name;
            public int LaneCount;
            public bool IsOneWay;
            public bool ServiceOnly;
            public bool IsHighway;
            public double SpeedLimit;
            public string FlagsText;
            public bool HasZoning;
            public bool PreferTrafficLights;
            public bool UseHighwayRules;
            public bool DefaultIsForward;
            public bool HasBridgeData;
            public bool HasAuxiliaryNet;
            public bool HasAssetPackElement;
            public bool HasContentPrerequisite;
            public bool IsLocked;
        }

        private struct CuratedRoadProfile
        {
            public readonly string Name;
            public readonly int Lanes;
            public readonly bool OneWay;
            public readonly bool ServiceOnly;

            public CuratedRoadProfile(string name, int lanes, bool oneWay, bool serviceOnly)
            {
                Name = name;
                Lanes = lanes;
                OneWay = oneWay;
                ServiceOnly = serviceOnly;
            }
        }
    }
}
