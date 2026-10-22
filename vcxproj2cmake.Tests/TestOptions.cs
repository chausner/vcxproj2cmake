namespace vcxproj2cmake.Tests;

static class TestOptions
{
    public static bool RunCMakeAssertions { get; } = Environment.GetEnvironmentVariable("RUN_CMAKE_ASSERTIONS") == "1";
}
