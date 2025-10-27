using OpenTK.Mathematics;

using NewtonsCradle.Utils;

namespace NewtonsCradle.Models
{
    public class CradleModel
    {
        public ModelLoader Model { get; private set; } = new();
        public Dictionary<string, Matrix4> LocalTransforms { get; private set; } = new();
        public Dictionary<string, Matrix4> OriginalTransforms { get; private set; } = new();
        public Dictionary<string, Vector3> PivotWorlds { get; private set; } = new();

        private int _activePendulum = 0;
        private float _pendulumTime = 0f;
        private const float swingDuration = 1f;
        private bool _isAnimating = false;

        public void Load(string path, Camera camera)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("Модель не найдена: " + path);
                return;
            }

            Model.LoadFromFile(path);

            if (Model.Scene != null)
            {
                ComputePivots();

                Model.BuildLocalTransforms(Model.Scene.RootNode, LocalTransforms);
                foreach (var kv in LocalTransforms)
                    OriginalTransforms[kv.Key] = kv.Value;

                ApplyTextures();
                CenterCamera(camera);
            }
        }

        private void ComputePivots()
        {
            var world = ModelUtils.BuildWorldTransformsFromOriginal(Model, OriginalTransforms);
            foreach (var name in new[]
            {
                "polySurface19_Hook_0", "polySurface18_Hook_0",
                "polySurface17_Hook_0", "polySurface16_Hook_0",
                "polySurface20_Hook_0"
            })
            {
                PivotWorlds[name] = ModelUtils.ComputeNodePivotWorld(Model, name, world);
            }
        }

        private void ApplyTextures()
        {
            try
            {
                int woodTex = TextureUtils.LoadTextureStandalone(System.IO.Path.Combine("Assets", "wood.jpg"));
                int metalTex = TextureUtils.LoadTextureStandalone(System.IO.Path.Combine("Assets", "metal.jpg"));

                if (Model.Meshes == null || Model.Meshes.Count == 0) return;

                foreach (var mesh in Model.Meshes)
                {
                    string name = mesh.SourceMesh?.Name ?? "";

                    if (name.Contains("Column", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Flor", StringComparison.OrdinalIgnoreCase))
                        mesh.TextureId = woodTex;
                    else
                        mesh.TextureId = metalTex;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка применения текстур: " + ex.Message);
            }
        }

        private void CenterCamera(Camera camera)
        {
            if (!Model.GetBoundingBoxWorld(out var min, out var max)) return;

            var center = (min + max) * 0.5f;
            var diag = (max - min).Length;
            camera.Target = center;
            camera.Radius = Math.Max(0.5f, diag * 0.9f);
        }

        public void ToggleAnimation()
        {
            _isAnimating = !_isAnimating;
        }

        public void Update(float deltaTime)
        {
            if (!_isAnimating) return;

            _pendulumTime += deltaTime;
            if (_pendulumTime > swingDuration)
            {
                _pendulumTime = 0f;
                _activePendulum = 1 - _activePendulum;
            }

            AnimatePendulums();
        }

        private void AnimatePendulums()
        {
            float t = _pendulumTime / swingDuration;
            float swingFactor = -MathF.Sin(t * MathF.PI);
            float baseAngle = 0.3f;

            float[] angles = new float[5];
            if (_activePendulum == 0) angles[0] = baseAngle * swingFactor;
            else angles[4] = -baseAngle * swingFactor;

            angles[1] = MathF.Sin(t * MathF.PI) * 0.05f;
            angles[2] = MathF.Sin(t * MathF.PI) * 0.03f;
            angles[3] = -MathF.Sin(t * MathF.PI) * 0.05f;

            string[][] pendulums =
            {
                new[] { "polySurface19_Hook_0", "polySurface5_Ball_0", "polySurface10_Wire_0" },
                new[] { "polySurface18_Hook_0", "polySurface4_Ball_0", "polySurface9_Wire_0" },
                new[] { "polySurface17_Hook_0", "polySurface3_Ball_0", "polySurface7_Wire_0" },
                new[] { "polySurface16_Hook_0", "polySurface2_Ball_0", "polySurface8_Wire_0" },
                new[] { "polySurface20_Hook_0", "polySurface1_Ball_0", "polySurface6_Wire_0" },
            };

            foreach (var kv in OriginalTransforms)
                LocalTransforms[kv.Key] = kv.Value;

            var worldTransforms = ModelUtils.BuildWorldTransformsFromOriginal(Model, OriginalTransforms);

            for (int p = 0; p < pendulums.Length; p++)
            {
                float localAngle = angles[p];
                if (MathF.Abs(localAngle) < 1e-5f) continue;

                string hookNode = pendulums[p][0];
                if (!PivotWorlds.TryGetValue(hookNode, out var pivot))
                    pivot = ModelUtils.ComputeNodePivotWorld(Model, hookNode, worldTransforms);

                foreach (var nodeName in pendulums[p])
                {
                    if (!OriginalTransforms.TryGetValue(nodeName, out var baseMatrix)) continue;

                    var rotation = Matrix4.CreateTranslation(-pivot) *
                                   Matrix4.CreateFromAxisAngle(Vector3.UnitZ, localAngle) *
                                   Matrix4.CreateTranslation(pivot);

                    LocalTransforms[nodeName] = rotation * baseMatrix;
                }
            }
        }

        public void Draw(int shaderProgram)
        {
            Model.Draw(shaderProgram, LocalTransforms, 0);
        }
    }
}