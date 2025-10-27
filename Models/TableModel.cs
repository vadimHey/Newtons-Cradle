using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace NewtonsCradle.Models
{
    public class TableModel
    {
        public int VAO { get; private set; }
        public int VBO { get; private set; }
        public int EBO { get; private set; }
        public Matrix4[] LegTransforms { get; private set; }
        public int TextureId { get; set; }

        public TableModel()
        {
            Create();
        }

        public void SetTexture(int textureId)
        {
            TextureId = textureId;
        }

        private void Create()
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
                0, 1, 2,      2, 3, 0,
                4, 5, 6,      6, 7, 4,
                8, 9, 10,     10,11,8,
                12,13,14,     14,15,12,
                16,17,18,     18,19,16,
                20,21,22,     22,23,20
            };

            VAO = GL.GenVertexArray();
            VBO = GL.GenBuffer();
            EBO = GL.GenBuffer();

            GL.BindVertexArray(VAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
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
            LegTransforms = new Matrix4[4];

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
                LegTransforms[i] =
                    Matrix4.CreateScale(legWidth, legHeight, legWidth) *
                    Matrix4.CreateTranslation(legPositions[i]);
            }
        }

        public void Draw(int shader)
        {
            if (TextureId != 0)
                GL.BindTexture(TextureTarget.Texture2D, TextureId);

            GL.BindVertexArray(VAO);

            // Столешница
            var model = Matrix4.CreateScale(12.0f, 0.65f, 9.0f) *
                        Matrix4.CreateTranslation(-0.5f, -1.75f, -1.5f);
            GL.UniformMatrix4(GL.GetUniformLocation(shader, "model"), false, ref model);
            GL.DrawElements(PrimitiveType.Triangles, 36, DrawElementsType.UnsignedInt, 0);

            // Ножки
            for (int i = 0; i < LegTransforms.Length; i++)
            {
                GL.UniformMatrix4(GL.GetUniformLocation(shader, "model"), false, ref LegTransforms[i]);
                GL.DrawElements(PrimitiveType.Triangles, 36, DrawElementsType.UnsignedInt, 0);
            }

            GL.BindVertexArray(0);
        }
    }
}
