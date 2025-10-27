using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;

namespace NewtonsCradle
{
    // Класс для загрузки glTF через AssimpNet и представления мешей для рендеринга
    public class ModelLoader
    {
        public Assimp.Scene Scene { get; private set; }
        private readonly List<MeshGL> _meshes = new();

        public IReadOnlyList<MeshGL> Meshes => _meshes;

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

        // Заполнить словарь локальных трансформов
        public void BuildLocalTransforms(Node node, Dictionary<string, Matrix4> outDict)
        {
            outDict[node.Name] = ToMatrix4(node.Transform);
            foreach (var ch in node.Children) BuildLocalTransforms(ch, outDict);
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
                    GL.DrawElements(PrimitiveType.Triangles, mg.IndexCount, DrawElementsType.UnsignedInt, 0);
                }
            }

            foreach (var ch in node.Children) DrawNodeRecursive(ch, world, localTransforms, shaderProgram, fallbackTexture);
        }

        public void Delete()
        {
            foreach (var m in _meshes) 
                m.Delete();
            _meshes.Clear();
        }

        static Matrix4 ToMatrix4(Assimp.Matrix4x4 m) => new Matrix4(
            m.A1, m.B1, m.C1, m.D1,
            m.A2, m.B2, m.C2, m.D2,
            m.A3, m.B3, m.C3, m.D3,
            m.A4, m.B4, m.C4, m.D4
        );

        /// <summary>
        /// Вычисляет AABB модели с учётом трансформаций узлов
        /// Возвращает true, если удалось найти вершины
        /// </summary>
        public bool GetBoundingBoxWorld(out Vector3 minOut, out Vector3 maxOut)
        {
            minOut = new Vector3(float.MaxValue);
            maxOut = new Vector3(float.MinValue);

            if (Scene == null || Scene.RootNode == null) return false;

            var localDict = new Dictionary<string, Matrix4>();
            BuildLocalTransforms(Scene.RootNode, localDict);

            bool anyVertex = false;

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            void Walk(Node node, Matrix4 parentWorld)
            {
                Matrix4 local = localDict.ContainsKey(node.Name) ? localDict[node.Name] : ToMatrix4(node.Transform);
                Matrix4 world = parentWorld * local;

                foreach (int mi in node.MeshIndices)
                {
                    if (mi < 0 || mi >= Scene.MeshCount) continue;
                    var mesh = Scene.Meshes[mi];
                    for (int vi = 0; vi < mesh.VertexCount; vi++)
                    {
                        var v = mesh.Vertices[vi]; 
                        var v4 = new Vector4((float)v.X, (float)v.Y, (float)v.Z, 1.0f);
                        var vt = Vector4.TransformRow(v4, world);
                        var vt3 = new Vector3(vt.X, vt.Y, vt.Z);

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

            Walk(Scene.RootNode, Matrix4.Identity);

            minOut = min;
            maxOut = max;

            return anyVertex;
        }
    }

    public class MeshGL
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
                    try
                    {
                        TextureSlot slot;
                        bool found = false;

                        if (mat.GetMaterialTextureCount(TextureType.Diffuse) > 0)
                        {
                            mat.GetMaterialTexture(TextureType.Diffuse, 0, out slot);
                            found = true;
                        }
                        else if (mat.GetMaterialTextureCount((TextureType)12) > 0) 
                        {
                            mat.GetMaterialTexture((TextureType)12, 0, out slot);
                            found = true;
                        }
                        else if (mat.GetMaterialTextureCount(TextureType.Unknown) > 0)
                        {
                            mat.GetMaterialTexture(TextureType.Unknown, 0, out slot);
                            found = true;
                        }
                        else
                        {
                            slot = new TextureSlot(); 
                        }

                        if (found)
                        {
                            string path = slot.FilePath;
                            if (!string.IsNullOrEmpty(path))
                            {
                                if (path.StartsWith("*"))
                                {
                                    int texIndex = int.Parse(path.Substring(1));
                                    if (ParentScene.HasTextures && ParentScene.Textures.Count > texIndex)
                                    {
                                        var embedded = ParentScene.Textures[texIndex];
                                        if (embedded.CompressedData != null && embedded.CompressedData.Length > 0)
                                        {
                                            TextureId = TextureUtils.LoadTextureFromMemory(embedded.CompressedData);
                                        }
                                    }
                                }
                                else
                                {
                                    var candidate = Path.Combine("Assets", Path.GetFileName(path));
                                    if (File.Exists(candidate))
                                        TextureId = TextureUtils.LoadTextureStandalone(candidate);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Ошибка загрузки текстуры: " + ex.Message);
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка загрузки текстуры для меша: " + ex.Message);
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
}