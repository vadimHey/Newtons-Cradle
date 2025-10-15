using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;

using OTQuat = OpenTK.Mathematics.Quaternion;

namespace NewtonsCradle
{
    // Класс для загрузки glTF через AssimpNet и представления мешей для рендеринга
    public class ModelLoader
    {
        public Assimp.Scene Scene { get; private set; }
        private List<MeshGL> _meshes = new();

        public void LoadFromFile(string path)
        {
            var importer = new AssimpContext();
            Scene = importer.ImportFile(path,
                PostProcessSteps.Triangulate |
                PostProcessSteps.GenerateNormals |
                PostProcessSteps.CalculateTangentSpace);

            if (Scene == null) return;

            foreach (var m in _meshes) m.Delete();
            _meshes.Clear();

            foreach (var m in Scene.Meshes)
            {
                var mg = new MeshGL(m, Scene);
                mg.UploadToGL();
                _meshes.Add(mg);
            }
        }

        // Заполнить словарь локальных трансформов (nodeName -> local matrix)
        public void BuildLocalTransforms(Node node, Dictionary<string, Matrix4> outDict)
        {
            outDict[node.Name] = ToMatrix4(node.Transform);
            foreach (var ch in node.Children) BuildLocalTransforms(ch, outDict);
        }

        // Применить анимацию (если есть)
        public void ApplyAnimation(double animTimeSec, Dictionary<string, Matrix4> localDict)
        {
            if (Scene == null || !Scene.HasAnimations) return;
            var anim = Scene.Animations[0];
            double ticksPerSec = anim.TicksPerSecond;
            if (ticksPerSec == 0) ticksPerSec = 25.0;
            double t = animTimeSec * ticksPerSec;
            double animTicks = anim.DurationInTicks;
            if (animTicks <= 0) animTicks = 1.0;
            double localTime = t % animTicks;

            foreach (var ch in anim.NodeAnimationChannels)
            {
                var nodeName = ch.NodeName;
                var pos = InterpVectorKeys(ch.PositionKeys, localTime);
                var rot = InterpQuatKeys(ch.RotationKeys, localTime);
                var scl = InterpVectorKeys(ch.ScalingKeys, localTime);

                var transM = Matrix4.CreateTranslation((float)pos.X, (float)pos.Y, (float)pos.Z);
                // Явно используем OpenTK.Quat (OTQuat) — чтобы не путать с Assimp.Quaternion
                var q = new OTQuat((float)rot.X, (float)rot.Y, (float)rot.Z, (float)rot.W);
                var rotM = Matrix4.CreateFromQuaternion(q);
                var scaleM = Matrix4.CreateScale((float)scl.X, (float)scl.Y, (float)scl.Z);

                // порядок: translation * rotation * scale
                localDict[nodeName] = transM * rotM * scaleM;
            }
        }

        public void Draw(int shaderProgram, Dictionary<string, Matrix4> localTransforms, int fallbackTexture)
        {
            if (Scene == null) return;
            DrawNodeRecursive(Scene.RootNode, Matrix4.Identity, localTransforms, shaderProgram, fallbackTexture);
        }

        private void DrawNodeRecursive(Node node, Matrix4 parentWorld, Dictionary<string, Matrix4> localTransforms, int shaderProgram, int fallbackTexture)
        {
            var local = localTransforms.ContainsKey(node.Name) ? localTransforms[node.Name] : ToMatrix4(node.Transform);
            var world = parentWorld * local;

            foreach (var mi in node.MeshIndices)
            {
                if (mi >= 0 && mi < _meshes.Count)
                {
                    var mg = _meshes[mi];
                    GL.UniformMatrix4(GL.GetUniformLocation(shaderProgram, "model"), false, ref world);

                    if (mg.TextureId != 0)
                    {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, mg.TextureId);
                    }
                    else
                    {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, fallbackTexture);
                    }

                    GL.BindVertexArray(mg.Vao);
                    // Явно указываем OpenTK.Graphics.OpenGL4.PrimitiveType, чтобы не было конфликта с Assimp.PrimitiveType
                    GL.DrawElements(OpenTK.Graphics.OpenGL4.PrimitiveType.Triangles, mg.IndexCount, DrawElementsType.UnsignedInt, 0);
                }
            }

