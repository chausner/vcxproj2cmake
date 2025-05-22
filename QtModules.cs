internal class QtModules
{
    private static Dictionary<string, string> qtModuleToCMakeComponent = new()
    {
        { "3danimation", "3DAnimation" },
        { "3dcore", "3DCore" },
        { "3dextras", "3DExtras" },
        { "3dinput", "3DInput" },
        { "3dlogic", "3DLogic" },
        { "3dquick", "3DQuick" },
        { "3drender", "3DRender" },
        { "axcontainer", "AxContainer" },
        { "axserver", "AxServer" },
        { "bluetooth", "Bluetooth" },
        { "concurrent", "Concurrent" },
        { "core", "Core" },
        { "dbus", "DBus" },
        { "gamepad", "Gamepad" },
        { "gui", "Gui" },
        { "help", "Help" },
        { "location", "Location" },
        { "multimedia", "Multimedia" },
        { "multimediawidgets", "MultimediaWidgets" },
        { "network", "Network" },
        { "nfc", "Nfc" },
        { "opengl", "OpenGL" },
        { "openglextensions", "OpenGLExtensions" },
        { "positioning", "Positioning" },
        { "printsupport", "PrintSupport" },
        { "qml", "Qml" },
        { "quick", "Quick" },
        { "quickcontrols2", "QuickControls2" },
        { "quickwidgets", "QuickWidgets" },
        { "remoteobjects", "RemoteObjects" },
        { "scxml", "Scxml" },
        { "sensors", "Sensors" },
        { "serialbus", "SerialBus" },
        { "serialport", "SerialPort" },
        { "sql", "Sql" },
        { "svg", "Svg" },
        { "uitools", "UiTools" },
        { "webchannel", "WebChannel" },
        { "websockets", "WebSockets" },
        { "webview", "WebView" },
        { "widgets", "Widgets" },
        { "winextras", "WinExtras" },
        { "xml", "Xml" },
        { "xmlpatterns", "XmlPatterns" }
    };

    public static string GetCMakeComponentForQtModule(string qtModule)
    {
        return qtModuleToCMakeComponent[qtModule];
    }

    public static string GetCMakeTargetForQtModule(string qtModule)
    {
        return "Qt5::" + GetCMakeComponentForQtModule(qtModule);
    }
}
