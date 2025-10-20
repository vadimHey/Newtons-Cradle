using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

namespace NewtonsCradle;

class Program
{
    static void Main(string[] args)
    {
        var gws = GameWindowSettings.Default;
        var nws = new NativeWindowSettings() 
        { 
            ClientSize = new Vector2i(1280, 720), 
            Title = "Newton's Cradle" 
        };

        var window = new SceneRenderer(gws, nws);
        window.Run();
    }
}