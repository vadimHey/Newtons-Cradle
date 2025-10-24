using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace NewtonsCradle
{
    public class SceneRenderer : GameWindow
    {
        private int _shaderProgram;

        // Камера
        private OrbitCamera _camera;

        // Стол и его ножки
        private int _tableVao, _tableVbo, _tableEbo;
        private int _tableTexture;
        private Matrix4[] _tableLegTransforms;

        // Модель лампы
        private ModelLoader _lampModel;
        private Dictionary<string, Matrix4> _originalTransformsLamp = new();
        private Dictionary<string, Matrix4> _localTransformsLamp = new();

        // Модель маятника
        private ModelLoader _cradleModel = new ModelLoader();
        private Dictionary<string, Matrix4> _modelLocalTransforms = new();
        private bool _localTransformsCradle = false;
        private bool _animationRunning = false;

        // Сохраняем исходные матрицы всех узлов модели
        private Dictionary<string, Matrix4> _originalTransforms = new Dictionary<string, Matrix4>();

        // Pivot-точки для каждого подвеса (Hook)
        private readonly Dictionary<string, Vector3> _pivotWorlds = new();

        private int _activePendulum = 0;
        private float _pendulumTime = 0f;
        private const float swingDuration = 1f;

        private Vector3 _lightPos = new Vector3(-2.0f, 4.0f, 1.5f);

        private int _depthMapFBO;
        private int _depthMap;
        private int _shadowShaderProgram;
        private const int SHADOW_WIDTH = 2048, SHADOW_HEIGHT = 2048;
        private Matrix4 _lightSpaceMatrix;

        public SceneRenderer(GameWindowSettings g, NativeWindowSettings n) : base(g, n) { }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.85f, 0.9f, 0.95f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            _camera = new OrbitCamera(Vector3.Zero, 2f, Size.X / (float)Size.Y, 10f);

            // Шейдер
            _shaderProgram = ShaderUtils.CreateProgram("Shaders/vertex.glsl", "Shaders/fragment.glsl");
            GL.UseProgram(_shaderProgram);

            // Стол
            CreateTable();
            _tableTexture = TextureUtils.LoadTextureStandalone(Path.Combine("Assets", "woodTable.jpg"));

            // Лампа
            LoadLampModelOnTable("Assets/lamp.glb");

            // Загрузка модели
            string modelPath = Path.Combine("Assets", "newtons_cradle.glb");
            if (!File.Exists(modelPath))
            {
                Console.WriteLine("Model not found: " + modelPath);
            }
            else
            {
                _cradleModel.LoadFromFile(modelPath);

                if (_cradleModel.Scene != null)
                {
                    var world = BuildWorldTransformsFromOriginal();
                    foreach (var name in new[]
                    {
                        "polySurface19_Hook_0", "polySurface18_Hook_0",
                        "polySurface17_Hook_0", "polySurface16_Hook_0",
                        "polySurface20_Hook_0"
                    })
                    {
                        _pivotWorlds[name] = ComputeNodePivotWorld(name, world);
                    }

                    // Локальные трансформы
                    _cradleModel.BuildLocalTransforms(_cradleModel.Scene.RootNode, _modelLocalTransforms);
                    foreach (var kv in _modelLocalTransforms)
                    {
                        _originalTransforms[kv.Key] = kv.Value;
                        _modelLocalTransforms[kv.Key] = kv.Value;
                    }
                    _localTransformsCradle = true;

                    // Подмена текстур по названию мешей
                    try
                    {
                        int woodTex = TextureUtils.LoadTextureStandalone(Path.Combine("Assets", "wood.jpg"));
                        int woodTableTex = TextureUtils.LoadTextureStandalone(Path.Combine("Assets", "woodTable.jpg"));
                        int metalTex = TextureUtils.LoadTextureStandalone(Path.Combine("Assets", "metal.jpg"));

                        if (_cradleModel.Meshes != null && _cradleModel.Meshes.Count > 0)
                        {
                            foreach (var mesh in _cradleModel.Meshes)
                            {
                                string name = mesh.SourceMesh?.Name ?? "";

                                if (name.Contains("Column", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Деревянные стойки маятника
                                    mesh.TextureId = woodTex;
                                }
                                else if (name.Contains("Flor", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Основание
                                    mesh.TextureId = woodTex;
                                }
                                else if (name.Contains("Ball", StringComparison.OrdinalIgnoreCase) ||
                                         name.Contains("Hook", StringComparison.OrdinalIgnoreCase) ||
                                         name.Contains("Wire", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Металлические части
                                    mesh.TextureId = metalTex;
                                }
                                else
                                {
                                    // На всякий случай, чтобы ничего не осталось без текстуры
                                    mesh.TextureId = metalTex;
                                }
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
                    if (_cradleModel.GetBoundingBoxWorld(out var bboxMin, out var bboxMax))
                    {
                        var center = (bboxMin + bboxMax) * 0.5f;
                        var diag = (bboxMax - bboxMin).Length;
                        _camera.Target = center;
                        _camera.Radius = Math.Max(0.5f, diag * 0.9f);
                    }
                }
            }

            _depthMapFBO = GL.GenFramebuffer();
            _depthMap = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, _depthMap);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent,
                SHADOW_WIDTH, SHADOW_HEIGHT, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            float[] borderColor = { 1.0f, 1.0f, 1.0f, 1.0f };
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, borderColor);

            _depthMapFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _depthMapFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D, _depthMap, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // Загружаем шейдер для рендера глубины
            _shadowShaderProgram = ShaderUtils.CreateProgram("Shaders/vertexShadow.glsl", "Shaders/fragmentShadow.glsl");

            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "texture0"), 0);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "lightPos"), _lightPos);
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            GL.DeleteProgram(_shaderProgram);
            GL.DeleteVertexArray(_tableVao);
            GL.DeleteTexture(_tableTexture);
            _lampModel.Delete();
            _cradleModel.Delete();
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

            // Скорости вращения и приближения камеры
            float rotationSpeed = 90f * (float)e.Time;
            float zoomSpeed = 5f * (float)e.Time;

            // Управление камерой
            if (KeyboardState.IsKeyDown(Keys.E))
                _camera.RotateLeft(rotationSpeed);
            if (KeyboardState.IsKeyDown(Keys.Q))
                _camera.RotateRight(rotationSpeed);

            if (KeyboardState.IsKeyDown(Keys.Equal))
                _camera.ZoomIn(zoomSpeed);
            if (KeyboardState.IsKeyDown(Keys.Minus))
                _camera.ZoomOut(zoomSpeed);

            // Анимация на клавишу пробела
            if (KeyboardState.IsKeyPressed(Keys.Space))
            {
                _animationRunning = !_animationRunning;
            }

            // Закрытие окна
            if (KeyboardState.IsKeyDown(Keys.Escape)) Close();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            // Матрица света (лампа направлена на маятник)
            Matrix4 lightProjection = Matrix4.CreateOrthographic(10f, 10f, 1f, 20f);
            Matrix4 lightView = Matrix4.LookAt(_lightPos, Vector3.Zero, Vector3.UnitY);
            _lightSpaceMatrix = lightView * lightProjection;

            // Настройка буфера для теней
            GL.Viewport(0, 0, SHADOW_WIDTH, SHADOW_HEIGHT);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _depthMapFBO);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            // Используем шейдер теней
            GL.UseProgram(_shadowShaderProgram);
            GL.UniformMatrix4(GL.GetUniformLocation(_shadowShaderProgram, "lightSpaceMatrix"), false, ref _lightSpaceMatrix);
            DrawSceneDepthOnly();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.Viewport(0, 0, Size.X, Size.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.UseProgram(_shaderProgram);

            // Матрицы камеры
            var view = _camera.GetViewMatrix();
            var proj = _camera.GetProjectionMatrix();
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "projection"), false, ref proj);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "viewPos"), _camera.Position);

            // Позиция света и матрица света
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "lightPos"), _lightPos);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "lightSpaceMatrix"), false, ref _lightSpaceMatrix);

            // Активируем shadow map
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _depthMap);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "shadowMap"), 1);

            // Стол и его ножки
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _tableTexture);
            var tableModel = Matrix4.CreateScale(12.0f, 0.65f, 9.0f) *
                             Matrix4.CreateTranslation(-0.5f, -1.75f, -1.5f);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "model"), false, ref tableModel);
            GL.BindVertexArray(_tableVao);
            GL.DrawElements(PrimitiveType.Triangles, 36, DrawElementsType.UnsignedInt, 0);
            GL.BindTexture(TextureTarget.Texture2D, _tableTexture);
            GL.BindVertexArray(_tableVao);
            for (int i = 0; i < _tableLegTransforms.Length; i++)
            {
                GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "model"), false, ref _tableLegTransforms[i]);
                GL.DrawElements(PrimitiveType.Triangles, 36, DrawElementsType.UnsignedInt, 0);
            }

            // Модель лампы
            if (_lampModel != null && _lampModel.Scene != null)
            {
                var lampModel = Matrix4.CreateTranslation(-1.3f, -0.7f, 0f);
                var finalTransforms = new Dictionary<string, Matrix4>();
                foreach (var kv in _localTransformsLamp)
                    finalTransforms[kv.Key] = lampModel * kv.Value;

                int testTex = TextureUtils.LoadTextureStandalone("Assets/lamp.jpg");
                foreach (var mesh in _lampModel.Meshes)
                {
                    mesh.TextureId = testTex;
                }

                // Обновляем позицию света
                Vector3 lampWorldPos = lampModel.ExtractTranslation();
                GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "lightPos"), lampWorldPos);

                // Передаём позицию камеры
                GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "viewPos"), _camera.Position);

                _lampModel.Draw(_shaderProgram, finalTransforms, 0);
            }

            // Модель маятника
            if (_cradleModel.Scene != null)
            {
                // Обновление таймера и вычисление angle
                if (_animationRunning)
                    _pendulumTime += (float)e.Time;

                if (_pendulumTime > swingDuration)
                {
                    _pendulumTime = 0f;
                    _activePendulum = 1 - _activePendulum;
                }

                float t = _pendulumTime / swingDuration;

                float swingFactor = -MathF.Sin(t * MathF.PI);
                float baseAngle = 0.3f;

                float[] angles = new float[5];
                if (_activePendulum == 0)
                    angles[0] = baseAngle * swingFactor;
                else
                    angles[4] = -baseAngle * swingFactor;

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

                foreach (var kv in _originalTransforms)
                    _modelLocalTransforms[kv.Key] = kv.Value;

                var worldTransforms = BuildWorldTransformsFromOriginal();

                // При вычислении поворота для каждого подвеса:
                for (int p = 0; p < pendulums.Length; p++)
                {
                    float localAngle = angles[p];
                    if (MathF.Abs(localAngle) < 1e-5f)
                        continue;

                    string hookNodeName = pendulums[p][0];
                    if (!_pivotWorlds.TryGetValue(hookNodeName, out var pivotWorld))
                        pivotWorld = ComputeNodePivotWorld(hookNodeName, worldTransforms);

                    foreach (string nodeName in pendulums[p])
                    {
                        if (_originalTransforms.TryGetValue(nodeName, out var baseMatrix))
                        {
                            var rotation =
                                Matrix4.CreateTranslation(-pivotWorld) *
                                Matrix4.CreateFromAxisAngle(Vector3.UnitZ, localAngle) *
                                Matrix4.CreateTranslation(pivotWorld);

                            _modelLocalTransforms[nodeName] = rotation * baseMatrix;
                        }
                    }
                }

                _cradleModel.Draw(_shaderProgram, _modelLocalTransforms, 0);
            }

            SwapBuffers();
        }

        private void CreateTable()
        {
            float[] vertices = {
                // Позиции              // Нормали // Текстурные координаты
                -0.5f,-0.5f,-0.5f,      0,0,-1,     0,0,
                 0.5f,-0.5f,-0.5f,      0,0,-1,     1,0,
                 0.5f, 0.5f,-0.5f,      0,0,-1,     1,1,
                -0.5f, 0.5f,-0.5f,      0,0,-1,     0,1,
                -0.5f,-0.5f, 0.5f,      0,0,1,      0,0,
                 0.5f,-0.5f, 0.5f,      0,0,1,      1,0,
                 0.5f, 0.5f, 0.5f,      0,0,1,      1,1,
                -0.5f, 0.5f, 0.5f,      0,0,1,      0,1,
                -0.5f, 0.5f, 0.5f,      -1,0,0,     1,0,
                -0.5f, 0.5f,-0.5f,      -1,0,0,     1,1,
                -0.5f,-0.5f,-0.5f,      -1,0,0,     0,1,
                -0.5f,-0.5f, 0.5f,      -1,0,0,     0,0,
                 0.5f, 0.5f, 0.5f,      1,0,0,      1,0,
                 0.5f, 0.5f,-0.5f,      1,0,0,      1,1,
                 0.5f,-0.5f,-0.5f,      1,0,0,      0,1,
                 0.5f,-0.5f, 0.5f,      1,0,0,      0,0,
                -0.5f,-0.5f,-0.5f,      0,-1,0,     0,1,
                 0.5f,-0.5f,-0.5f,      0,-1,0,     1,1,
                 0.5f,-0.5f, 0.5f,      0,-1,0,     1,0,
                -0.5f,-0.5f, 0.5f,      0,-1,0,     0,0,
                -0.5f, 0.5f,-0.5f,      0,1,0,      0,1,
                 0.5f, 0.5f,-0.5f,      0,1,0,      1,1,
                 0.5f, 0.5f, 0.5f,      0,1,0,      1,0,
                -0.5f, 0.5f, 0.5f,      0,1,0,      0,0,
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

            // Матрицы ножек стола
            _tableLegTransforms = new Matrix4[4];

            float legWidth = 0.4f;
            float legHeight = 6.5f;

            // Позиции ножек
            Vector3[] legPositions =
            {
                new Vector3(-5.5f, -5f, -5f),
                new Vector3( 4.5f, -5f, -5f),
                new Vector3(-5.5f, -5f,  2f),
                new Vector3( 4.5f, -5f,  2f)
            };

            for (int i = 0; i < 4; i++)
            {
                _tableLegTransforms[i] =
                    Matrix4.CreateScale(legWidth, legHeight, legWidth) *
                    Matrix4.CreateTranslation(legPositions[i]);
            }
        }

        private void LoadLampModelOnTable(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("Lamp model not found: " + path);
                return;
            }

            _lampModel = new ModelLoader();
            _lampModel.LoadFromFile(path);

            // Сохраняем оригинальные локальные трансформы
            _lampModel.BuildLocalTransforms(_lampModel.Scene.RootNode, _localTransformsLamp);
            foreach (var kv in _localTransformsLamp)
                _originalTransformsLamp[kv.Key] = kv.Value;

            // Загружаем текстуры для каждого меша, если они есть
            foreach (var mesh in _lampModel.Meshes)
            {
                if (mesh.TextureId != 0) continue; // уже загружено

                var mat = _lampModel.Scene.Materials[mesh.SourceMesh.MaterialIndex];

                if (mat.HasTextureDiffuse)
                {
                    // Путь к текстуре относительно папки Assets
                    string texPath = Path.Combine("Assets", mat.TextureDiffuse.FilePath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(texPath))
                    {
                        mesh.TextureId = TextureUtils.LoadTextureStandalone(texPath);
                    }
                }
                else
                {
                    mesh.TextureId = 0;
                }
            }
        }

        // Построить мировые матрицы всех узлов на базе _originalTransforms
        private Dictionary<string, Matrix4> BuildWorldTransformsFromOriginal()
        {
            var world = new Dictionary<string, Matrix4>();
            if (_cradleModel?.Scene?.RootNode == null) return world;

            void Walk(Assimp.Node node, Matrix4 parentWorld)
            {
                Matrix4 local;
                if (!_originalTransforms.TryGetValue(node.Name, out local))
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

            Walk(_cradleModel.Scene.RootNode, Matrix4.Identity);
            return world;
        }

        // Для заданного узла вычислить pivot как среднюю позицию вершин мешей этого узла
        private Vector3 ComputeNodePivotWorld(string nodeName, Dictionary<string, Matrix4> worldTransforms)
        {
            var scene = _cradleModel.Scene;
            if (scene == null || scene.RootNode == null) return Vector3.Zero;
            Assimp.Node node = FindNodeByName(scene.RootNode, nodeName);
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

            if (total > 0) return sum / (float)total;

            return world.ExtractTranslation();
        }

        // Вспомогательный поиск узла по имени
        private Assimp.Node FindNodeByName(Assimp.Node root, string name)
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

        private void DrawSceneDepthOnly()
        {
            if (_cradleModel?.Scene != null)
            {
                _cradleModel.Draw(_shadowShaderProgram, _modelLocalTransforms, 0);
            }
        }
    }
}