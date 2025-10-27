using OpenTK.Mathematics;

namespace NewtonsCradle.Models
{
    public class LampModel
    {
        public ModelLoader Model;
        public Dictionary<string, Matrix4> LocalTransforms = new();
        public Dictionary<string, Matrix4> OriginalTransforms = new();
        public int _textureId;

        // Загружаем модель лампы и текстуру
        public void Load(string modelPath, string texturePath = "Assets/lamp.jpg")
        {
            if (!File.Exists(modelPath))
            {
                Console.WriteLine("Модель лампы не найдена: " + modelPath);
                return;
            }

            Model = new ModelLoader();
            Model.LoadFromFile(modelPath);

            // Строим локальные трансформации
            Model.BuildLocalTransforms(Model.Scene.RootNode, LocalTransforms);
            foreach (var kv in LocalTransforms)
                OriginalTransforms[kv.Key] = kv.Value;

            // Загружаем текстуры для мешей
            foreach (var mesh in Model.Meshes)
            {
                if (mesh.TextureId != 0) continue;

                var mat = Model.Scene.Materials[mesh.SourceMesh.MaterialIndex];

                if (mat.HasTextureDiffuse)
                {
                    string texPath = Path.Combine("Assets", mat.TextureDiffuse.FilePath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(texPath))
                        mesh.TextureId = TextureUtils.LoadTextureStandalone(texPath);
                }
                else
                {
                    mesh.TextureId = 0;
                }
            }

            // Можно задать единичную текстуру, если есть отдельная картинка лампы
            if (File.Exists(texturePath))
                _textureId = TextureUtils.LoadTextureStandalone(texturePath);
        }

        // Рисуем лампу, передавая шейдер и позицию
        public void Draw(int shaderProgram, Vector3 position)
        {
            if (Model == null || Model.Scene == null) return;

            var finalTransforms = new Dictionary<string, Matrix4>();
            foreach (var kv in LocalTransforms)
                finalTransforms[kv.Key] = Matrix4.CreateTranslation(position) * kv.Value;

            // Важные моменты:
            // - Не трогаем lightPos
            // - Не трогаем viewPos
            Model.Draw(shaderProgram, finalTransforms, _textureId);
        }
    }
}