            foreach (var ch in node.Children) DrawNodeRecursive(ch, world, localTransforms, shaderProgram, fallbackTexture);
        }

        public void Delete()
        {
            foreach (var m in _meshes) m.Delete();
            _meshes.Clear();
        }

        #region Helpers: conversion & interpolation

        static Matrix4 ToMatrix4(Assimp.Matrix4x4 m) => new Matrix4(
            m.A1, m.B1, m.C1, m.D1,
            m.A2, m.B2, m.C2, m.D2,
            m.A3, m.B3, m.C3, m.D3,
            m.A4, m.B4, m.C4, m.D4
        );

        static Assimp.Vector3D InterpVectorKeys(List<Assimp.VectorKey> keys, double time)
        {
            if (keys == null || keys.Count == 0) return new Assimp.Vector3D();
            if (keys.Count == 1) return keys[0].Value;
            int idx = 0;
            while (idx < keys.Count - 1 && keys[idx + 1].Time <= time) idx++;
            int next = Math.Min(idx + 1, keys.Count - 1);
            if (idx == next) return keys[idx].Value;
            double t0 = keys[idx].Time, t1 = keys[next].Time;
            double f = (time - t0) / (t1 - t0);
            var a = keys[idx].Value; var b = keys[next].Value;
            return a + (b - a) * (float)f;
        }

        static Assimp.Quaternion InterpQuatKeys(List<Assimp.QuaternionKey> keys, double time)
        {
            if (keys == null || keys.Count == 0) return new Assimp.Quaternion(1, 0, 0, 0);
            if (keys.Count == 1) return keys[0].Value;
            int idx = 0;
            while (idx < keys.Count - 1 && keys[idx + 1].Time <= time) idx++;
            int next = Math.Min(idx + 1, keys.Count - 1);
            if (idx == next) return keys[idx].Value;
            double t0 = keys[idx].Time, t1 = keys[next].Time;
            double f = (time - t0) / (t1 - t0);
            var a = keys[idx].Value; var b = keys[next].Value;
            return Assimp.Quaternion.Slerp(a, b, (float)f);
        }

        /// <summary>
        /// Вычисляет AABB модели с учётом трансформаций узлов (world transforms).
        /// Возвращает true, если удалось найти вершины (mesh count > 0).
        /// </summary>
        public bool GetBoundingBoxWorld(out OpenTK.Mathematics.Vector3 minOut, out OpenTK.Mathematics.Vector3 maxOut)
        {
            minOut = new OpenTK.Mathematics.Vector3(float.MaxValue);
            maxOut = new OpenTK.Mathematics.Vector3(float.MinValue);

            if (Scene == null || Scene.RootNode == null) return false;

            // Сначала соберём локальные матрицы всех узлов
            var localDict = new Dictionary<string, OpenTK.Mathematics.Matrix4>();
            BuildLocalTransforms(Scene.RootNode, localDict);

            // Рекурсивно пройдёмся по узлам и вычислим мировые матрицы
            bool anyVertex = false;

            // Локальные переменные для min/max, чтобы избежать ошибки CS1628
            var min = new OpenTK.Mathematics.Vector3(float.MaxValue);
            var max = new OpenTK.Mathematics.Vector3(float.MinValue);

            void Walk(Node node, OpenTK.Mathematics.Matrix4 parentWorld)
            {
                OpenTK.Mathematics.Matrix4 local = localDict.ContainsKey(node.Name) ? localDict[node.Name] : ToMatrix4(node.Transform);
                OpenTK.Mathematics.Matrix4 world = parentWorld * local;

                // для каждого меша этого узла трансформируем вершины
                foreach (int mi in node.MeshIndices)
                {
                    if (mi < 0 || mi >= Scene.MeshCount) continue;
                    var mesh = Scene.Meshes[mi];
                    for (int vi = 0; vi < mesh.VertexCount; vi++)
                    {
                        var v = mesh.Vertices[vi]; // Assimp.Vector3D
                        var v4 = new OpenTK.Mathematics.Vector4((float)v.X, (float)v.Y, (float)v.Z, 1.0f);
                        var vt = OpenTK.Mathematics.Vector4.TransformRow(v4, world);
                        var vt3 = new OpenTK.Mathematics.Vector3(vt.X, vt.Y, vt.Z);

                        // обновляем min/max
                        min.X = Math.Min(min.X, vt3.X);
                        min.Y = Math.Min(min.Y, vt3.Y);
                        min.Z = Math.Min(min.Z, vt3.Z);
                        max.X = Math.Max(max.X, vt3.X);
                        max.Y = Math.Max(max.Y, vt3.Y);
                        max.Z = Math.Max(max.Z, vt3.Z);

                        anyVertex = true;
                    }
                }

                foreach (var ch in node.Children) Walk(ch, world);
            }

            Walk(Scene.RootNode, OpenTK.Mathematics.Matrix4.Identity);

            // Копируем значения из локальных переменных в out-параметры
            minOut = min;
            maxOut = max;

            return anyVertex;
        }
    }
    #endregion

    #region Nested MeshGL
    class MeshGL
    {
        public int Vao;
        public int Vbo;
        public int Ebo;
        public int IndexCount;
        public int TextureId;
        public Assimp.Mesh SourceMesh;
        public Assimp.Scene ParentScene;

        public MeshGL(Assimp.Mesh m, Assimp.Scene scene)
        {
            SourceMesh = m;
            ParentScene = scene;
            Vao = Vbo = Ebo = 0;
            TextureId = 0;
        }

        public void UploadToGL()
        {
            var m = SourceMesh;
            var verts = new List<float>();
            for (int i = 0; i < m.VertexCount; i++)
            {
                var v = m.Vertices[i];
                var n = (m.Normals != null && m.Normals.Count > i) ? m.Normals[i] : new Assimp.Vector3D(0, 1, 0);
                Assimp.Vector3D uv = new Assimp.Vector3D(0, 0, 0);
                if (m.TextureCoordinateChannelCount > 0 && m.TextureCoordinateChannels[0].Count > i)
                    uv = m.TextureCoordinateChannels[0][i];

                verts.Add(v.X); verts.Add(v.Y); verts.Add(v.Z);
                verts.Add(n.X); verts.Add(n.Y); verts.Add(n.Z);
                verts.Add(uv.X); verts.Add(uv.Y);
            }

            var indices = new List<uint>();
            for (int f = 0; f < m.FaceCount; f++)
            {
                var face = m.Faces[f];
                if (face.IndexCount == 3)
                {
                    indices.Add((uint)face.Indices[0]);
                    indices.Add((uint)face.Indices[1]);
                    indices.Add((uint)face.Indices[2]);
                }
            }

            IndexCount = indices.Count;
            Vao = GL.GenVertexArray();
            Vbo = GL.GenBuffer();
            Ebo = GL.GenBuffer();

            GL.BindVertexArray(Vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Count * sizeof(float), verts.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StaticDraw);

            int stride = (3 + 3 + 2) * sizeof(float);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.BindVertexArray(0);

            // Попытка загрузить diffuse-текстуру из материала
            try
            {
                var matIndex = m.MaterialIndex;
                if (matIndex >= 0 && ParentScene != null && ParentScene.Materials.Count > matIndex)
                {
                    var mat = ParentScene.Materials[matIndex];
                    if (mat.GetMaterialTextureCount(TextureType.Diffuse) > 0)
                    {
                        mat.GetMaterialTexture(TextureType.Diffuse, 0, out TextureSlot slot);
                        string path = slot.FilePath;
                        if (!string.IsNullOrEmpty(path))
                        {
                            if (path.StartsWith("*"))
                            {
                                int texIndex = int.Parse(path.Substring(1));
                                if (ParentScene.HasTextures && ParentScene.Textures.Count > texIndex)
                                {
                                    var embedded = ParentScene.Textures[texIndex];
                                    // CompressedData (byte[]) — обычный случай
                                    if (embedded.CompressedData != null && embedded.CompressedData.Length > 0)
                                    {
                                        TextureId = TextureUtils.LoadTextureFromMemory(embedded.CompressedData);
                                    }
                                    // NonCompressedData — Assimp.Texel[] — нужно конвертировать в Bitmap
                                    else if (embedded.HasNonCompressedData && embedded.NonCompressedData != null)
                                    {
                                        try
                                        {
                                            var texels = embedded.NonCompressedData; // Assimp.Texel[]
                                            int w = embedded.Width > 0 ? embedded.Width : 1;
                                            int h = embedded.Height > 0 ? embedded.Height : 1;
                                            using (var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                                            {
                                                int idx = 0;
                                                for (int yy = 0; yy < h; yy++)
                                                {
                                                    for (int xx = 0; xx < w; xx++)
                                                    {
                                                        var t = texels[idx++]; // t is Assimp.Texel
                                                                               // Texel has channels R,G,B,A (bytes) — формируем Color
                                                        Color c = Color.FromArgb(t.A, t.R, t.G, t.B);
                                                        bmp.SetPixel(xx, yy, c);
                                                    }
                                                }
                                                // Сохраним в поток PNG и загрузим как CompressedData
                                                using (var ms = new MemoryStream())
                                                {
                                                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                                    TextureId = TextureUtils.LoadTextureFromMemory(ms.ToArray());
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine("Failed to create texture from NonCompressedData: " + ex.Message);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var candidate = Path.Combine("Assets", Path.GetFileName(path));
                                if (File.Exists(candidate)) TextureId = TextureUtils.LoadTextureStandalone(candidate);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Texture load error for mesh: " + ex.Message);
            }
        }

        public void Delete()
        {
            if (Vao != 0) GL.DeleteVertexArray(Vao);
            if (Vbo != 0) GL.DeleteBuffer(Vbo);
            if (Ebo != 0) GL.DeleteBuffer(Ebo);
            if (TextureId != 0) GL.DeleteTexture(TextureId);
        }
    }
    #endregion
}