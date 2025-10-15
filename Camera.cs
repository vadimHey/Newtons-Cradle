using OpenTK.Mathematics;

namespace NewtonsCradle
{
    public class OrbitCamera
    {
        public Vector3 Target;
        public float Yaw = -90;
        public float Pitch = -10;
        public float Radius = 3.0f;
        public Vector3 Position => ComputePosition();
        public Vector3 Up => Vector3.UnitY;
        public float AspectRatio;
        public float Fov = 45f;

        public OrbitCamera(Vector3 position, Vector3 target, float aspect)
        {
            Target = target;
            AspectRatio = aspect;
            var dir = (target - position).Normalized();
            Yaw = MathHelper.RadiansToDegrees((float)Math.Atan2(dir.Z, dir.X));
            Pitch = MathHelper.RadiansToDegrees((float)Math.Asin(dir.Y));
            Radius = (position - target).Length;
        }

        Vector3 ComputePosition()
        {
            float yawR = MathHelper.DegreesToRadians(Yaw);
            float pitchR = MathHelper.DegreesToRadians(Pitch);
            float x = (float)(Target.X + Radius * Math.Cos(pitchR) * Math.Cos(yawR));
            float y = (float)(Target.Y + Radius * Math.Sin(pitchR));
            float z = (float)(Target.Z + Radius * Math.Cos(pitchR) * Math.Sin(yawR));
            return new Vector3(x, y, z);
        }

        public Matrix4 GetViewMatrix() => Matrix4.LookAt(Position, Target, Up);
        public Matrix4 GetProjectionMatrix() => Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Fov), AspectRatio, 0.1f, 100f);
    }
}