using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Tools.Physics;
using static MCPForUnityTests.Editor.TestUtilities;

namespace MCPForUnityTests.Editor.Tools
{
    public class ManagePhysicsTests
    {
        private const string TempRoot = "Assets/Temp/ManagePhysicsTests";

        [SetUp]
        public void SetUp()
        {
            EnsureFolder(TempRoot);
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_2022_2_OR_NEWER
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
#else
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
#endif
            {
                if (go.name.StartsWith("PhysTest_"))
                    UnityEngine.Object.DestroyImmediate(go);
            }

            if (AssetDatabase.IsValidFolder(TempRoot))
                AssetDatabase.DeleteAsset(TempRoot);
            CleanupEmptyParentFolders(TempRoot);
        }

        // =====================================================================
        // Dispatch / Error Handling
        // =====================================================================

        [Test]
        public void HandleCommand_NullParams_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(null));
            Assert.IsFalse(result.Value<bool>("success"));
        }

        [Test]
        public void HandleCommand_MissingAction_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject()));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("action"));
        }

        [Test]
        public void HandleCommand_UnknownAction_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(
                new JObject { ["action"] = "bogus_action" }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("Unknown action"));
        }

        // =====================================================================
        // Ping
        // =====================================================================

        [Test]
        public void Ping_ReturnsPhysicsStatus()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(
                new JObject { ["action"] = "ping" }));
            Assert.IsTrue(result.Value<bool>("success"));
            Assert.That(result["message"].ToString(), Does.Contain("Physics"));
            var data = result["data"];
            Assert.IsNotNull(data);
            Assert.IsNotNull(data["gravity3d"]);
            Assert.IsNotNull(data["gravity2d"]);
            Assert.IsNotNull(data["simulationMode"]);
            Assert.IsNotNull(data["defaultSolverIterations"]);
            Assert.IsNotNull(data["bounceThreshold"]);
            Assert.IsNotNull(data["queriesHitTriggers"]);
        }

        // =====================================================================
        // GetSettings
        // =====================================================================

        [Test]
        public void GetSettings_3D_ReturnsGravity()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "get_settings",
                ["dimension"] = "3d"
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            var data = result["data"];
            Assert.AreEqual("3d", data["dimension"].ToString());
            var gravity = data["gravity"] as JArray;
            Assert.IsNotNull(gravity);
            Assert.AreEqual(3, gravity.Count);
            Assert.IsNotNull(data["defaultContactOffset"]);
            Assert.IsNotNull(data["sleepThreshold"]);
            Assert.IsNotNull(data["defaultSolverIterations"]);
            Assert.IsNotNull(data["simulationMode"]);
        }

        [Test]
        public void GetSettings_DefaultDimension_Returns3D()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "get_settings"
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            Assert.AreEqual("3d", result["data"]["dimension"].ToString());
        }

        [Test]
        public void GetSettings_2D_ReturnsGravity()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "get_settings",
                ["dimension"] = "2d"
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            var data = result["data"];
            Assert.AreEqual("2d", data["dimension"].ToString());
            var gravity = data["gravity"] as JArray;
            Assert.IsNotNull(gravity);
            Assert.AreEqual(2, gravity.Count);
            Assert.IsNotNull(data["velocityIterations"]);
            Assert.IsNotNull(data["positionIterations"]);
            Assert.IsNotNull(data["queriesHitTriggers"]);
        }

        [Test]
        public void GetSettings_InvalidDimension_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "get_settings",
                ["dimension"] = "4d"
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("Invalid dimension"));
        }

        // =====================================================================
        // SetSettings 3D
        // =====================================================================

        [Test]
        public void SetSettings_3D_ChangesGravity()
        {
            // Save original
            var originalGravity = UnityEngine.Physics.gravity;

            try
            {
                var result = ToJObject(ManagePhysics.HandleCommand(new JObject
                {
                    ["action"] = "set_settings",
                    ["dimension"] = "3d",
                    ["settings"] = new JObject
                    {
                        ["gravity"] = new JArray(0, -20, 0)
                    }
                }));
                Assert.IsTrue(result.Value<bool>("success"), result.ToString());
                var changed = result["data"]["changed"] as JArray;
                Assert.IsNotNull(changed);
                Assert.That(changed.ToString(), Does.Contain("gravity"));

                // Verify the change took effect
                Assert.AreEqual(-20f, UnityEngine.Physics.gravity.y, 0.01f);
            }
            finally
            {
                // Restore original
                UnityEngine.Physics.gravity = originalGravity;
            }
        }

        [Test]
        public void SetSettings_3D_ChangesSolverIterations()
        {
            var original = UnityEngine.Physics.defaultSolverIterations;

            try
            {
                var result = ToJObject(ManagePhysics.HandleCommand(new JObject
                {
                    ["action"] = "set_settings",
                    ["dimension"] = "3d",
                    ["settings"] = new JObject
                    {
                        ["defaultSolverIterations"] = 12
                    }
                }));
                Assert.IsTrue(result.Value<bool>("success"), result.ToString());
                Assert.AreEqual(12, UnityEngine.Physics.defaultSolverIterations);
            }
            finally
            {
                UnityEngine.Physics.defaultSolverIterations = original;
            }
        }

        [Test]
        public void SetSettings_3D_UnknownKey_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "set_settings",
                ["dimension"] = "3d",
                ["settings"] = new JObject
                {
                    ["nonExistentSetting"] = 42
                }
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("Unknown"));
            Assert.That(result["error"].ToString(), Does.Contain("nonExistentSetting"));
        }

        [Test]
        public void SetSettings_3D_InvalidGravityArray_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "set_settings",
                ["dimension"] = "3d",
                ["settings"] = new JObject
                {
                    ["gravity"] = new JArray(0, -10)  // Only 2 elements for 3D
                }
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("[x, y, z]"));
        }

        [Test]
        public void SetSettings_EmptySettings_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "set_settings",
                ["dimension"] = "3d",
                ["settings"] = new JObject()
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("settings"));
        }

        [Test]
        public void SetSettings_MissingSettings_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "set_settings",
                ["dimension"] = "3d"
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("settings"));
        }

        // =====================================================================
        // SetSettings 2D
        // =====================================================================

        [Test]
        public void SetSettings_2D_ChangesGravity()
        {
            var originalGravity = Physics2D.gravity;

            try
            {
                var result = ToJObject(ManagePhysics.HandleCommand(new JObject
                {
                    ["action"] = "set_settings",
                    ["dimension"] = "2d",
                    ["settings"] = new JObject
                    {
                        ["gravity"] = new JArray(0, -20)
                    }
                }));
                Assert.IsTrue(result.Value<bool>("success"), result.ToString());
                var changed = result["data"]["changed"] as JArray;
                Assert.IsNotNull(changed);
                Assert.That(changed.ToString(), Does.Contain("gravity"));

                Assert.AreEqual(-20f, Physics2D.gravity.y, 0.01f);
            }
            finally
            {
                Physics2D.gravity = originalGravity;
            }
        }

        [Test]
        public void SetSettings_2D_UnknownKey_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "set_settings",
                ["dimension"] = "2d",
                ["settings"] = new JObject
                {
                    ["fakeSetting"] = true
                }
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("Unknown"));
            Assert.That(result["error"].ToString(), Does.Contain("fakeSetting"));
        }

        [Test]
        public void SetSettings_2D_InvalidGravityArray_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "set_settings",
                ["dimension"] = "2d",
                ["settings"] = new JObject
                {
                    ["gravity"] = new JArray(0)  // Only 1 element for 2D
                }
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("[x, y]"));
        }
    }
}
