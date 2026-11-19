using System.Linq;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameForge.Tests.EditMode
{
    public sealed class RunnerCatalogTests
    {
        private const string Path = "Assets/GameForge/Content/Catalog/RunnerCatalog.asset";

        [Test]
        public void DefaultCatalogResolvesTwoThemesAndTwoObstacles()
        {
            RunnerCatalog catalog = LoadCatalog();
            Assert.That(catalog.Themes.Select(theme => theme.Id),
                Is.EquivalentTo(new[] { "theme-default", "theme-night" }));
            Assert.That(catalog.Obstacles.Select(obstacle => obstacle.Id),
                Is.EquivalentTo(new[] { "obstacle-block", "obstacle-barrier" }));
            Assert.That(catalog.TryGetTheme("theme-default", out var meadow), Is.True);
            Assert.That(catalog.TryGetTheme("theme-night", out var night), Is.True);
            Assert.That(meadow.DisplayName, Is.Not.EqualTo(night.DisplayName));
            Assert.That(catalog.HasTheme("THEME-DEFAULT"), Is.False);
            Assert.That(catalog.HasObstacle("obstacle-missing"), Is.False);
            Assert.That(RunnerConfigValidator.Validate(RunnerConfigV1.CreateDefault(), catalog), Is.Empty);

            var meadowFloor = meadow.Segments.Single().Prefab.transform.Find("Floor");
            var nightPrefab = night.Segments.Single().Prefab;
            var nightFloor = nightPrefab.transform.Find("Floor");
            Assert.That(PrefabUtility.GetPrefabAssetType(nightPrefab), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(nightFloor.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(meadowFloor.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(nightFloor.GetComponent<Renderer>().sharedMaterial, Is.Not.SameAs(meadowFloor.GetComponent<Renderer>().sharedMaterial));
        }

        [Test]
        public void TrackAndObstacleCollidersMatchTheStreamingEnvelope()
        {
            RunnerCatalog catalog = LoadCatalog();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var theme in catalog.Themes)
                {
                    var segment = theme.Segments.Single();
                    var first = (GameObject)PrefabUtility.InstantiatePrefab(segment.Prefab, preview);
                    var second = (GameObject)PrefabUtility.InstantiatePrefab(segment.Prefab, preview);
                    var entry = first.transform.Find("Entry");
                    var exit = first.transform.Find("Exit");
                    Assert.That(first.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(entry.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(exit.localPosition, Is.EqualTo(Vector3.forward * SegmentPlanner.SegmentLength));
                    Assert.That(entry.localRotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(exit.localRotation, Is.EqualTo(Quaternion.identity));
                    second.transform.position = exit.position;
                    Physics.SyncTransforms();
                    Assert.That(second.transform.Find("Entry").position, Is.EqualTo(exit.position));
                    var floorCollider = first.GetComponentInChildren<BoxCollider>();
                    Assert.That(floorCollider, Is.Not.Null);
                    Assert.That(floorCollider.bounds.size.x, Is.EqualTo(6f).Within(0.001f));
                    Assert.That(floorCollider.bounds.max.y, Is.EqualTo(0f).Within(0.001f));
                    Assert.That(floorCollider.bounds.min.z, Is.EqualTo(entry.position.z).Within(0.001f));
                    Assert.That(floorCollider.bounds.max.z, Is.EqualTo(exit.position.z).Within(0.001f));
                    Assert.That(floorCollider.gameObject.layer, Is.EqualTo(0));
                }

                int obstacleLayer = LayerMask.NameToLayer("RunnerObstacle");
                Assert.That(obstacleLayer, Is.EqualTo(8));
                foreach (var obstacle in catalog.Obstacles)
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(obstacle.Prefab, preview);
                    var collider = model.GetComponentsInChildren<Collider>().Single();
                    Physics.SyncTransforms();
                    Assert.That(model.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(collider.isTrigger, Is.False);
                    Assert.That(collider.gameObject.layer, Is.EqualTo(obstacleLayer));
                    Assert.That(collider.bounds.min.y, Is.EqualTo(0f).Within(0.001f));
                    Assert.That(collider.bounds.center.x, Is.EqualTo(0f).Within(0.001f));
                    Assert.That(collider.bounds.center.z, Is.EqualTo(0f).Within(0.001f));
                    Assert.That(collider.bounds.extents.x, Is.LessThanOrEqualTo(SegmentPlanner.MaxObstacleHalfWidth));
                    Assert.That(collider.bounds.extents.z, Is.LessThanOrEqualTo(SegmentPlanner.MaxObstacleHalfDepth));
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static RunnerCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RunnerCatalog>(Path);
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }
    }
}
