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

        // =====================================================================
        // CreatePhysicsMaterial
        // =====================================================================

        [Test]
        public void CreatePhysicsMaterial_3D_CreatesAsset()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "create_physics_material",
                ["name"] = "TestMat3D",
                ["path"] = TempRoot,
                ["dimension"] = "3d"
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());

            string expectedPath = TempRoot + "/TestMat3D.physicMaterial";
            Assert.AreEqual(expectedPath, result["data"]["path"].ToString());
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(expectedPath),
                "Asset should exist on disk.");
        }

        [Test]
        public void CreatePhysicsMaterial_2D_CreatesAsset()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "create_physics_material",
                ["name"] = "TestMat2D",
                ["path"] = TempRoot,
                ["dimension"] = "2d"
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());

            string expectedPath = TempRoot + "/TestMat2D.physicsMaterial2D";
            Assert.AreEqual(expectedPath, result["data"]["path"].ToString());
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(expectedPath),
                "2D physics material asset should exist on disk.");
        }

        [Test]
        public void CreatePhysicsMaterial_MissingName_ReturnsError()
        {
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "create_physics_material",
                ["path"] = TempRoot,
                ["dimension"] = "3d"
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("name"));
        }

        // =====================================================================
        // ConfigurePhysicsMaterial
        // =====================================================================

        [Test]
        public void ConfigurePhysicsMaterial_3D_UpdatesProperties()
        {
            // Create material first
            ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "create_physics_material",
                ["name"] = "ConfigTest3D",
                ["path"] = TempRoot,
                ["dimension"] = "3d",
                ["bounciness"] = 0
            });

            string matPath = TempRoot + "/ConfigTest3D.physicMaterial";

            // Configure it
            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "configure_physics_material",
                ["path"] = matPath,
                ["dimension"] = "3d",
                ["properties"] = new JObject
                {
                    ["bounciness"] = 0.75
                }
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());

            // Verify the property changed
#if UNITY_6000_0_OR_NEWER
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(matPath);
#else
            var mat = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(matPath);
