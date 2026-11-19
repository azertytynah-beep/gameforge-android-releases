using System.Collections.Generic;
using UnityEngine;

namespace GameForge.Runtime
{
    internal static class RunnerEnemyVisual
    {
        private static readonly Dictionary<RunnerEnemyDesign, Mesh> Meshes = new Dictionary<RunnerEnemyDesign, Mesh>();
        private static readonly int[] BoxTriangles =
        {
            0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 4, 7, 0, 7, 3,
            1, 2, 6, 1, 6, 5, 0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2
        };
        private static readonly byte[] StoneTriangles =
        {
            0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2,
            1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5
        };

        public static void Apply(GameObject obstacle, RunnerEnemyDesign design)
        {
            var collision = obstacle.GetComponent<BoxCollider>();
            var renderers = obstacle.GetComponentsInChildren<MeshRenderer>(true);
            if (collision == null || renderers.Length == 0 || renderers[0].sharedMaterial == null)
                return;

            var display = new GameObject("EnemyVisual", typeof(MeshFilter), typeof(MeshRenderer));
            display.layer = obstacle.layer;
            display.transform.SetParent(obstacle.transform, false);
            display.transform.localPosition = collision.center;
            display.transform.localScale = collision.size;
            display.GetComponent<MeshFilter>().sharedMesh = GetMesh(design);
            var renderer = display.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = renderers[0].sharedMaterial;
            foreach (var previous in renderers)
                previous.enabled = false;
        }

        private static Mesh GetMesh(RunnerEnemyDesign design)
        {
            if (!Meshes.TryGetValue(design, out var mesh) || mesh == null)
            {
                mesh = Build(design);
                Meshes[design] = mesh;
            }
            return mesh;
        }

        private static Mesh Build(RunnerEnemyDesign design)
        {
            var vertices = new List<Vector3>();
            void Box(float x, float y, float z, float w, float h, float d)
            {
                var min = new Vector3(x - w / 2f, y - h / 2f, z - d / 2f);
                var max = new Vector3(x + w / 2f, y + h / 2f, z + d / 2f);
                var v = new[]
                {
                    min, new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z),
                    new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, max.z),
                    new Vector3(max.x, min.y, max.z), max, new Vector3(min.x, max.y, max.z)
                };
                foreach (var i in BoxTriangles)
                    vertices.Add(v[i]);
            }

            void Stone(float x, float y, float z, float w, float h, float d)
            {
                var v = new[]
                {
                    new Vector3(x, y + h / 2f, z), new Vector3(x, y - h / 2f, z),
                    new Vector3(x - w / 2f, y, z), new Vector3(x + w / 2f, y, z),
                    new Vector3(x, y, z + d / 2f), new Vector3(x, y, z - d / 2f)
                };
                foreach (var i in StoneTriangles)
                    vertices.Add(v[i]);
            }

            switch (design)
            {
                case RunnerEnemyDesign.Robot:
                    Box(0, -.03f, 0, .51f, .52f, .43f);
                    Box(0, .3f, 0, .6f, .26f, .47f);
                    Box(-.38f, -.08f, 0, .16f, .35f, .37f);
                    Box(.38f, -.08f, 0, .16f, .35f, .37f);
                    Box(-.15f, -.36f, 0, .22f, .27f, .41f);
                    Box(.15f, -.36f, 0, .22f, .27f, .41f);
                    break;
                case RunnerEnemyDesign.Creature:
                    Stone(0, -.04f, 0, .83f, .76f, .7f);
                    Stone(0, .23f, -.16f, .56f, .42f, .42f);
                    Stone(-.19f, .33f, -.17f, .24f, .33f, .25f);
                    Stone(.19f, .33f, -.17f, .24f, .33f, .25f);
                    break;
                case RunnerEnemyDesign.Golem:
                    Stone(0, -.03f, 0, .92f, .94f, .88f);
                    Stone(-.27f, -.16f, -.19f, .48f, .62f, .6f);
                    Stone(.23f, .15f, .07f, .55f, .63f, .67f);
                    break;
                case RunnerEnemyDesign.Drone:
                    Stone(0, -.07f, 0, .52f, .58f, .83f);
                    Box(0, .08f, 0, .85f, .13f, .25f);
                    Box(-.32f, .23f, 0, .34f, .11f, .58f);
                    Box(.32f, .23f, 0, .34f, .11f, .58f);
                    break;
                case RunnerEnemyDesign.Plant:
                    Box(0, -.2f, 0, .23f, .6f, .26f);
                    Stone(0, .2f, 0, .63f, .55f, .65f);
                    Stone(-.24f, -.02f, 0, .48f, .18f, .39f);
                    Stone(.24f, .07f, 0, .48f, .18f, .39f);
                    break;
            }

            var mesh = new Mesh { name = "GameForge " + design };
            mesh.SetVertices(vertices);
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
