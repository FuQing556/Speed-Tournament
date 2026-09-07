using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpeedTournament
{
    // Shared materials/meshes: no material instantiation during racing.
    public static class RaceVisuals
    {
        static readonly Dictionary<string, Material> materials = new();
        static readonly Dictionary<PrimitiveType, Mesh> meshes = new();
        static Material outline;
        public static readonly Color Ink = new Color(0.018f, 0.027f, 0.060f);
        public static readonly Color Cyan = new Color(0.08f, 0.88f, 1f);
        public static readonly Color Orange = new Color(1f, 0.49f, 0.10f);
        public static readonly Color Danger = new Color(1f, 0.20f, 0.23f);
        public static readonly Color[] Roles = { new(1f,0.68f,0.12f), new(0.12f,0.72f,1f), new(1f,0.26f,0.35f), new(0.69f,0.35f,1f), new(0.42f,1f,0.73f), new(1f,0.86f,0.26f) };

        public static Material Material(string name, Color color)
        {
            if (materials.TryGetValue(name, out Material result) && result != null) return result;
            Shader shader = Resources.Load<Shader>("Shaders/RaceToon");
            result = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { name = name, enableInstancing = true };
            result.SetColor("_BaseColor", color);
            materials[name] = result;
            return result;
        }

        public static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material, bool edged = false)
        {
            if (!meshes.TryGetValue(shape, out Mesh mesh) || mesh == null)
            {
                var template = GameObject.CreatePrimitive(shape);
                template.SetActive(false);
                mesh = template.GetComponent<MeshFilter>().sharedMesh;
                meshes[shape] = mesh;
                Object.Destroy(template);
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (edged)
            {
                if (outline == null)
                {
                    outline = new Material(Resources.Load<Shader>("Shaders/RaceOutline")) { name = "Shared_InkOutline", enableInstancing = true };
                    outline.SetColor("_BaseColor", Ink);
                }
                var hull = new GameObject("Outline", typeof(MeshFilter), typeof(MeshRenderer));
                hull.transform.SetParent(go.transform, false);
                hull.GetComponent<MeshFilter>().sharedMesh = mesh;
                var edge = hull.GetComponent<MeshRenderer>();
                edge.sharedMaterial = outline; edge.shadowCastingMode = ShadowCastingMode.Off; edge.receiveShadows = false;
            }
            return go;
        }

        public static void Chevron(Transform parent, Vector3 pos, float size, Material material)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var part = Part(parent, "Arrow", PrimitiveType.Cube, pos + Vector3.right * side * size * 0.24f, new Vector3(size * 0.17f, 0.05f, size * 0.72f), material);
                part.transform.localRotation = Quaternion.Euler(0f, side * -40f, 0f);
            }
        }
    }
}
