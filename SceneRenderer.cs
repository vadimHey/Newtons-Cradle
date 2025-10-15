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
            string modelPath = Path.Combine("Assets", "newtons_cradle.glb");
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
                            Console.WriteLine("⚠️ _model.Meshes пуст — возможно, модель не содержит мешей?");
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
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "model"), false, ref tableModel);
            GL.BindVertexArray(_tableVao);
            GL.DrawElements(OpenTK.Graphics.OpenGL4.PrimitiveType.Triangles, 36, DrawElementsType.UnsignedInt, 0);

            // Модель (каждый меш со своей текстурой)
            if (_model.Scene != null)
            {
                if (!_localTransformsBuilt)
                {
                    _model.BuildLocalTransforms(_model.Scene.RootNode, _localTransforms);
                    _localTransformsBuilt = true;
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