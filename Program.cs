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
            Title = "Маятник Ньютона на столе" 
        };

        var window = new MainWindow(gws, nws);
        window.Run();
    }
}