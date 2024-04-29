using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Quality;
using DCL.SkyBox.Rendering;
using ECS.Abstract;
using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DCL.SkyBox
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class TimeOfDaySystem : BaseUnityLoopSystem
    {
        private const string NAME = "Time of Day";

        private readonly IRendererFeaturesCache rendererFeaturesCache;
        private readonly Light light;

        private readonly ElementBinding<float> sunLatitude;
        private readonly ElementBinding<float> sunLongitude;
        private readonly ElementBinding<int> hour;
        private readonly ElementBinding<int> minutes;
        private readonly ElementBinding<float> timeSpeed;
        private readonly ElementBinding<int> frameThrottling;
        private bool paused;

        private DateTime currentDate;
        private float frameStep;

        private JobHandle jobHandle;
        private SunPosition.LightJob lightJob;

        private float accumulatedSeconds;

        internal TimeOfDaySystem(World world, IDebugContainerBuilder debugBuilder, IRendererFeaturesCache rendererFeaturesCache, Light light) : base(world)
        {
            this.rendererFeaturesCache = rendererFeaturesCache;
            this.light = light;

            currentDate = DateTime.Now;

            debugBuilder.AddWidget(NAME)
                        .AddControl(new DebugFloatFieldDef(sunLongitude = new ElementBinding<float>(0)), new DebugConstLabelDef("Sun Longitude"))
                        .AddControl(new DebugFloatFieldDef(sunLatitude = new ElementBinding<float>(0)), new DebugConstLabelDef("Sun Latitude"))
                        .AddControl(new DebugIntSliderDef("Hour", hour = new ElementBinding<int>(currentDate.Hour), 0, 24), null)
                        .AddControl(new DebugIntSliderDef("Minutes", minutes = new ElementBinding<int>(currentDate.Minute), 0, 60), null)
                        .AddControl(new DebugFloatFieldDef(timeSpeed = new ElementBinding<float>(1)), new DebugConstLabelDef("Time Speed"))
                        .AddControl(new DebugIntFieldDef(frameThrottling = new ElementBinding<int>(1)), new DebugConstLabelDef("Frame Throttling"))
                        .AddSingleButton("Reset Seconds", () => accumulatedSeconds = 0)
                        .AddControl(new DebugToggleDef(evt => paused = evt.newValue, false), new DebugConstLabelDef("Paused"));

            lightJob.Output = new NativeReference<SunPosition.LightJob.Result>(Allocator.Persistent);
        }

        public override void Dispose()
        {
            base.Dispose();

            jobHandle.Complete();
            lightJob.Output.Dispose();
        }

        protected override void Update(float t)
        {
            CompleteLightJob();

            if (paused) return;

            accumulatedSeconds += t * timeSpeed.Value;

            currentDate = DateTime.Now.Date;
            currentDate = currentDate.Add(new TimeSpan(hour.Value, minutes.Value, (int)accumulatedSeconds));

            if (frameStep == 0)
                ScheduleLightJob();

            frameStep = (frameStep + 1) % frameThrottling.Value;
        }

        private void CompleteLightJob()
        {
            if (jobHandle.Equals(default(JobHandle))) return;

            jobHandle.Complete();

            SunPosition.LightJob.Result output = lightJob.Output.Value;

            light.transform.localRotation = output.LightRotation;
            //light.intensity = output.LightIntensity;
            Color ambientSkyColor = new Color((float)125 / 255, (float)136 / 255, (float)159 / 255);
            Color ambientEquatorColor = new Color((float)170 / 255, (float)197 / 255, (float)178 / 255);
            Color ambientGroundColor = new Color((float)12 / 255, (float)9 / 255, (float)3 / 255);
            // RenderSettings.ambientSkyColor = ambientSkyColor * output.LightIntensity;
            // RenderSettings.ambientEquatorColor = ambientEquatorColor * output.LightIntensity;
            // RenderSettings.ambientGroundColor = ambientGroundColor * output.LightIntensity;
            //
            // RenderSettings.fogDensity = output.LightIntensity * 0.001f;
            // RenderSettings.fogColor = RenderSettings.ambientEquatorColor;

            // Update the model
            rendererFeaturesCache.GetRendererFeature<DCL_RenderFeature_ProceduralSkyBox>()?.RenderingModel.SetStellarPositions(output.SunPos, output.MoonPos);

            jobHandle = default(JobHandle);
        }

        private void ScheduleLightJob()
        {
            lightJob.Year = currentDate.Year;
            lightJob.Month = currentDate.Month;
            lightJob.Day = currentDate.Day;
            lightJob.TotalHours = (float)currentDate.TimeOfDay.TotalHours;

            lightJob.Latitude = sunLatitude.Value;
            lightJob.Longitude = sunLongitude.Value;

            jobHandle = lightJob.ScheduleByRef();
        }
    }
}
