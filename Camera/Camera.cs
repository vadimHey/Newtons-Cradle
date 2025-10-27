using OpenTK.Mathematics;

namespace NewtonsCradle
{
    public class Camera
    {
        public Vector3 Target;         // Центр сцены
        public float Yaw = -90f;       // Горизонтальный угол в градусах
        public float Radius = 5f;      // Расстояние до цели
        public float FixedY = 1.0f;    // Фиксированная высота камеры
        public float MinRadius = 4f;   // Минимальное приближение
        public float MaxRadius = 12f;  // Максимальное отдаление

        public float AspectRatio;
        public float Fov = 45f;

        // Позиция камеры вычисляется из Yaw и Radius
        public Vector3 Position => ComputePosition();
        public Vector3 Up => Vector3.UnitY;

        public Camera(Vector3 target, float initialY, float aspect, float initialRadius)
        {
            Target = target;
            FixedY = initialY;
            AspectRatio = aspect;
            Radius = MathHelper.Clamp(initialRadius, MinRadius, MaxRadius);
        }

        private Vector3 ComputePosition()
        {
            float yawRad = MathHelper.DegreesToRadians(Yaw);
            float x = Target.X + Radius * (float)Math.Cos(yawRad);
            float z = Target.Z + Radius * (float)Math.Sin(yawRad);
            float y = FixedY;
            return new Vector3(x, y, z);
        }

        public Matrix4 GetViewMatrix() => Matrix4.LookAt(Position, Target, Up);

        public Matrix4 GetProjectionMatrix() =>
            Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(Fov),
                AspectRatio,
                0.1f,
                100f
            );

        // Методы управления камерой
        public void RotateLeft(float deltaYaw) => Yaw -= deltaYaw;
        public void RotateRight(float deltaYaw) => Yaw += deltaYaw;

        public void ZoomIn(float deltaRadius)
        {
            Radius -= deltaRadius;
            Radius = MathHelper.Clamp(Radius, MinRadius, MaxRadius);
        }

        public void ZoomOut(float deltaRadius)
        {
            Radius += deltaRadius;
            Radius = MathHelper.Clamp(Radius, MinRadius, MaxRadius);
        }
    }
}