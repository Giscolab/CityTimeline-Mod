using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CityTimelineMod.Roads
{
    public partial class RuntimeRoadToolSystem : ToolBaseSystem
    {
        public override string toolID => "CityTimelineMod.RuntimeRoadTool";

        private const int MaxFinalizeWaitFrames = 300;
        private const int MinCommitApplySettleFrames = 8;
        private const int MinCommittedEdgeStableFrames = 2;
        private const int DefaultRequestsPerFinalizeBatch = 256;
        private const int Earth2CitiesEmitPerFrame = 5000;
        private const int ConfirmedFastFlushCommitMultiplier = 4;
        private const int ConfirmedFastFlushMaxCommitRequests = 2048;

        private ToolOutputBarrier _toolOutputBarrier;
        private EntityQuery _tempQuery;
        private EntityQuery _tempIconQuery;
        private EntityQuery _tempNetCourseQuery;
        private EntityQuery _creationNetCourseQuery;
        private EntityQuery _runtimeCreationRequestQuery;
        private EntityQuery _roadEdgeQuery;
        private EntityQuery _nodeQuery;
        private PrefabBase _prefab;
        private RuntimeRoadSnapResolver _snapResolver;
        private float _runtimeRoadSnapToleranceMeters = 4f;

        private readonly List<RoadRequest> _queue = new List<RoadRequest>(1024);

        private int _readIndex;
        private int _pendingRequestCount;
        private int _baselineRoadEdgeCount;

        private HashSet<int> _preparedBaselineRoadEdgeIndices;
        private HashSet<int> _baselineRoadEdgeIndices = new HashSet<int>();

        private string _preparedImportKey = "";
        private string _activeImportKey = "";

        private bool _importBatchActive;
        private bool _waitingForTemp;
        private int _waitingForTempFrames;
        private int _currentCommitEmitted;
        private bool _commitApplying;
        private int _commitApplyWaitFrames;
        private int _currentCommitIndex;
        private int _currentCommitStartEdgeCount;
        private int _currentCommitAppliedEmitted;
        private int _commitLastCommittedEdgeProbe = -1;
        private int _commitCommittedEdgeStableFrames;
        private int _lastCommitDeltaEdges;
        private int _lastCommitBeforeEdges;
        private int _lastCommitAfterEdges;
        private int _totalCommitDeltaEdges;
        private bool _currentCommitBaselineCaptured;
        private int _lastCommitCleanedCreationRequests;
        private int _totalCleanedCreationRequests;
        private int _lastTempDiagnosticCommitIndex = -1;
        private bool _dumpTempDiagnosticsBeforeApply = false;
        private float _currentCommitMinX;
        private float _currentCommitMaxX;
        private float _currentCommitMinZ;
        private float _currentCommitMaxZ;
        private int _currentCommitInsideHeightmap14336;
        private int _currentCommitInsideWorld57344;
        private int _currentCommitOutsideWorld57344;
        private int _currentCommitSkippedDegenerateRequests;
        private int _maxRequestsPerFinalizeBatch = DefaultRequestsPerFinalizeBatch;
        private string _runtimeRoadImportPipelineMode = "confirmed-fast-flush";

        private bool IsLegacyFastFlushPipeline
        {
            get
            {
                return string.Equals(_runtimeRoadImportPipelineMode, "legacy-fast-flush", StringComparison.OrdinalIgnoreCase);
            }
        }

        private bool IsConfirmedFastFlushPipeline
        {
            get
            {
                return string.Equals(_runtimeRoadImportPipelineMode, "confirmed-fast-flush", StringComparison.OrdinalIgnoreCase);
            }
        }

        private int GetCurrentCommitLimit()
        {
            if (IsLegacyFastFlushPipeline)
                return int.MaxValue;

            var batchLimit = Math.Max(1, _maxRequestsPerFinalizeBatch);

            if (IsConfirmedFastFlushPipeline)
            {
                var requested = batchLimit * ConfirmedFastFlushCommitMultiplier;
                return Math.Min(ConfirmedFastFlushMaxCommitRequests, Math.Max(batchLimit, requested));
            }

            return batchLimit;
        }

        private bool IsCommitLimitBounded(int commitLimit)
        {
            return commitLimit > 0 && commitLimit < int.MaxValue;
        }

private int _lastLoggedQueueCount = -1;
private bool _lastLoggedWaitingForTemp;
private bool _hasLoggedRuntimeState;
private int _activeImportTotalQueued;
private int _activeImportEmitted;
private int _activeImportFinalized;
private int _activeImportBatchIndex;
private int _activeImportLastBatchSize;
private int _activeImportLastTaggedEdges;
private int _activeImportLastTaggedNodes;
private int _activeImportLastDeltaEdges;
private string _activeImportPhase = "idle";

        protected override void OnCreate()
        {
            base.OnCreate();

            _toolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            _tempQuery = GetEntityQuery(ComponentType.ReadOnly<Temp>());
            _tempIconQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Game.Notifications.Icon>()
            );
            _tempNetCourseQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<NetCourse>()
            );
            _creationNetCourseQuery = GetEntityQuery(
                ComponentType.ReadOnly<CreationDefinition>(),
                ComponentType.ReadOnly<NetCourse>()
            );
            _runtimeCreationRequestQuery = GetEntityQuery(
                ComponentType.ReadOnly<RuntimeRoadCreationRequestTag>(),
                ComponentType.ReadOnly<CreationDefinition>(),
                ComponentType.ReadOnly<NetCourse>()
            );
            _roadEdgeQuery = GetEntityQuery(
                ComponentType.ReadOnly<Edge>(),
                ComponentType.ReadOnly<Road>(),
                ComponentType.Exclude<Temp>()
            );
            _nodeQuery = GetEntityQuery(
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<Game.Objects.Transform>(),
                ComponentType.Exclude<Temp>()
            );
            _snapResolver = new RuntimeRoadSnapResolver(EntityManager);

            _queue.Clear();
            _readIndex = 0;
            _pendingRequestCount = 0;
            _waitingForTemp = false;
            _waitingForTempFrames = 0;
            _commitApplying = false;
            _commitApplyWaitFrames = 0;
            _commitLastCommittedEdgeProbe = -1;
            _commitCommittedEdgeStableFrames = 0;
            _currentCommitEmitted = 0;
            _currentCommitBaselineCaptured = false;

            base.applyMode = (ApplyMode)2;
            Enabled = true;

            Util.Log.Info("[RoadImport] RuntimeRoadToolSystem OnCreate.");
        }

        public override PrefabBase GetPrefab()
        {
            return _prefab;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            _prefab = prefab;
            return true;
        }

        public void QueueRoadEdge(Entity prefabEntity, PrefabBase prefabBase, float3 start, float3 end, string roadName = null)
        {
            if (prefabEntity == Entity.Null)
                return;

            StartImportBatchIfNeeded();

            _queue.Add(new RoadRequest
            {
                Prefab = prefabEntity,
                PrefabBase = prefabBase,
                Start = start,
                End = end,
                RoadName = roadName ?? ""
            });

_pendingRequestCount++;
_activeImportTotalQueued++;
_activeImportPhase = "queued";

            try
            {
                var toolSystem = World.GetExistingSystemManaged<ToolSystem>();

                if (toolSystem != null && toolSystem.activeTool != this)
                    toolSystem.activeTool = this;
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] Could not activate RuntimeRoadToolSystem: " + ex.Message);
            }

            if (_pendingRequestCount <= 5 || _pendingRequestCount % 10000 == 0)
            {
                Util.Log.Info(
                    "[RoadImport] queued runtime road edge. pending=" +
                    _pendingRequestCount +
                    " prefabEntity=" +
                    prefabEntity.Index
                );
            }
        }

        public void QueueTestRoad(Entity prefabEntity, float3 start, float3 end)
        {
            QueueRoadEdge(prefabEntity, null, start, end, "CityTimelineMod test road");
        }

        public void PrepareTaggedImportBatch(HashSet<int> baselineRoadEdgeIndices, string importKey)
        {
            _preparedBaselineRoadEdgeIndices = baselineRoadEdgeIndices != null
                ? new HashSet<int>(baselineRoadEdgeIndices)
                : new HashSet<int>();

            _preparedImportKey = importKey ?? "";

            Util.Log.Info(
                "[RoadImport] import batch prepared. key=" + SafeLog(_preparedImportKey) +
                ", baseline Edge/Road count=" + _preparedBaselineRoadEdgeIndices.Count
            );
        }

        public void ClearPreparedTaggedImportBatch(string importKey)
        {
            if (_preparedBaselineRoadEdgeIndices == null)
                return;

            if (!string.IsNullOrEmpty(importKey) &&
                !string.IsNullOrEmpty(_preparedImportKey) &&
                !string.Equals(importKey, _preparedImportKey, StringComparison.Ordinal))
            {
                return;
            }

            _preparedBaselineRoadEdgeIndices = null;
            _preparedImportKey = "";

            Util.Log.Info("[RoadImport] prepared import batch cleared without queued segments.");
        }

        public void SetMaxRequestsPerFinalizeBatch(int value)
        {
            var clamped = Mathf.Clamp(value, 1, 4096);

            if (_maxRequestsPerFinalizeBatch == clamped)
                return;

            _maxRequestsPerFinalizeBatch = clamped;

            Util.Log.Info("[RoadImport] RuntimeRoadToolSystem batch size set to " + _maxRequestsPerFinalizeBatch);
        }

        public void SetRuntimeRoadImportPipelineMode(string value)
        {
            var normalized = NormalizeRuntimeRoadImportPipelineMode(value);

            if (string.Equals(_runtimeRoadImportPipelineMode, normalized, StringComparison.Ordinal))
            {
                Util.Log.Info("[RoadImport] RuntimeRoadToolSystem pipelineMode=" + _runtimeRoadImportPipelineMode);
                return;
            }

            _runtimeRoadImportPipelineMode = normalized;

            Util.Log.Info("[RoadImport] RuntimeRoadToolSystem pipelineMode=" + _runtimeRoadImportPipelineMode);
        }

        public void SetRuntimeRoadSnapTolerance(float value)
        {
            _runtimeRoadSnapToleranceMeters = Mathf.Clamp(value, 0.25f, 20f);

            if (_snapResolver != null)
                _snapResolver.SetTolerances(_runtimeRoadSnapToleranceMeters, _runtimeRoadSnapToleranceMeters);

            Util.Log.Info(
                "[RoadImport] RuntimeRoadToolSystem snap tolerance set to " +
                _runtimeRoadSnapToleranceMeters.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            );
        }

        public JobHandle DriverUpdate(JobHandle inputDeps)
        {
            return OnUpdate(inputDeps);
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            LogRuntimeStateIfChanged("OnUpdate state changed");

            base.applyMode = (ApplyMode)2;

            if (!HasPendingWork)
            {
                if (_importBatchActive)
                {
                    FinishImportBatchIfNeeded();
                    LogRuntimeStateIfChanged("import finished and tagged");
                }

                return inputDeps;
            }

            if (_waitingForTemp)
            {
                base.applyMode = (ApplyMode)0;
                _waitingForTempFrames++;

                if (!_tempQuery.IsEmpty || _waitingForTempFrames >= MaxFinalizeWaitFrames)
                {
                    var forced = _waitingForTempFrames >= MaxFinalizeWaitFrames;

                    EnsureRuntimeToolActive("apply commit");
                    DumpTempDiagnosticsBeforeApply(_currentCommitIndex, 12);

                    base.applyMode = (ApplyMode)1;

                    _waitingForTemp = false;
                    _waitingForTempFrames = 0;
                    _commitApplying = true;
                    _commitApplyWaitFrames = 0;

                    _currentCommitAppliedEmitted = _currentCommitEmitted;

                    _activeImportFinalized += _currentCommitEmitted;
                    _activeImportLastBatchSize = _currentCommitEmitted;
                    _activeImportPhase = "earth2cities-commit-applyMode-1";

                    Util.Log.Info(
                        "[RoadImport] commit applyMode=1. commitIndex=" +
                        _currentCommitIndex +
                        ", commitEmitted=" +
                        _currentCommitEmitted +
                        ", totalEmitted=" +
                        _activeImportEmitted +
                        ", finalized=" +
                        _activeImportFinalized +
                        ", remaining=" +
                        _pendingRequestCount +
                        ", edgeCountBeforeCommit=" +
                        _currentCommitStartEdgeCount +
                        ", tempEmpty=" +
                        _tempQuery.IsEmpty +
                        ", forcedNoTemp=" +
                        forced +
                        ", boundsX=[" +
                        _currentCommitMinX.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                        "," +
                        _currentCommitMaxX.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                        "], boundsZ=[" +
                        _currentCommitMinZ.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                        "," +
                        _currentCommitMaxZ.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                        "], inside14336=" +
                        _currentCommitInsideHeightmap14336 +
                        ", inside57344=" +
                        _currentCommitInsideWorld57344 +
                        ", outside57344=" +
                        _currentCommitOutsideWorld57344
                    );

                    LogRuntimeStateIfChanged("commit applyMode=1 triggered");
                }

                return inputDeps;
            }

            if (_commitApplying)
            {
                // Do not clean the CreationDefinition/NetCourse requests immediately.
                // Small batch-safe commits often do not create Temp entities long enough to be observed here.
                // If we finalize on tempEmpty immediately, we delete the requests before the vanilla tool pipeline
                // has consumed them. Keep the apply phase alive until actual Edge/Road growth is observed,
                // the requests have disappeared after a short settle window, or a hard timeout is reached.
                base.applyMode = (ApplyMode)2;
                _commitApplyWaitFrames++;

                var afterEdgesProbe = GetRoadEdgeCountSafe();
                var beforeEdgesProbe = _currentCommitStartEdgeCount;
                var deltaEdgesProbe = afterEdgesProbe >= 0 && beforeEdgesProbe >= 0
                    ? afterEdgesProbe - beforeEdgesProbe
                    : -1;

                if (afterEdgesProbe == _commitLastCommittedEdgeProbe)
                    _commitCommittedEdgeStableFrames++;
                else
                {
                    _commitLastCommittedEdgeProbe = afterEdgesProbe;
                    _commitCommittedEdgeStableFrames = 1;
                }

                var runtimeRequestsRemaining = 0;
                var tempNetCourseRemaining = 0;
                try
                {
                    runtimeRequestsRemaining = _runtimeCreationRequestQuery.CalculateEntityCount();
                    tempNetCourseRemaining = _tempNetCourseQuery.CalculateEntityCount();
                }
                catch
                {
                    runtimeRequestsRemaining = -1;
                    tempNetCourseRemaining = -1;
                }

                // Count only committed Road/Edge entities here. The broad Edge/Road query can see
                // temporary preview roads while Temp is still present; starting the next commit at
                // that moment cancels the previous preview. A commit is finalized only after
                // non-Temp committed roads have appeared and the count is stable for a short window.
                var committedEdgeGrowthObserved = deltaEdgesProbe > 0;
                var committedEdgesSettled =
                    committedEdgeGrowthObserved &&
                    _commitApplyWaitFrames >= MinCommitApplySettleFrames &&
                    _commitCommittedEdgeStableFrames >= MinCommittedEdgeStableFrames;
                var forced = _commitApplyWaitFrames >= MaxFinalizeWaitFrames;

                if (!committedEdgesSettled && !forced)
                {
                    if (_commitApplyWaitFrames == 1 ||
                        _commitApplyWaitFrames == MinCommitApplySettleFrames ||
                        _commitApplyWaitFrames == 30 ||
                        _commitApplyWaitFrames == 60 ||
                        _commitApplyWaitFrames == 120)
                    {
                        Util.Log.Info(
                            "[RoadImport] commit apply waiting. commitIndex=" +
                            _currentCommitIndex +
                            ", waitFrames=" +
                            _commitApplyWaitFrames +
                            ", committedEdgeBefore=" +
                            beforeEdgesProbe +
                            ", committedEdgeNow=" +
                            afterEdgesProbe +
                            ", committedDelta=" +
                            deltaEdgesProbe +
                            ", stableFrames=" +
                            _commitCommittedEdgeStableFrames +
                            ", runtimeRequestsRemaining=" +
                            runtimeRequestsRemaining +
                            ", tempNetCourseRemaining=" +
                            tempNetCourseRemaining +
                            ", tempEmpty=" +
                            _tempQuery.IsEmpty
                        );
                    }

                    return inputDeps;
                }

                {
                    var afterEdges = afterEdgesProbe;
                    var beforeEdges = beforeEdgesProbe;
                    var deltaEdges = deltaEdgesProbe;

                    var cleanedRequests = CleanupRuntimeCreationRequests(
                        "commit finalized " + _currentCommitIndex
                    );

                    _lastCommitCleanedCreationRequests = cleanedRequests;
                    _totalCleanedCreationRequests += cleanedRequests;

                    _lastCommitBeforeEdges = beforeEdges;
                    _lastCommitAfterEdges = afterEdges;
                    _lastCommitDeltaEdges = deltaEdges;

                    if (deltaEdges > 0)
                        _totalCommitDeltaEdges += deltaEdges;

                    if (_snapResolver != null)
                    {
                        _snapResolver.LogStats(
                            _currentCommitIndex,
                            _currentCommitAppliedEmitted,
                            deltaEdges,
                            _currentCommitSkippedDegenerateRequests
                        );
                    }

                    Util.Log.Info(
                        "[RoadImport] commit finalized. commitIndex=" +
                        _currentCommitIndex +
                        ", commitEmitted=" +
                        _currentCommitAppliedEmitted +
                        ", edgeBefore=" +
                        _lastCommitBeforeEdges +
                        ", edgeAfter=" +
                        _lastCommitAfterEdges +
                        ", commitDeltaEdges=" +
                        _lastCommitDeltaEdges +
                        ", totalCommitDeltaEdges=" +
                        _totalCommitDeltaEdges +
                        ", cleanedRequests=" +
                        cleanedRequests +
                        ", totalCleanedRequests=" +
                        _totalCleanedCreationRequests +
                        ", totalEmitted=" +
                        _activeImportEmitted +
                        ", finalized=" +
                        _activeImportFinalized +
                        ", remaining=" +
                        _pendingRequestCount +
                        ", waitFrames=" +
                        _commitApplyWaitFrames +
                        ", runtimeRequestsRemainingBeforeCleanup=" +
                        runtimeRequestsRemaining +
                        ", tempEmpty=" +
                        _tempQuery.IsEmpty +
                        ", forced=" +
                        forced +
                        ", boundsX=[" +
                        _currentCommitMinX.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                        "," +
                        _currentCommitMaxX.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                        "], boundsZ=[" +
                        _currentCommitMinZ.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                        "," +
                        _currentCommitMaxZ.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                        "], inside14336=" +
                        _currentCommitInsideHeightmap14336 +
                        ", inside57344=" +
                        _currentCommitInsideWorld57344 +
                        ", outside57344=" +
                        _currentCommitOutsideWorld57344
                    );

                    _commitApplying = false;
                    _commitApplyWaitFrames = 0;
                    _commitLastCommittedEdgeProbe = -1;
                    _commitCommittedEdgeStableFrames = 0;
                    _currentCommitEmitted = 0;
                    _currentCommitAppliedEmitted = 0;
                    _currentCommitSkippedDegenerateRequests = 0;
                    _currentCommitBaselineCaptured = false;

                    base.applyMode = (ApplyMode)2;
                    EnsureRuntimeToolActive("commit finalized");

                    _activeImportPhase = _pendingRequestCount <= 0
                        ? "earth2cities-all-commits-finalized"
                        : "earth2cities-commit-finalized";

                    if (_pendingRequestCount <= 0)
                    {
                        FinishImportBatchIfNeeded();
                        LogRuntimeStateIfChanged("all commits finalized and tagged");
                    }
                    else
                    {
                        LogRuntimeStateIfChanged("commit finalized; continuing next commit");
                    }
                }

                return inputDeps;
            }

            if (_pendingRequestCount > 0)
            {
                EnsureRuntimeToolActive("emit commit");

                base.applyMode = (ApplyMode)0;

                if (!_currentCommitBaselineCaptured)
                {
                    _currentCommitIndex++;
                    _currentCommitStartEdgeCount = GetRoadEdgeCountSafe();
                    _currentCommitBaselineCaptured = true;
                    ResetCurrentCommitSpatialStats();
                    _commitLastCommittedEdgeProbe = _currentCommitStartEdgeCount;
                    _commitCommittedEdgeStableFrames = 0;

                    if (_snapResolver != null)
                    {
                        _snapResolver.SetTolerances(_runtimeRoadSnapToleranceMeters, _runtimeRoadSnapToleranceMeters);
                        _snapResolver.Refresh();
                        _snapResolver.ResetStats();
                    }

                    Util.Log.Info(
                        "[RoadImport] commit started. commitIndex=" +
                        _currentCommitIndex +
                        ", edgeCountBeforeCommit=" +
                        _currentCommitStartEdgeCount +
                        ", remainingBeforeCommit=" +
                        _pendingRequestCount
                    );
                }

                var ecb = _toolOutputBarrier.CreateCommandBuffer();
                var emittedThisFrame = 0;
                var commitLimit = GetCurrentCommitLimit();
                var boundedCommitLimit = IsCommitLimitBounded(commitLimit);

                while (_pendingRequestCount > 0 &&
                       _readIndex < _queue.Count &&
                       emittedThisFrame < Earth2CitiesEmitPerFrame &&
                       _currentCommitEmitted < commitLimit)
                {
                    var request = _queue[_readIndex++];
                    _pendingRequestCount--;

                    AccumulateCurrentCommitSpatialStats(request);

                    if (request.PrefabBase != null)
                        _prefab = request.PrefabBase;

                    if (!CreateEdgeRequest(ecb, request))
                        _currentCommitSkippedDegenerateRequests++;

                    emittedThisFrame++;
                    _currentCommitEmitted++;
                }

                if (_readIndex >= _queue.Count)
                {
                    _queue.Clear();
                    _readIndex = 0;
                }

                if (emittedThisFrame > 0)
                {
                    _activeImportBatchIndex++;
                    _activeImportEmitted += emittedThisFrame;

                    var allQueuedEmitted = _pendingRequestCount <= 0;
                    var reachedCommitLimit = boundedCommitLimit && _currentCommitEmitted >= commitLimit;
                    var transactionReadyForApply = allQueuedEmitted || reachedCommitLimit;

                    if (transactionReadyForApply)
                    {
                        _waitingForTemp = true;
                        _waitingForTempFrames = 0;
                        _activeImportPhase = allQueuedEmitted
                            ? _runtimeRoadImportPipelineMode + "-transaction-emitted"
                            : _runtimeRoadImportPipelineMode + "-commit-limit-emitted";
                    }
                    else
                    {
                        _activeImportPhase = _runtimeRoadImportPipelineMode + "-emitting-transaction";
                    }

                    if (_activeImportBatchIndex <= 5 || _activeImportBatchIndex % 10 == 0 || _waitingForTemp)
                    {
                        Util.Log.Info(
                            "[RoadImport] emitted chunk. commitIndex=" +
                            _currentCommitIndex +
                            ", emittedThisFrame=" +
                            emittedThisFrame +
                            ", transactionEmitted=" +
                            _currentCommitEmitted +
                            ", totalEmitted=" +
                            _activeImportEmitted +
                            ", remaining=" +
                            _pendingRequestCount +
                            ", commitLimit=" +
                            commitLimit +
                            ", reachedCommitLimit=" +
                            reachedCommitLimit +
                            ", transactionReadyForApply=" +
                            transactionReadyForApply
                        );
                    }

                    LogRuntimeStateIfChanged("emitting commit");
                }

                return inputDeps;
            }

            return inputDeps;
        }

        private bool HasPendingWork
        {
            get { return _pendingRequestCount > 0 || _waitingForTemp || _commitApplying; }
        }

        private void EnsureRuntimeToolActive(string reason)
        {
            try
            {
                var toolSystem = World.GetExistingSystemManaged<ToolSystem>();

                if (toolSystem != null && toolSystem.activeTool != this)
                {
                    toolSystem.activeTool = this;

                    Util.Log.Info(
                        "[RoadImport] RuntimeRoadToolSystem forced active tool. reason=" +
                        SafeLog(reason)
                    );
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[RoadImport] failed to force RuntimeRoadToolSystem active. reason=" +
                    SafeLog(reason) +
                    ", error=" +
                    ex.Message
                );
            }
        }

        private bool CreateEdgeRequest(EntityCommandBuffer ecb, RoadRequest request)
        {
            var requestedStart = request.Start;
            var requestedEnd = request.End;

            var startSnap = _snapResolver != null
                ? _snapResolver.Resolve(requestedStart)
                : RuntimeRoadSnapTarget.None(requestedStart);

            var endSnap = _snapResolver != null
                ? _snapResolver.Resolve(requestedEnd)
                : RuntimeRoadSnapTarget.None(requestedEnd);

            var startPos = startSnap.Position;
            var endPos = endSnap.Position;

            var length = math.distance(startPos, endPos);

            if (length <= 0.01f)
                return false;

            var start = BuildTransform(startPos, endPos);
            var end = BuildTransform(endPos, startPos);

            var dir = math.normalizesafe(endPos - startPos, new float3(1f, 0f, 0f));
            var tangent = dir * (length / 3f);

            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, new CreationDefinition
            {
                m_Prefab = request.Prefab,
                m_SubPrefab = Entity.Null,
                m_Owner = Entity.Null,
                m_Original = Entity.Null,
                m_Attached = Entity.Null,
                m_RandomSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                m_Flags = (CreationFlags)65536
            });

            ecb.AddComponent(entity, new NetCourse
            {
                m_StartPosition = BuildCoursePos(start, 0f, BuildStartCoursePosFlags(startSnap), startSnap),
                m_EndPosition = BuildCoursePos(end, 1f, BuildEndCoursePosFlags(endSnap), endSnap),
                m_Curve = new Bezier4x3(startPos, startPos + tangent, endPos - tangent, endPos),
                m_Elevation = new float2(startPos.y, endPos.y),
                m_Length = length,
                m_FixedIndex = -1
            });

            ecb.AddComponent<OwnerDefinition>(entity, default(OwnerDefinition));
            ecb.AddComponent<Updated>(entity, default(Updated));
            return true;
        }

        private static Game.Objects.Transform BuildTransform(float3 position, float3 lookAt)
        {
            var forward = math.normalizesafe(
                new float3(lookAt.x - position.x, 0f, lookAt.z - position.z),
                new float3(0f, 0f, 1f)
            );

            if (math.lengthsq(forward) < 0.000001f)
                forward = new float3(0f, 0f, 1f);

            return new Game.Objects.Transform
            {
                m_Position = position,
                m_Rotation = quaternion.LookRotationSafe(forward, math.up())
            };
        }


        private static CoursePosFlags BuildStartCoursePosFlags(RuntimeRoadSnapTarget snap)
        {
            var flags = CoursePosFlags.IsRight | CoursePosFlags.IsLeft;

            if (snap.HasTarget)
                flags |= CoursePosFlags.FreeHeight;
            else
                flags |= CoursePosFlags.IsFirst;

            return flags;
        }

        private static CoursePosFlags BuildEndCoursePosFlags(RuntimeRoadSnapTarget snap)
        {
            var flags = CoursePosFlags.IsRight | CoursePosFlags.IsLeft;

            if (snap.HasTarget)
                flags |= CoursePosFlags.FreeHeight;
            else
                flags |= CoursePosFlags.IsLast;

            return flags;
        }

        private static CoursePos BuildCoursePos(
            Game.Objects.Transform transform,
            float courseDelta,
            CoursePosFlags flags,
            RuntimeRoadSnapTarget snap
        )
        {
            return new CoursePos
            {
                m_Entity = snap.HasTarget ? snap.Entity : Entity.Null,
                m_Position = transform.m_Position,
                m_Rotation = transform.m_Rotation,
                m_Elevation = new float2(transform.m_Position.y, transform.m_Position.y),
                m_CourseDelta = courseDelta,
                m_SplitPosition = snap.HasTarget ? snap.SplitPosition : 0f,
                m_Flags = flags,
                m_ParentMesh = -1
            };
        }

        public string GetRuntimeImportProgressText()
        {
            return
                "Progression : phase=" + SafeLog(_activeImportPhase) +
                " | pipeline=" + SafeLog(_runtimeRoadImportPipelineMode) +
                " | queued=" + _activeImportTotalQueued +
                " | emitted=" + _activeImportEmitted +
                " | finalized=" + _activeImportFinalized +
                " | pending=" + _pendingRequestCount +
                " | commitIndex=" + _currentCommitIndex +
                " | transactionEmitted=" + _currentCommitEmitted +
                " | chunks=" + _activeImportBatchIndex +
                " | waiting=" + _waitingForTemp +
                " | applying=" + _commitApplying +
                " | tempEmpty=" + _tempQuery.IsEmpty +
                " | lastCommitDeltaEdges=" + _lastCommitDeltaEdges +
                " | totalCommitDeltaEdges=" + _totalCommitDeltaEdges +
                " | skippedDegenerate=" + _currentCommitSkippedDegenerateRequests +
                " | cleanedRequests=" + _totalCleanedCreationRequests +
                " | lastCleaned=" + _lastCommitCleanedCreationRequests +
                " | taggedEdges=" + _activeImportLastTaggedEdges +
                " | taggedNodes=" + _activeImportLastTaggedNodes +
                " | deltaEdges=" + _activeImportLastDeltaEdges;
        }

        private void StartImportBatchIfNeeded()
        {
            if (_importBatchActive)
                return;

            _importBatchActive = true;
            _activeImportKey = _preparedImportKey ?? "";

            if (_preparedBaselineRoadEdgeIndices != null)
            {
                _baselineRoadEdgeIndices = new HashSet<int>(_preparedBaselineRoadEdgeIndices);
                _preparedBaselineRoadEdgeIndices = null;
                _preparedImportKey = "";
            }
            else
            {
                _baselineRoadEdgeIndices = CaptureCurrentRoadEdgeIndices();
            }

            _baselineRoadEdgeCount = _baselineRoadEdgeIndices.Count;
            _activeImportTotalQueued = 0;
            _activeImportEmitted = 0;
            _activeImportFinalized = 0;
            _activeImportBatchIndex = 0;
            _activeImportLastBatchSize = 0;
            _activeImportLastTaggedEdges = 0;
            _activeImportLastTaggedNodes = 0;
            _activeImportLastDeltaEdges = 0;
            _activeImportPhase = "started";

            _waitingForTemp = false;
            _waitingForTempFrames = 0;
            _currentCommitEmitted = 0;
            _commitApplying = false;
            _commitApplyWaitFrames = 0;
            _commitLastCommittedEdgeProbe = -1;
            _commitCommittedEdgeStableFrames = 0;
            _currentCommitIndex = 0;
            _currentCommitStartEdgeCount = 0;
            _currentCommitAppliedEmitted = 0;
            _currentCommitSkippedDegenerateRequests = 0;
            _lastCommitDeltaEdges = 0;
            _lastCommitBeforeEdges = 0;
            _lastCommitAfterEdges = 0;
            _totalCommitDeltaEdges = 0;
            _currentCommitBaselineCaptured = false;
            _lastCommitCleanedCreationRequests = 0;
            _totalCleanedCreationRequests = 0;
            _lastTempDiagnosticCommitIndex = -1;
            ResetCurrentCommitSpatialStats();
            CleanupRuntimeCreationRequests("start import stale cleanup");

            Util.Log.Info(
                "[RoadImport] import batch tracking started. key=" + SafeLog(_activeImportKey) +
                ", baseline Edge/Road count=" + _baselineRoadEdgeCount
            );
        }

        private void FinishImportBatchIfNeeded()
        {
            if (!_importBatchActive)
                return;

            _importBatchActive = false;

            var current = 0;
            var taggedEdges = 0;
            var taggedNodes = 0;
            var deltaEdges = 0;

            try
            {
                using (var roadEdges = _roadEdgeQuery.ToEntityArray(Allocator.Temp))
                {
                    current = roadEdges.Length;
                    var delta = current - _baselineRoadEdgeCount;
                    deltaEdges = delta;

                    if (delta > 0)
                        TagImportedEntitiesFromDelta(roadEdges, out taggedEdges, out taggedNodes);
                    else
                        Util.Log.Info("[RoadImport] tag skipped: delta Edge/Road <= 0.");
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] Could not finalize import batch tracking: " + ex.Message);
                return;
            }

            Util.Log.Info(
                "[RoadImport] import batch tracking finished. key=" + SafeLog(_activeImportKey) +
                ", baseline=" + _baselineRoadEdgeCount +
                ", current=" + current +
                ", delta=" + (current - _baselineRoadEdgeCount) +
                ", taggedEdges=" + taggedEdges +
                ", taggedNodes=" + taggedNodes
            );
            _activeImportLastDeltaEdges = deltaEdges;
            _activeImportLastTaggedEdges = taggedEdges;
            _activeImportLastTaggedNodes = taggedNodes;
            _activeImportPhase = "finished";

            _baselineRoadEdgeIndices.Clear();
            _activeImportKey = "";
        }

        private void ResetCurrentCommitSpatialStats()
        {
            _currentCommitMinX = float.MaxValue;
            _currentCommitMaxX = float.MinValue;
            _currentCommitMinZ = float.MaxValue;
            _currentCommitMaxZ = float.MinValue;
            _currentCommitInsideHeightmap14336 = 0;
            _currentCommitInsideWorld57344 = 0;
            _currentCommitOutsideWorld57344 = 0;
            _currentCommitSkippedDegenerateRequests = 0;
        }

        private void AccumulateCurrentCommitSpatialStats(RoadRequest request)
        {
            AccumulatePoint(request.Start);
            AccumulatePoint(request.End);

            if (IsSegmentInsideSquare(request.Start, request.End, 7168f))
                _currentCommitInsideHeightmap14336++;

            if (IsSegmentInsideSquare(request.Start, request.End, 28672f))
                _currentCommitInsideWorld57344++;
            else
                _currentCommitOutsideWorld57344++;
        }

        private void AccumulatePoint(float3 p)
        {
            if (p.x < _currentCommitMinX) _currentCommitMinX = p.x;
            if (p.x > _currentCommitMaxX) _currentCommitMaxX = p.x;
            if (p.z < _currentCommitMinZ) _currentCommitMinZ = p.z;
            if (p.z > _currentCommitMaxZ) _currentCommitMaxZ = p.z;
        }

        private static bool IsSegmentInsideSquare(float3 start, float3 end, float halfSize)
        {
            return
                IsPointInsideSquare(start, halfSize) &&
                IsPointInsideSquare(end, halfSize);
        }

        private static bool IsPointInsideSquare(float3 p, float halfSize)
        {
            return
                p.x >= -halfSize &&
                p.x <= halfSize &&
                p.z >= -halfSize &&
                p.z <= halfSize;
        }

        private void DumpTempDiagnosticsBeforeApply(int commitIndex, int maxEntities = 12)
        {
            if (!_dumpTempDiagnosticsBeforeApply)
                return;

            if (_lastTempDiagnosticCommitIndex == commitIndex)
                return;

            _lastTempDiagnosticCommitIndex = commitIndex;

            try
            {
                using (var temps = _tempQuery.ToEntityArray(Allocator.Temp))
                {
                    var total = temps.Length;
                    var limit = Math.Min(maxEntities, total);

                    Util.Log.Info(
                        "[RoadImportTempDiag] commitIndex=" +
                        commitIndex +
                        ", tempEntities=" +
                        total +
                        ", logging=" +
                        limit
                    );

                    Util.Log.Info(
                        "[RoadImportTempDiag] focused counts. commitIndex=" +
                        commitIndex +
                        ", tempTotal=" +
                        _tempQuery.CalculateEntityCount() +
                        ", tempIcons=" +
                        _tempIconQuery.CalculateEntityCount() +
                        ", tempNetCourse=" +
                        _tempNetCourseQuery.CalculateEntityCount() +
                        ", runtimeCreationRequests=" +
                        _runtimeCreationRequestQuery.CalculateEntityCount() +
                        ", creationNetCourse=" +
                        _creationNetCourseQuery.CalculateEntityCount()
                    );

                    for (var i = 0; i < limit; i++)
                    {
                        var entity = temps[i];

                        if (!EntityManager.Exists(entity))
                        {
                            Util.Log.Info("[RoadImportTempDiag] temp#" + i + " entity=" + entity.Index + " exists=False");
                            continue;
                        }

                        var tempText = "<no Temp>";

                        try
                        {
                            if (EntityManager.HasComponent<Temp>(entity))
                            {
                                var temp = EntityManager.GetComponentData<Temp>(entity);
                                tempText =
                                    "flags=" + temp.m_Flags +
                                    ", original=" + FormatEntity(temp.m_Original);
                            }
                        }
                        catch (Exception ex)
                        {
                            tempText = "<Temp read failed: " + ex.Message + ">";
                        }

                        Util.Log.Info(
                            "[RoadImportTempDiag] temp#" +
                            i +
                            " entity=" +
                            FormatEntity(entity) +
                            " " +
                            tempText +
                            " components=" +
                            DumpComponentTypesLimited(entity, 40)
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImportTempDiag] failed: " + ex);
            }
        }

        private string DumpComponentTypesLimited(Entity entity, int maxTypes)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
                return "Entity.Null or missing";

            try
            {
                using (var types = EntityManager.GetComponentTypes(entity, Allocator.Temp))
                {
                    var names = new List<string>();

                    for (var i = 0; i < types.Length && i < maxTypes; i++)
                    {
                        var managedType = types[i].GetManagedType();
                        names.Add(managedType != null ? managedType.FullName : types[i].ToString());
                    }

                    names.Sort(StringComparer.Ordinal);

                    var text = string.Join(" | ", names);

                    if (types.Length > maxTypes)
                        text += " | ... +" + (types.Length - maxTypes);

                    return text;
                }
            }
            catch (Exception ex)
            {
                return "<component dump failed: " + ex.Message + ">";
            }
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null ? "Entity.Null" : "#" + entity.Index;
        }

        private int CleanupRuntimeCreationRequests(string reason)
        {
            try
            {
                using (var entities = _runtimeCreationRequestQuery.ToEntityArray(Allocator.Temp))
                {
                    if (entities.Length <= 0)
                        return 0;

                    var removedRuntimeTags = 0;
                    var skippedMissing = 0;

                    for (var i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];

                        if (!EntityManager.Exists(entity))
                        {
                            skippedMissing++;
                            continue;
                        }

                        // Do not destroy CreationDefinition/NetCourse entities during runtime import.
                        // In CS2's tool pipeline these entities may still own or drive the temporary
                        // road preview for a few frames. Destroying them between batch-safe commits
                        // makes the previous batch disappear before it becomes a committed network.
                        if (EntityManager.HasComponent<RuntimeRoadCreationRequestTag>(entity))
                        {
                            EntityManager.RemoveComponent<RuntimeRoadCreationRequestTag>(entity);
                            removedRuntimeTags++;
                        }
                    }

                    Util.Log.Info(
                        "[RoadImport] cleaned runtime creation request markers. reason=" +
                        SafeLog(reason) +
                        ", matched=" +
                        entities.Length +
                        ", removedRuntimeTags=" +
                        removedRuntimeTags +
                        ", skippedMissing=" +
                        skippedMissing +
                        ", destroyedRequests=0"
                    );

                    return removedRuntimeTags;
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[RoadImport] failed to clean runtime creation request markers. reason=" +
                    SafeLog(reason) +
                    ", error=" +
                    ex
                );

                return 0;
            }
        }

        private bool IsCreatedNetworkEntity(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
                return false;

            if (EntityManager.HasComponent<Deleted>(entity))
                return false;

            return
                EntityManager.HasComponent<Edge>(entity) ||
                EntityManager.HasComponent<Node>(entity) ||
                EntityManager.HasComponent<Road>(entity) ||
                EntityManager.HasComponent<Curve>(entity);
        }

        private int GetRoadEdgeCountSafe()
        {
            try
            {
                return _roadEdgeQuery.CalculateEntityCount();
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] failed to count Road/Edge entities: " + ex.Message);
                return -1;
            }
        }

        private HashSet<int> CaptureCurrentRoadEdgeIndices()
        {
            var result = new HashSet<int>();

            try
            {
                using (var entities = _roadEdgeQuery.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < entities.Length; i++)
                        result.Add(entities[i].Index);
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] Could not capture baseline road edge entities: " + ex.Message);
            }

            return result;
        }

        private void TagImportedEntitiesFromDelta(NativeArray<Entity> roadEdges, out int taggedEdges, out int taggedNodes)
        {
            taggedEdges = 0;
            taggedNodes = 0;

            var taggedNodeIndices = new HashSet<int>();

            for (var i = 0; i < roadEdges.Length; i++)
            {
                var edgeEntity = roadEdges[i];

                if (_baselineRoadEdgeIndices.Contains(edgeEntity.Index))
                    continue;

                if (!EntityManager.Exists(edgeEntity) || !EntityManager.HasComponent<Edge>(edgeEntity))
                    continue;

                try
                {
                    if (!EntityManager.HasComponent<ImportedRoadTag>(edgeEntity))
                    {
                        EntityManager.AddComponent<ImportedRoadTag>(edgeEntity);
                        taggedEdges++;
                    }

                    var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                    taggedNodes += TryTagImportedNode(edge.m_Start, taggedNodeIndices);
                    taggedNodes += TryTagImportedNode(edge.m_End, taggedNodeIndices);
                }
                catch (Exception ex)
                {
                    Util.Log.Error("[RoadImport] Failed to tag imported edge entity=" + edgeEntity.Index + ": " + ex.Message);
                }
            }
        }

        private int TryTagImportedNode(Entity nodeEntity, HashSet<int> taggedNodeIndices)
        {
            if (nodeEntity == Entity.Null ||
                taggedNodeIndices.Contains(nodeEntity.Index) ||
                !EntityManager.Exists(nodeEntity) ||
                !EntityManager.HasComponent<Node>(nodeEntity))
            {
                return 0;
            }

            try
            {
                taggedNodeIndices.Add(nodeEntity.Index);

                if (!EntityManager.HasComponent<ImportedRoadTag>(nodeEntity))
                {
                    EntityManager.AddComponent<ImportedRoadTag>(nodeEntity);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] Failed to tag imported node entity=" + nodeEntity.Index + ": " + ex.Message);
            }

            return 0;
        }

        private Entity FindExistingNodeNear(float3 position, float toleranceMeters)
        {
            if (_nodeQuery.IsEmpty)
                return Entity.Null;

            var best = Entity.Null;
            var bestDistanceSq = toleranceMeters * toleranceMeters;

            try
            {
                using (var nodes = _nodeQuery.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < nodes.Length; i++)
                    {
                        var node = nodes[i];

                        if (!EntityManager.Exists(node) || !EntityManager.HasComponent<Game.Objects.Transform>(node))
                            continue;

                        var transform = EntityManager.GetComponentData<Game.Objects.Transform>(node);
                        var delta = transform.m_Position - position;
                        var horizontalSq = delta.x * delta.x + delta.z * delta.z;

                        if (horizontalSq <= bestDistanceSq)
                        {
                            bestDistanceSq = horizontalSq;
                            best = node;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] FindExistingNodeNear failed: " + ex.Message);
                return Entity.Null;
            }

            return best;
        }

        private void LogRuntimeStateIfChanged(string reason)
        {
            var queueCount = _queue.Count;

            if (_hasLoggedRuntimeState &&
                _lastLoggedQueueCount == queueCount &&
                _lastLoggedWaitingForTemp == _waitingForTemp)
            {
                return;
            }

            _hasLoggedRuntimeState = true;
            _lastLoggedQueueCount = queueCount;
            _lastLoggedWaitingForTemp = _waitingForTemp;

            Util.Log.Info(
                "[RoadImport] RuntimeRoadToolSystem " + reason +
                ". queue=" + queueCount +
                " pending=" + _pendingRequestCount +
                " waitingForTemp=" + _waitingForTemp
            );
        }

        private static string NormalizeRuntimeRoadImportPipelineMode(string value)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "confirmed-fast-flush"
                : value.Trim().ToLowerInvariant().Replace("_", "-");

            if (normalized == "fast" ||
                normalized == "flush" ||
                normalized == "fast-flush" ||
                normalized == "earth2cities" ||
                normalized == "confirmed" ||
                normalized == "confirmed-chunks" ||
                normalized == "confirmed-fast-flush")
            {
                return "confirmed-fast-flush";
            }

            if (normalized == "legacy" ||
                normalized == "legacy-fast" ||
                normalized == "legacy-fast-flush" ||
                normalized == "unbounded-fast-flush")
            {
                return "legacy-fast-flush";
            }

            if (normalized == "batch" ||
                normalized == "safe" ||
                normalized == "batch-safe")
            {
                return "batch-safe";
            }

            return "confirmed-fast-flush";
        }

        private static string SafeLog(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Replace("\r", " ").Replace("\n", " ");
        }
    }
}
