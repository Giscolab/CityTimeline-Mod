using Game;
using CityTimelineMod.Roads;

namespace CityTimelineMod.Systems
{
    public partial class RuntimeRoadToolDriverSystem : GameSystemBase
    {
        private RuntimeRoadToolSystem m_RoadToolSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_RoadToolSystem = World.GetOrCreateSystemManaged<RuntimeRoadToolSystem>();

            CityTimelineMod.Util.Log.Info("[RoadImport] RuntimeRoadToolDriverSystem OnCreate.");
            Enabled = true;
        }

        protected override void OnUpdate()
        {
            if (m_RoadToolSystem == null)
                return;

            Dependency = m_RoadToolSystem.DriverUpdate(Dependency);
        }
    }
}

