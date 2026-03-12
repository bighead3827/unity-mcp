using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Editor.Tools.Physics
{
    internal static class PhysicsSimulationOps
    {
        public static object SimulateStep(JObject @params)
        {
            var p = new ToolParams(@params);
            string dimension = (p.Get("dimension") ?? "3d").ToLowerInvariant();
            int steps = Mathf.Clamp(p.GetInt("steps") ?? 1, 1, 100);
            float stepSize = p.GetFloat("step_size") ?? Time.fixedDeltaTime;

            if (dimension != "3d" && dimension != "2d")
                return new ErrorResponse($"Invalid dimension: '{dimension}'. Use '3d' or '2d'.");

            if (dimension == "2d")
            {
                Physics2D.SyncTransforms();
                for (int i = 0; i < steps; i++)
                    Physics2D.Simulate(stepSize);
            }
            else
            {
                UnityEngine.Physics.SyncTransforms();
#if UNITY_2022_2_OR_NEWER
                if (UnityEngine.Physics.simulationMode != SimulationMode.Script)
                    UnityEngine.Physics.simulationMode = SimulationMode.Script;
#else
                if (UnityEngine.Physics.autoSimulation)
                    UnityEngine.Physics.autoSimulation = false;
#endif
                for (int i = 0; i < steps; i++)
                    UnityEngine.Physics.Simulate(stepSize);
            }

            return new
            {
                success = true,
                message = $"Executed {steps} physics step(s) ({dimension.ToUpper()}, step_size={stepSize:F4}s).",
                data = new { steps_executed = steps, step_size = stepSize, dimension }
            };
        }
    }
}
