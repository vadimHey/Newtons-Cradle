using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

using NewtonsCradle.Models;

namespace NewtonsCradle
{
    public class MainWindow : GameWindow
    {
        // Шейдерные программы
        private int _shaderProgram;
        private int _shadowShaderProgram;

        // Камера
        private Camera _camera;

        // Модели стола и маятника
        private TableModel _table;
        private CradleModel _cradle;
        private LampModel _lamp;

        // Позиция светв
        private Vector3 _lightPosition = new Vector3(-2.0f, 4.0f, 1.5f);

        // Создание теней
        private ShadowMap _shadowMap;

        public MainWindow(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.85f, 0.9f, 0.95f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            _camera = new Camera(Vector3.Zero, 2f, Size.X / (float)Size.Y, 10f);

            // Шейдер
            _shaderProgram = ShaderUtils.CreateProgram("Shaders/vertex.glsl", "Shaders/fragment.glsl");
            _shadowShaderProgram = ShaderUtils.CreateProgram("Shaders/vertexShadow.glsl", "Shaders/fragmentShadow.glsl");

            GL.UseProgram(_shaderProgram);

            // Стол
            _table = new TableModel();
            int tableTex = TextureUtils.LoadTextureStandalone(Path.Combine("Assets", "woodTable.jpg"));
            _table.SetTexture(tableTex);

            // Маятник
            _cradle = new CradleModel();
            _cradle.Load(Path.Combine("Assets", "newtons_cradle.glb"), _camera);

            // Лампа
            _lamp = new LampModel();
            _lamp.Load("Assets/lamp.glb");

            // Настройка буфера теней
            _shadowMap = new ShadowMap();

            // Передача шейдеру текстуру и свет
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "texture0"), 0);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "lightPos"), _lightPosition);
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
                _cradle.ToggleAnimation();

            _cradle.Update((float)e.Time);

            // Закрытие окна
            if (KeyboardState.IsKeyDown(Keys.Escape)) Close();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            // Матрица света 
            _shadowMap.BeginRender(_lightPosition);

            GL.UseProgram(_shadowShaderProgram);
            var lightMatrix = _shadowMap.LightSpaceMatrix;
            GL.UniformMatrix4(GL.GetUniformLocation(_shadowShaderProgram, "lightSpaceMatrix"), false, ref lightMatrix);

            _cradle.Draw(_shadowShaderProgram);

            _shadowMap.EndRender(Size.X, Size.Y);

            GL.Viewport(0, 0, Size.X, Size.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.UseProgram(_shaderProgram);

            // Матрицы камеры
            var view = _camera.GetViewMatrix();
            var proj = _camera.GetProjectionMatrix();
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "projection"), false, ref proj);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "viewPos"), _camera.Position);

            // Активируем shadow map
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _shadowMap.DepthMap);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "shadowMap"), 1);

            // Позиция света и матрица света
            Vector3 lampPos = new Vector3(-1.3f, -0.7f, 0f);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "lightPos"), lampPos);
            GL.UniformMatrix4(GL.GetUniformLocation(_shadowShaderProgram, "lightSpaceMatrix"), false, ref lightMatrix);

            // Модели лампы, стола и маятника
            _lamp.Draw(_shaderProgram, lampPos);
            _table.Draw(_shaderProgram);
            _cradle.Draw(_shaderProgram);

            SwapBuffers();
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            GL.DeleteProgram(_shaderProgram);
            GL.DeleteProgram(_shadowShaderProgram);
            GL.DeleteVertexArray(_table.VAO);
            GL.DeleteTexture(_table.TextureId);
            _cradle.Model.Delete();
            _lamp.Model.Delete();
            _shadowMap.Delete();
        }
    }
}