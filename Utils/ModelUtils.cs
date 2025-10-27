using OpenTK.Mathematics;

namespace NewtonsCradle.Utils
{
    public class ModelUtils
    {
        public static Dictionary<string, Matrix4> BuildWorldTransformsFromOriginal(ModelLoader model, Dictionary<string, Matrix4> originalTransforms)
        {
            var world = new Dictionary<string, Matrix4>();
            if (model?.Scene?.RootNode == null) return world;

            void Walk(Assimp.Node node, Matrix4 parentWorld)
            {
                Matrix4 local;
                if (!originalTransforms.TryGetValue(node.Name, out local))
                {
                    var m = node.Transform;
                    local = new Matrix4(
                        m.A1, m.B1, m.C1, m.D1,
                        m.A2, m.B2, m.C2, m.D2,
                        m.A3, m.B3, m.C3, m.D3,
                        m.A4, m.B4, m.C4, m.D4
                    );
                }

                var curWorld = parentWorld * local;
                world[node.Name] = curWorld;

                foreach (var ch in node.Children) Walk(ch, curWorld);
            }

            Walk(model.Scene.RootNode, Matrix4.Identity);
            return world;
        }

        public static Vector3 ComputeNodePivotWorld(ModelLoader model, string nodeName, Dictionary<string, Matrix4> worldTransforms)
        {
            var scene = model?.Scene;
            if (scene == null || scene.RootNode == null) return Vector3.Zero;
            var node = FindNodeByName(scene.RootNode, nodeName);
            if (node == null)
            {
                if (worldTransforms != null && worldTransforms.TryGetValue(nodeName, out var wm))
                    return wm.ExtractTranslation();
                return Vector3.Zero;
            }

            Matrix4 world = Matrix4.Identity;
            if (worldTransforms != null && worldTransforms.TryGetValue(node.Name, out var w)) world = w;

            long total = 0;
            Vector3 sum = Vector3.Zero;

            foreach (int mi in node.MeshIndices)
            {
                if (mi < 0 || mi >= scene.MeshCount) continue;
                var mesh = scene.Meshes[mi];
                for (int vi = 0; vi < mesh.VertexCount; vi++)
                {
                    var v = mesh.Vertices[vi];
                    var v4 = new Vector4((float)v.X, (float)v.Y, (float)v.Z, 1.0f);
                    var vt = Vector4.TransformRow(v4, world);
                    sum += new Vector3(vt.X, vt.Y, vt.Z);
                    total++;
                }
            }

            return total > 0 ? sum / (float)total : world.ExtractTranslation();
        }

        public static Assimp.Node FindNodeByName(Assimp.Node root, string name)
        {
            if (root == null) return null;
            if (string.Equals(root.Name, name, StringComparison.Ordinal)) return root;
            foreach (var ch in root.Children)
            {
                var r = FindNodeByName(ch, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