#endif
            Assert.IsNotNull(mat);
            Assert.AreEqual(0.75f, mat.bounciness, 0.01f);
        }

        // =====================================================================
        // AssignPhysicsMaterial
        // =====================================================================

        [Test]
        public void AssignPhysicsMaterial_ToBoxCollider()
        {
            // Create a physics material
            ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "create_physics_material",
                ["name"] = "AssignTest3D",
                ["path"] = TempRoot,
                ["dimension"] = "3d"
            });

            string matPath = TempRoot + "/AssignTest3D.physicMaterial";

            // Create a GameObject with a BoxCollider
            var go = new GameObject("PhysTest_BoxCollider");
            go.AddComponent<BoxCollider>();

            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "assign_physics_material",
                ["target"] = "PhysTest_BoxCollider",
                ["material_path"] = matPath,
                ["search_method"] = "by_name"
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());

            var collider = go.GetComponent<BoxCollider>();
            Assert.IsNotNull(collider.sharedMaterial,
                "BoxCollider.sharedMaterial should be set after assignment.");
        }

        [Test]
        public void AssignPhysicsMaterial_NoCollider_ReturnsError()
        {
            // Create a physics material
            ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "create_physics_material",
                ["name"] = "AssignNoCollider",
                ["path"] = TempRoot,
                ["dimension"] = "3d"
            });

            string matPath = TempRoot + "/AssignNoCollider.physicMaterial";

            // Create a GameObject WITHOUT any collider
            var go = new GameObject("PhysTest_NoCollider");

            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "assign_physics_material",
                ["target"] = "PhysTest_NoCollider",
                ["material_path"] = matPath,
                ["search_method"] = "by_name"
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("collider").IgnoreCase);
        }

        // =====================================================================
        // AddJoint
        // =====================================================================

        [Test]
        public void AddJoint_Hinge_RequiresRigidbody()
        {
            var go = new GameObject("PhysTest_NoRB");

            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "add_joint",
                ["target"] = "PhysTest_NoRB",
                ["joint_type"] = "hinge"
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("Rigidbody"));
        }

        [Test]
        public void AddJoint_Hinge_Success()
        {
            var go = new GameObject("PhysTest_HingeAdd");
            go.AddComponent<Rigidbody>();

            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "add_joint",
                ["target"] = "PhysTest_HingeAdd",
                ["joint_type"] = "hinge"
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            Assert.That(result["data"]["jointType"].ToString(), Is.EqualTo("HingeJoint"));

            var hinge = go.GetComponent<HingeJoint>();
            Assert.IsNotNull(hinge, "HingeJoint should be present on the GameObject.");
        }

        [Test]
        public void AddJoint_UnknownType_ReturnsError()
        {
            var go = new GameObject("PhysTest_UnknownJoint");
            go.AddComponent<Rigidbody>();

            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "add_joint",
                ["target"] = "PhysTest_UnknownJoint",
                ["joint_type"] = "rubber_band"
            }));
            Assert.IsFalse(result.Value<bool>("success"));
            Assert.That(result["error"].ToString(), Does.Contain("Unknown"));
            Assert.That(result["error"].ToString(), Does.Contain("rubber_band"));
        }

        // =====================================================================
        // ConfigureJoint
        // =====================================================================

        [Test]
        public void ConfigureJoint_Hinge_Motor()
        {
            var go = new GameObject("PhysTest_HingeMotor");
            go.AddComponent<Rigidbody>();
            go.AddComponent<HingeJoint>();

            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "configure_joint",
                ["target"] = "PhysTest_HingeMotor",
                ["joint_type"] = "hinge",
                ["motor"] = new JObject
                {
                    ["targetVelocity"] = 90f,
                    ["force"] = 50f,
                    ["freeSpin"] = false
                }
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());

            var hinge = go.GetComponent<HingeJoint>();
            Assert.IsTrue(hinge.useMotor, "useMotor should be true after motor config.");
            Assert.AreEqual(90f, hinge.motor.targetVelocity, 0.01f);
            Assert.AreEqual(50f, hinge.motor.force, 0.01f);
        }

        [Test]
        public void ConfigureJoint_Hinge_Limits()
        {
            var go = new GameObject("PhysTest_HingeLimits");
            go.AddComponent<Rigidbody>();
            go.AddComponent<HingeJoint>();

            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "configure_joint",
                ["target"] = "PhysTest_HingeLimits",
                ["joint_type"] = "hinge",
                ["limits"] = new JObject
                {
                    ["min"] = -45f,
                    ["max"] = 45f,
                    ["bounciness"] = 0.5f
                }
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());

            var hinge = go.GetComponent<HingeJoint>();
            Assert.IsTrue(hinge.useLimits, "useLimits should be true after limits config.");
            Assert.AreEqual(-45f, hinge.limits.min, 0.01f);
            Assert.AreEqual(45f, hinge.limits.max, 0.01f);
            Assert.AreEqual(0.5f, hinge.limits.bounciness, 0.01f);
        }

        // =====================================================================
        // RemoveJoint
        // =====================================================================

        [Test]
        public void RemoveJoint_Specific_Success()
        {
            var go = new GameObject("PhysTest_RemoveHinge");
            go.AddComponent<Rigidbody>();
            go.AddComponent<HingeJoint>();
            Assert.IsNotNull(go.GetComponent<HingeJoint>());

            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "remove_joint",
                ["target"] = "PhysTest_RemoveHinge",
                ["joint_type"] = "hinge"
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            Assert.AreEqual(1, result["data"]["removedCount"].Value<int>());
            Assert.IsNull(go.GetComponent<HingeJoint>(),
                "HingeJoint should be removed.");
        }

        [Test]
        public void RemoveJoint_All()
        {
            var go = new GameObject("PhysTest_RemoveAll");
            go.AddComponent<Rigidbody>();
            go.AddComponent<HingeJoint>();
            go.AddComponent<FixedJoint>();
            Assert.IsNotNull(go.GetComponent<HingeJoint>());
            Assert.IsNotNull(go.GetComponent<FixedJoint>());

            var result = ToJObject(ManagePhysics.HandleCommand(new JObject
            {
                ["action"] = "remove_joint",
                ["target"] = "PhysTest_RemoveAll"
            }));
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            Assert.AreEqual(2, result["data"]["removedCount"].Value<int>());
            Assert.IsNull(go.GetComponent<HingeJoint>(),
                "HingeJoint should be removed.");
            Assert.IsNull(go.GetComponent<FixedJoint>(),
                "FixedJoint should be removed.");
        }
    }
}
