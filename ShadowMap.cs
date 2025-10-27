using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace NewtonsCradle
{
    public class ShadowMap
    {
        public int DepthMapFBO { get; private set; }
        public int DepthMap { get; private set; }
        public Matrix4 LightSpaceMatrix { get; private set; }

        private readonly int _width;
        private readonly int _height;

        public ShadowMap(int width = 2048, int height = 2048)
        {
            _width = width;
            _height = height;
            Initialize();
        }

        private void Initialize()
        {
            DepthMapFBO = GL.GenFramebuffer();
            DepthMap = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, DepthMap);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent,
                _width, _height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            float[] borderColor = { 1f, 1f, 1f, 1f };
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, borderColor);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, DepthMapFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, DepthMap, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void BeginRender(Vector3 lightPos, float orthoSize = 10f, float near = 1f, float far = 20f)
        {
            Matrix4 lightProjection = Matrix4.CreateOrthographic(orthoSize, orthoSize, near, far);
            Matrix4 lightView = Matrix4.LookAt(lightPos, Vector3.Zero, Vector3.UnitY);
            LightSpaceMatrix = lightView * lightProjection;

            GL.Viewport(0, 0, _width, _height);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, DepthMapFBO);
            GL.Clear(ClearBufferMask.DepthBufferBit);
        }

        public void EndRender(int windowWidth, int windowHeight)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, windowWidth, windowHeight);
        }

        public void Delete()
        {
            GL.DeleteFramebuffer(DepthMapFBO);
            GL.DeleteTexture(DepthMap);
        }
    }
}