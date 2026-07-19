using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        internal bool RenderServices = true;
        internal float ServiceMarkerSize = 16f;
        internal float ServiceYOffset = 1.5f;
        internal bool EnableServiceSpatialChunking = true;
        internal float ServiceChunkSizeMeters = 8192f;
        internal int ServiceChunksPerFrame = 8;

        private void LoadServiceSettings(JObject root)
        {
            RenderServices = GetBool(root, "renderServices", RenderServices);
            ServiceMarkerSize = GetFloat(root, "serviceMarkerSize", ServiceMarkerSize);
            ServiceYOffset = GetFloat(root, "serviceYOffset", ServiceYOffset);
            EnableServiceSpatialChunking = GetBool(
                root,
                "enableServiceSpatialChunking",
                EnableServiceSpatialChunking
            );
            ServiceChunkSizeMeters = GetFloat(
                root,
                "serviceChunkSizeMeters",
                ServiceChunkSizeMeters
            );
            ServiceChunksPerFrame = GetInt(
                root,
                "serviceChunksPerFrame",
                ServiceChunksPerFrame
            );

            ServicesWaterVisible = GetBool(root, "servicesWaterVisible", ServicesWaterVisible);
            ServicesElectricityVisible = GetBool(root, "servicesElectricityVisible", ServicesElectricityVisible);
            ServicesEducationVisible = GetBool(root, "servicesEducationVisible", ServicesEducationVisible);
            ServicesFireVisible = GetBool(root, "servicesFireVisible", ServicesFireVisible);
            ServicesHealthVisible = GetBool(root, "servicesHealthVisible", ServicesHealthVisible);
            ServicesParksVisible = GetBool(root, "servicesParksVisible", ServicesParksVisible);
            ServicesWasteVisible = GetBool(root, "servicesWasteVisible", ServicesWasteVisible);
            ServicesTransportVisible = GetBool(root, "servicesTransportVisible", ServicesTransportVisible);
            ServicesCommunicationVisible = GetBool(root, "servicesCommunicationVisible", ServicesCommunicationVisible);

            ServicesWaterAlpha = GetFloat(root, "servicesWaterAlpha", ServicesWaterAlpha);
            ServicesElectricityAlpha = GetFloat(root, "servicesElectricityAlpha", ServicesElectricityAlpha);
            ServicesEducationAlpha = GetFloat(root, "servicesEducationAlpha", ServicesEducationAlpha);
            ServicesFireAlpha = GetFloat(root, "servicesFireAlpha", ServicesFireAlpha);
            ServicesHealthAlpha = GetFloat(root, "servicesHealthAlpha", ServicesHealthAlpha);
            ServicesParksAlpha = GetFloat(root, "servicesParksAlpha", ServicesParksAlpha);
            ServicesWasteAlpha = GetFloat(root, "servicesWasteAlpha", ServicesWasteAlpha);
            ServicesTransportAlpha = GetFloat(root, "servicesTransportAlpha", ServicesTransportAlpha);
            ServicesCommunicationAlpha = GetFloat(root, "servicesCommunicationAlpha", ServicesCommunicationAlpha);
        }

        private void WriteServiceSettings(JObject root)
        {
            if (root == null)
                return;

            root["renderServices"] = RenderServices;
            root["serviceMarkerSize"] = ServiceMarkerSize;
            root["serviceYOffset"] = ServiceYOffset;
            root["enableServiceSpatialChunking"] = EnableServiceSpatialChunking;
            root["serviceChunkSizeMeters"] = ServiceChunkSizeMeters;
            root["serviceChunksPerFrame"] = ServiceChunksPerFrame;

            root["servicesWaterVisible"] = ServicesWaterVisible;
            root["servicesElectricityVisible"] = ServicesElectricityVisible;
            root["servicesEducationVisible"] = ServicesEducationVisible;
            root["servicesFireVisible"] = ServicesFireVisible;
            root["servicesHealthVisible"] = ServicesHealthVisible;
            root["servicesParksVisible"] = ServicesParksVisible;
            root["servicesWasteVisible"] = ServicesWasteVisible;
            root["servicesTransportVisible"] = ServicesTransportVisible;
            root["servicesCommunicationVisible"] = ServicesCommunicationVisible;

            root["servicesWaterAlpha"] = ServicesWaterAlpha;
            root["servicesElectricityAlpha"] = ServicesElectricityAlpha;
            root["servicesEducationAlpha"] = ServicesEducationAlpha;
            root["servicesFireAlpha"] = ServicesFireAlpha;
            root["servicesHealthAlpha"] = ServicesHealthAlpha;
            root["servicesParksAlpha"] = ServicesParksAlpha;
            root["servicesWasteAlpha"] = ServicesWasteAlpha;
            root["servicesTransportAlpha"] = ServicesTransportAlpha;
            root["servicesCommunicationAlpha"] = ServicesCommunicationAlpha;
        }

        private void SanitizeServiceSettings()
        {
            ServiceMarkerSize = Mathf.Clamp(ServiceMarkerSize, 1f, 128f);
            ServiceYOffset = Mathf.Clamp(ServiceYOffset, 0f, 50f);
            ServiceChunkSizeMeters = Mathf.Clamp(ServiceChunkSizeMeters, 128f, 16384f);
            ServiceChunksPerFrame = Mathf.Clamp(ServiceChunksPerFrame, 1, 64);

            ServicesWaterAlpha = Clamp01(ServicesWaterAlpha);
            ServicesElectricityAlpha = Clamp01(ServicesElectricityAlpha);
            ServicesEducationAlpha = Clamp01(ServicesEducationAlpha);
            ServicesFireAlpha = Clamp01(ServicesFireAlpha);
            ServicesHealthAlpha = Clamp01(ServicesHealthAlpha);
            ServicesParksAlpha = Clamp01(ServicesParksAlpha);
            ServicesWasteAlpha = Clamp01(ServicesWasteAlpha);
            ServicesTransportAlpha = Clamp01(ServicesTransportAlpha);
            ServicesCommunicationAlpha = Clamp01(ServicesCommunicationAlpha);
        }
    }
}
