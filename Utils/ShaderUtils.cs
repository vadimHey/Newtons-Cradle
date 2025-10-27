using OpenTK.Graphics.OpenGL4;

namespace NewtonsCradle
{
    public class ShaderUtils
    {
        public static int CreateProgram(string vertexPath, string fragmentPath)
        {
            var vsrc = File.ReadAllText(vertexPath);
            var fsrc = File.ReadAllText(fragmentPath);
            int v = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(v, vsrc);
            GL.CompileShader(v);
            var log = GL.GetShaderInfoLog(v); if (!string.IsNullOrEmpty(log)) Console.WriteLine("Журнал вершинного шейдера:\n" + log);

            int f = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(f, fsrc);
            GL.CompileShader(f);
            log = GL.GetShaderInfoLog(f); if (!string.IsNullOrEmpty(log)) Console.WriteLine("Журнал фргементного шейдера:\n" + log);

            int p = GL.CreateProgram();
            GL.AttachShader(p, v);
            GL.AttachShader(p, f);
            GL.LinkProgram(p);
            GL.GetProgram(p, GetProgramParameterName.LinkStatus, out int status);
            if (status == 0) Console.WriteLine("Журнал ссылок на программы: " + GL.GetProgramInfoLog(p));
            GL.DeleteShader(v);
            GL.DeleteShader(f);
            return p;
        }
    }
}