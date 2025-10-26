using NewtonsCradle.Models;
using NewtonsCradle.Utils;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace NewtonsCradle
{
    public class MainWindow : GameWindow
    {
        private int _shaderProgram;
        private int _shadowShaderProgram;

        // Камера
        private Camera _camera;

        // Модели стола и маятника
        private TableModel _table;
        private CradleModel _cradle;
        private LampModel _lamp;

        private Vector3 _lightPosition = new Vector3(-2.0f, 4.0f, 1.5f);

        private int _depthMapFBO;
        private int _depthMap;
        private const int SHADOW_WIDTH = 2048, SHADOW_HEIGHT = 2048;
        private Matrix4 _lightSpaceMatrix;

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
            _depthMapFBO = GL.GenFramebuffer();
            _depthMap = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, _depthMap);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent,
                SHADOW_WIDTH, SHADOW_HEIGHT, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            float[] borderColor = { 1, 1, 1, 1 };
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, borderColor);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _depthMapFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _depthMap, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

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
            Matrix4 lightProjection = Matrix4.CreateOrthographic(10f, 10f, 1f, 20f);
            Matrix4 lightView = Matrix4.LookAt(_lightPosition, Vector3.Zero, Vector3.UnitY);
            _lightSpaceMatrix = lightView * lightProjection;

            // Настройка буфера для теней
            GL.Viewport(0, 0, SHADOW_WIDTH, SHADOW_HEIGHT);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _depthMapFBO);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            // Используем шейдер теней
            GL.UseProgram(_shadowShaderProgram);
            GL.UniformMatrix4(GL.GetUniformLocation(_shadowShaderProgram, "lightSpaceMatrix"), false, ref _lightSpaceMatrix);

            _cradle.Draw(_shadowShaderProgram);

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

            // Активируем shadow map
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _depthMap);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "shadowMap"), 1);

            // Позиция света и матрица света
            Vector3 lampPos = new Vector3(-1.3f, -0.7f, 0f);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "lightPos"), lampPos);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "lightSpaceMatrix"), false, ref _lightSpaceMatrix);

            // Модели стола и маятника
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
        }
    }
}