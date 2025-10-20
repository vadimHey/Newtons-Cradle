using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace NewtonsCradle
{
    public class SceneRenderer : GameWindow
    {
        private OrbitCamera _camera;
        private int _shaderProgram;

        // Table
        private int _tableVao, _tableVbo, _tableEbo;
        private int _tableTexture;

        // Model
        private ModelLoader _model = new ModelLoader();
        private Dictionary<string, Matrix4> _localTransforms = new();
        private bool _localTransformsBuilt = false;

        // Сохраняем исходные матрицы всех узлов модели
        private Dictionary<string, Matrix4> _originalTransforms = new Dictionary<string, Matrix4>();

        private int _activePendulum = 0; // 0 — левый, 1 — правый
        private float _pendulumTime = 0f;
        private const float swingDuration = 1f; // секунда качания одного шара

        private Vector3 _lightPos = new Vector3(0.5f, 2.0f, 2.0f);

        public SceneRenderer(GameWindowSettings g, NativeWindowSettings n) : base(g, n) { }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.85f, 0.9f, 0.95f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            _camera = new OrbitCamera(new Vector3(0, 0.6f, 3.0f), Vector3.Zero, Size.X / (float)Size.Y);

            // Шейдер
            _shaderProgram = ShaderUtils.CreateProgram("Shaders/vertex.glsl", "Shaders/fragment.glsl");
            GL.UseProgram(_shaderProgram);

            // Стол
            CreateTable();
            _tableTexture = TextureUtils.LoadTextureStandalone(Path.Combine("Assets", "woodTable.jpg"));

            // Загрузка модели
            string modelPath = Path.Combine("Assets", "newtons_cradle3.glb");
            if (!File.Exists(modelPath))
            {
                Console.WriteLine("Model not found: " + modelPath);
            }
            else
            {
                _model.LoadFromFile(modelPath);

                if (_model.Scene != null)
                {
                    // Локальные трансформы
                    _model.BuildLocalTransforms(_model.Scene.RootNode, _localTransforms);
                    foreach (var kv in _localTransforms)
                    {
                        _originalTransforms[kv.Key] = kv.Value;
                        _localTransforms[kv.Key] = kv.Value;
                    }
                    _localTransformsBuilt = true;

                    // Подмена текстур по названию мешей
                    try
                    {
                        // Загружаем материалы для столешницы и маятника
                        int woodTex = TextureUtils.LoadTextureStandalone(Path.Combine("Assets", "wood.jpg"));
                        int woodTableTex = TextureUtils.LoadTextureStandalone(Path.Combine("Assets", "woodTable.jpg"));
                        int metalTex = TextureUtils.LoadTextureStandalone(Path.Combine("Assets", "metal.jpg"));

                        if (_model.Meshes != null && _model.Meshes.Count > 0)
                        {
                            for (int i = 0; i < _model.Meshes.Count; i++)
                            {
                                var mesh = _model.Meshes[i];
                                // допустим первые 3 меша — основа, остальные — металлические
                                if (i < 1)
                                    mesh.TextureId = woodTableTex;
                                else if (i == 2)
                                    mesh.TextureId = woodTex;
                                else
                                    mesh.TextureId = metalTex;
                            }
                        }
                        else
                        {
                            Console.WriteLine("_model.Meshes пуст — возможно, модель не содержит мешей?");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Texture override failed: " + ex.Message);
                    }


                    // Камера по центру модели
                    if (_model.GetBoundingBoxWorld(out var bboxMin, out var bboxMax))
                    {
                        var center = (bboxMin + bboxMax) * 0.5f;
                        var diag = (bboxMax - bboxMin).Length;
                        _camera.Target = center;
                        _camera.Radius = Math.Max(0.5f, diag * 0.9f);
                    }
                }
            }

            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "texture0"), 0);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "lightPos"), _lightPos);

            Console.WriteLine("=== Mesh list ===");
            for (int i = 0; i < _model.Meshes.Count; i++)
            {
                var mesh = _model.Meshes[i];
                string name = mesh.SourceMesh?.Name ?? "(no name)";
                Console.WriteLine($"[{i}] {name}");
            }

            Console.WriteLine("=== Scene nodes ===");
            if (_model.Scene?.RootNode != null)
            {
                void PrintNodes(Assimp.Node node, string indent)
                {
                    Console.WriteLine($"{indent}- {node.Name}");
                    foreach (var child in node.Children)
                        PrintNodes(child, indent + "  ");
                }
                PrintNodes(_model.Scene.RootNode, "");
            }
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            GL.DeleteProgram(_shaderProgram);
            GL.DeleteVertexArray(_tableVao);
            GL.DeleteTexture(_tableTexture);
            _model.Delete();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
            _camera.AspectRatio = Size.X / (float)Size.Y;
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            if (KeyboardState.IsKeyDown(Keys.Escape)) Close();

            if (MouseState.IsButtonDown(MouseButton.Right))
            {
                var md = MouseState.Delta;
                _camera.Yaw += md.X * 0.2f;
                _camera.Pitch -= md.Y * 0.2f;
            }
            _camera.Radius -= MouseState.ScrollDelta.Y * 0.5f;
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.UseProgram(_shaderProgram);

            // Матрицы камеры
            var view = _camera.GetViewMatrix();
            var proj = _camera.GetProjectionMatrix();
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "projection"), false, ref proj);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "viewPos"), _camera.Position);

            // Стол
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _tableTexture);
            var tableModel = Matrix4.CreateScale(33.0f, 3.6f, 44.0f) *
                             Matrix4.CreateTranslation(0f, -4.3f, -4f);
            //GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "model"), false, ref tableModel);
            //GL.BindVertexArray(_tableVao);
            //GL.DrawElements(PrimitiveType.Triangles, 36, DrawElementsType.UnsignedInt, 0);

            if (_model.Scene != null)
            {
                // === Таймер ===
                _pendulumTime += (float)e.Time;
                if (_pendulumTime > swingDuration)
                {
                    _pendulumTime = 0f;
                    _activePendulum = 1 - _activePendulum; // переключаем левый/правый
                }

                // === Настройка качания ===
                float t = _pendulumTime / swingDuration; // от 0 до 1
                float swing = -MathF.Sin(t * MathF.PI);   // движение туда-обратно
                float baseAngle = 0.3f;                 // макс. угол (17°)

                // === Группы маятников ===
                string[][] pendulums =
                {
                    new[] { "polySurface19_Hook_0", "polySurface5_Ball_0", "polySurface10_Wire_0" }, // левый
                    new[] { "polySurface18_Hook_0", "polySurface4_Ball_0", "polySurface9_Wire_0" },  // 2
                    new[] { "polySurface17_Hook_0", "polySurface3_Ball_0", "polySurface7_Wire_0" },  // 3 (центр)
                    new[] { "polySurface16_Hook_0", "polySurface2_Ball_0", "polySurface8_Wire_0" },  // 4
                    new[] { "polySurface20_Hook_0", "polySurface1_Ball_0", "polySurface6_Wire_0" },  // правый
                };

                // === Сброс всех трансформов ===
                foreach (var kv in _originalTransforms)
                    _localTransforms[kv.Key] = kv.Value;

                // === Рассчёт углов качания ===
                float[] angles = new float[5];

                if (_activePendulum == 0)
                {
                    // качается левый шар
                    angles[0] = baseAngle * swing;
                    angles[4] = 0f;
                }
                else
                {
                    // качается правый шар
                    angles[0] = 0f;
                    angles[4] = -baseAngle * swing;
                }

                // слегка двигаем средние при столкновении
                angles[1] = MathF.Sin(t * MathF.PI) * 0.05f;
                angles[2] = MathF.Sin(t * MathF.PI) * 0.03f;
                angles[3] = MathF.Sin(t * MathF.PI) * 0.05f;

                // === Применяем трансформации ===
                for (int p = 0; p < pendulums.Length; p++)
                {
                    float localAngle = angles[p];
                    if (MathF.Abs(localAngle) < 1e-4f) continue;

                    foreach (string nodeName in pendulums[p])
                    {
                        if (_originalTransforms.TryGetValue(nodeName, out var baseMatrix))
                        {
                            // pivot — точка подвеса (крюк)
                            Vector3 pivot = _originalTransforms[pendulums[p][0]].ExtractTranslation();

                            var rotation =
                                Matrix4.CreateTranslation(-pivot) *
                                Matrix4.CreateFromAxisAngle(Vector3.UnitZ, localAngle) * // ось качания
                                Matrix4.CreateTranslation(pivot);

                            _localTransforms[nodeName] = rotation * baseMatrix;
                        }
                    }
                }

                _model.Draw(_shaderProgram, _localTransforms, 0);
            }


            SwapBuffers();
        }


        private void CreateTable()
        {
            float[] vertices = {
                // positions          // normals         // texcoords
                -0.5f,-0.5f,-0.5f,  0,0,-1,  0,0,
                 0.5f,-0.5f,-0.5f,  0,0,-1,  1,0,
                 0.5f, 0.5f,-0.5f,  0,0,-1,  1,1,
                -0.5f, 0.5f,-0.5f,  0,0,-1,  0,1,
                -0.5f,-0.5f, 0.5f,  0,0,1,   0,0,
                 0.5f,-0.5f, 0.5f,  0,0,1,   1,0,
                 0.5f, 0.5f, 0.5f,  0,0,1,   1,1,
                -0.5f, 0.5f, 0.5f,  0,0,1,   0,1,
                -0.5f, 0.5f, 0.5f, -1,0,0,   1,0,
                -0.5f, 0.5f,-0.5f, -1,0,0,   1,1,
                -0.5f,-0.5f,-0.5f, -1,0,0,   0,1,
                -0.5f,-0.5f, 0.5f, -1,0,0,   0,0,
                 0.5f, 0.5f, 0.5f,  1,0,0,   1,0,
                 0.5f, 0.5f,-0.5f,  1,0,0,   1,1,
                 0.5f,-0.5f,-0.5f,  1,0,0,   0,1,
                 0.5f,-0.5f, 0.5f,  1,0,0,   0,0,
                -0.5f,-0.5f,-0.5f, 0,-1,0,   0,1,
                 0.5f,-0.5f,-0.5f, 0,-1,0,   1,1,
                 0.5f,-0.5f, 0.5f, 0,-1,0,   1,0,
                -0.5f,-0.5f, 0.5f, 0,-1,0,   0,0,
                -0.5f, 0.5f,-0.5f, 0,1,0,    0,1,
                 0.5f, 0.5f,-0.5f, 0,1,0,    1,1,
                 0.5f, 0.5f, 0.5f, 0,1,0,    1,0,
                -0.5f, 0.5f, 0.5f, 0,1,0,    0,0,
            };

            uint[] indices = {
                0,1,2, 2,3,0,
                4,5,6, 6,7,4,
                8,9,10, 10,11,8,
                12,13,14, 14,15,12,
                16,17,18, 18,19,16,
                20,21,22, 22,23,20
            };

            _tableVao = GL.GenVertexArray();
            _tableVbo = GL.GenBuffer();
            _tableEbo = GL.GenBuffer();

            GL.BindVertexArray(_tableVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _tableVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _tableEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            int stride = (3 + 3 + 2) * sizeof(float);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.BindVertexArray(0);
        }
    }
}