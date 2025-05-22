record QtModule(string ModuleName, string CMakeComponentName, string CMakeTargetName);

class QtModuleInfoRepository
{
    public static QtModule GetQtModuleInfo(string moduleName)
    {
        if (!qtModuleToCMakeComponentName.TryGetValue(moduleName, out var cmakeComponentName))
            throw new CatastrophicFailureException($"Unknown Qt module: {moduleName}");

        return new QtModule(moduleName, cmakeComponentName, "Qt5::" + cmakeComponentName);
    }

    static readonly Dictionary<string, string> qtModuleToCMakeComponentName = new()
    {
        { "3danimation",      "3DAnimation" },
        { "3dcore",           "3DCore" },
        { "3dextras",         "3DExtras" },
        { "3dinput",          "3DInput" },
        { "3dlogic",          "3DLogic" },
        { "3dquick",          "3DQuick" },
        { "3dquickanimation", "3DQuickAnimation" },
        { "3dquickextras",    "3DQuickExtras" },
        { "3dquickinput",     "3DQuickInput" },
        { "3dquickrender",    "3DQuickRender" },
        { "3dquickscene2d",   "3DQuickScene2D" },
        { "androidextras",    "AndroidExtras" },
        { "axcontainer",      "AxContainer" },
        { "axserver",         "AxServer" },
        { "bluetooth",        "Bluetooth" },
        { "charts",           "Charts" },
        { "concurrent",       "Concurrent" },
        { "core",             "Core" },
        { "datavisualization","DataVisualization" },
        { "dbus",             "DBus" },
        { "gamepad",          "Gamepad" },
        { "graphicaleffects", "GraphicalEffects" },
        { "gui",              "Gui" },
        { "help",             "Help" },
        { "location",         "Location" },
        { "lottieanimation",  "LottieAnimation" },
        { "macextras",        "MacExtras" },
        { "multimedia",       "Multimedia" },
        { "multimediawidgets","MultimediaWidgets" },
        { "network",          "Network" },
        { "networkauth",      "NetworkAuth" },
        { "nfc",              "Nfc" },
        { "opengl",           "OpenGL" },
        { "platformheaders",  "PlatformHeaders" },
        { "positioning",      "Positioning" },
        { "printsupport",     "PrintSupport" },
        { "purchasing",       "Purchasing" },
        { "qml",              "Qml" },
        { "quick",            "Quick" },
        { "quickcontrols",    "QuickControls" },
        { "quickcontrols2",   "QuickControls2" },
        { "quickdialogs",     "QuickDialogs" },
        { "quicklayouts",     "QuickLayouts" },
        { "quicktest",        "QuickTest" },
        { "quickwidgets",     "QuickWidgets" },
        { "remoteobjects",    "RemoteObjects" },
        { "script",           "Script" },
        { "scripttools",      "ScriptTools" },
        { "scxml",            "Scxml" },
        { "sensors",          "Sensors" },
        { "serialbus",        "SerialBus" },
        { "serialport",       "SerialPort" },
        { "sql",              "Sql" },
        { "svg",              "Svg" },
        { "testlib",          "Test" },
        { "texttospeech",     "TextToSpeech" },
        { "virtualkeyboard",  "VirtualKeyboard" },
        { "webchannel",       "WebChannel" },
        { "webengine",        "WebEngine" },
        { "webenginecore",    "WebEngineCore" },
        { "webenginewidgets", "WebEngineWidgets" },
        { "websockets",       "WebSockets" },
        { "webview",          "WebView" },
        { "widgets",          "Widgets" },
        { "winextras",        "WinExtras" },
        { "winuiextras",      "WinExtras" },   // accepted alias
        { "x11extras",        "X11Extras" },
        { "xml",              "Xml" },
        { "xmlpatterns",      "XmlPatterns" }
    };
}
