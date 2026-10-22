namespace vcxproj2cmake;

record QtModule(string ModuleName, string CMakeComponentName, string CMakeTargetName);

class QtModuleInfoRepository
{
    public static QtModule GetQtModuleInfo(string moduleName, int qtVersion)
    {
        var qtModuleToCMakeComponentName = qtVersion switch
        {
            5 => qt5ModuleToCMakeComponentName,
            6 => qt6ModuleToCMakeComponentName,
            _ => throw new ArgumentException("Only Qt 5 and 6 are supported", nameof(qtVersion))
        };

        if (!qtModuleToCMakeComponentName.TryGetValue(moduleName, out var cmakeComponentName))
            throw new CatastrophicFailureException($"Unknown Qt module: {moduleName}");

        return new QtModule(moduleName, cmakeComponentName, $"Qt{qtVersion}::{cmakeComponentName}");
    }

    static readonly Dictionary<string, string> qt5ModuleToCMakeComponentName = new()
    {
        // Core & foundation
        { "core", "Core" },
        { "gui", "Gui" },
        { "widgets", "Widgets" },
        { "network", "Network" },
        { "concurrent", "Concurrent" },
        { "dbus", "DBus" },
        { "test", "Test" },
        { "testlib", "Test" }, // alias
        { "sql", "Sql" },
        { "xml", "Xml" },
        { "xmlpatterns", "XmlPatterns" },
        { "statemachine", "StateMachine" },

        // QML & Quick
        { "qml", "Qml" },
        { "quick", "Quick" },
        { "quickwidgets", "QuickWidgets" },
        { "quicktest", "QuickTest" },
        { "quickcontrols2", "QuickControls2" },
        { "quicktemplates2", "QuickTemplates2" },

        // Qt 3D
        { "3dcore", "3DCore" },
        { "3drender", "3DRender" },
        { "3dinput", "3DInput" },
        { "3dlogic", "3DLogic" },
        { "3danimation", "3DAnimation" },
        { "3dextras", "3DExtras" },
        { "3dquick", "3DQuick" },
        { "3dquickrender", "3DQuickRender" },
        { "3dquickinput", "3DQuickInput" },
        { "3dquickextras", "3DQuickExtras" },
        { "3dquickanimation", "3DQuickAnimation" },
        { "3dquickscene2d", "3DQuickScene2D" },

        // Multimedia & graphics
        { "multimedia", "Multimedia" },
        { "multimediawidgets", "MultimediaWidgets" },
        { "opengl", "OpenGL" },
        { "svg", "Svg" },
        { "datavisualization", "DataVisualization" },
        { "charts", "Charts" },

        // Connectivity & hardware
        { "bluetooth", "Bluetooth" },
        { "nfc", "Nfc" },
        { "positioning", "Positioning" },
        { "location", "Location" },
        { "sensors", "Sensors" },
        { "serialport", "SerialPort" },
        { "serialbus", "SerialBus" },

        // Networking & web
        { "networkauth", "NetworkAuth" },
        { "websockets", "WebSockets" },
        { "webchannel", "WebChannel" },
        { "webengine", "WebEngine" },
        { "webenginecore", "WebEngineCore" },
        { "webenginewidgets", "WebEngineWidgets" },
        { "webenginequick", "WebEngineQuick" },
        { "webview", "WebView" },

        // Data, formats & display
        { "help", "Help" },
        { "pdf", "Pdf" },
        { "pdfwidgets", "PdfWidgets" },
        { "designer", "Designer" },

        // System, IPC & Wayland
        { "printsupport", "PrintSupport" },
        { "remoteobjects", "RemoteObjects" },
        { "scxml", "Scxml" },
        { "uitools", "UiTools" },
        { "waylandclient", "WaylandClient" },
        { "waylandcompositor", "WaylandCompositor" },

        // Platform extras
        { "winextras", "WinExtras" },
        { "winuiextras", "WinExtras" }, // alias
        { "macextras", "MacExtras" },
        { "x11extras", "X11Extras" },
        { "androidextras", "AndroidExtras" },

        // Add-ons
        { "gamepad", "Gamepad" },
        { "purchasing", "Purchasing" },
        { "virtualkeyboard", "VirtualKeyboard" },
        { "texttospeech", "TextToSpeech" },

        // Deprecated scripting
        { "script", "Script" },
        { "scripttools", "ScriptTools" },

        // Industrial & IoT
        { "opcua", "OpcUa" },
        { "mqtt", "Mqtt" },
        { "coap", "Coap" },

        // ActiveQt
        { "axcontainer", "AxContainer" },
        { "axserver", "AxServer" }
    };

