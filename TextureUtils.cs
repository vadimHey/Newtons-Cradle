using System.Drawing;
using OpenTK.Graphics.OpenGL4;

namespace NewtonsCradle
{
    public static class TextureUtils
    {
        public static int LoadTextureFallback(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("Texture not found: " + path + " — using single-color fallback.");
                int t = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, t);
                byte[] col = new byte[] { 200, 160, 110, 255 };
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.UnsignedByte, col);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                return t;
            }
            return LoadTextureStandalone(path);
        }

        public static int LoadTextureStandalone(string path)
        {
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            using (var bmp = new Bitmap(path))
            {
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp.Width, bmp.Height, 0,
                    OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
                bmp.UnlockBits(data);
            }
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)OpenTK.Graphics.OpenGL4.TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)OpenTK.Graphics.OpenGL4.TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            return tex;
        }

        public static int LoadTextureFromMemory(byte[] data)
        {
            // Используется для embedded textures из Assimp (compressed data)
            int t = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, t);
            using (var ms = new MemoryStream(data))
            using (var bmp = new Bitmap(ms))
            {
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                var bits = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp.Width, bmp.Height, 0,
                    OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, bits.Scan0);
                bmp.UnlockBits(bits);
            }
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)OpenTK.Graphics.OpenGL4.TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)OpenTK.Graphics.OpenGL4.TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            return t;
        }
    }
}