    static readonly Dictionary<string, string> qt6ModuleToCMakeComponentName = new()
    {
        // Core & foundation
        { "core", "Core" },
        { "core5compat", "Core5Compat" },
        { "gui", "Gui" },
        { "widgets", "Widgets" },
        { "network", "Network" },
        { "concurrent", "Concurrent" },
        { "dbus", "DBus" },
        { "test", "Test" },
        { "testlib", "Test" }, // alias
        { "sql", "Sql" },
        { "xml", "Xml" },
        { "statemachine", "StateMachine" },

        // QML & Quick
        { "qml", "Qml" },
        { "qmlmodels", "QmlModels" },
        { "qmlworkerscript", "QmlWorkerScript" },
        { "quick", "Quick" },
        { "quickwidgets", "QuickWidgets" },
        { "quicklayouts", "QuickLayouts" },
        { "quickparticles", "QuickParticles" },
        { "quickshapes", "QuickShapes" },
        { "quicktest", "QuickTest" },
        { "quicktimeline", "QuickTimeline" },
        { "quickeffects", "QuickEffects" },
        { "quickcontrols2", "QuickControls2" },
        { "quickdialogs2", "QuickDialogs2" },
        { "quicktemplates2", "QuickTemplates2" },

        // Qt Quick 3D
        { "quick3d", "Quick3D" },
        { "quick3dassetimport", "Quick3DAssetImport" },
        { "quick3dphysics", "Quick3DPhysics" },
        { "quick3dutils", "Quick3DUtils" },

        // Qt 3D
        { "3dcore", "3DCore" },
        { "3drender", "3DRender" },
        { "3dinput", "3DInput" },
        { "3dlogic", "3DLogic" },
        { "3danimation", "3DAnimation" },
        { "3dextras", "3DExtras" },
        { "3dquick", "3DQuick" },
        { "3dquickrender", "3DQuickRender" },
        { "3dquickinput", "3DQuickInput" },
        { "3dquickextras", "3DQuickExtras" },
        { "3dquickanimation", "3DQuickAnimation" },
        { "3dquickscene2d", "3DQuickScene2D" },

        // Multimedia & graphics
        { "multimedia", "Multimedia" },
        { "multimediawidgets", "MultimediaWidgets" },
        { "opengl", "OpenGL" },
        { "openglwidgets", "OpenGLWidgets" },
        { "svg", "Svg" },
        { "svgwidgets", "SvgWidgets" },
        { "shadertools", "ShaderTools" },
        { "spatialaudio", "SpatialAudio" },
        { "graphs", "Graphs" },
        { "graphswidgets", "GraphsWidgets" },
        { "datavisualization", "DataVisualization" },
        { "charts", "Charts" },
        { "pdf", "Pdf" },
        { "pdfwidgets", "PdfWidgets" },
        { "designer", "Designer" },

        // Connectivity & hardware
        { "bluetooth", "Bluetooth" },
        { "nfc", "Nfc" },
        { "positioning", "Positioning" },
        { "location", "Location" },
        { "sensors", "Sensors" },
        { "serialport", "SerialPort" },
        { "serialbus", "SerialBus" },

        // Networking & web
        { "networkauth", "NetworkAuth" },
        { "httpserver", "HttpServer" },
        { "websockets", "WebSockets" },
        { "webchannel", "WebChannel" },
        { "webengine", "WebEngine" },
        { "webenginecore", "WebEngineCore" },
        { "webenginewidgets", "WebEngineWidgets" },
        { "webenginequick", "WebEngineQuick" },
        { "webview", "WebView" },
        { "grpc", "Grpc" },
        { "protobuf", "Protobuf" },

        // System, IPC & Wayland
        { "printsupport", "PrintSupport" },
        { "remoteobjects", "RemoteObjects" },
        { "scxml", "Scxml" },
        { "uitools", "UiTools" },
        { "waylandclient", "WaylandClient" },
        { "waylandcompositor", "WaylandCompositor" },

        // UI & UX
        { "virtualkeyboard", "VirtualKeyboard" },
        { "texttospeech", "TextToSpeech" },
        { "lottieanimation", "LottieAnimation" },
        { "help", "Help" },

        // Industrial & IoT
        { "opcua", "OpcUa" },
        { "mqtt", "Mqtt" },
        { "coap", "Coap" },

        // Automotive / IF
        { "interfaceframework", "InterfaceFramework" },
        { "ifservicemanager", "IfServiceManager" },

        // ActiveQt
        { "axcontainer", "AxContainer" },
        { "axserver", "AxServer" }
    };
}
