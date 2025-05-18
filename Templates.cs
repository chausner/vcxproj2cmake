namespace vcxproj2cmake;

static class Templates
{
    public const string CMakeLists = """
cmake_minimum_required(VERSION 3.0)

project({{ project_name }})

{{~ if qt_modules.size > 0 ~}}
find_package(Qt5 REQUIRED COMPONENTS {{ qt_modules | array.join " " }})
{{~ end -}}
{{~ for package in conan_packages ~}}
find_package({{ package.cmake_config_name }} REQUIRED CONFIG)
{{~ end ~}}

{{~ if configuration_type == "Application" ~}}
add_executable({{ project_name }}
{{~ else if configuration_type == "StaticLibrary" ~}}
add_library({{ project_name }} STATIC
{{~ else if configuration_type == "DynamicLibrary" ~}}
add_library({{ project_name }} SHARED
{{~ else ~}}
{{ error.unsupported_configuration_type }}
{{~ end ~}}
    {{ source_files | array.sort | array.join "\n" }}
)

{{~ if language_standard ~}}
target_compile_features({{ project_name }} PUBLIC {{ }}
    {{- if language_standard == "stdcpp20" -}} cxx_std_20
    {{- else if language_standard == "stdcpp17" -}} cxx_std_17
    {{- else if language_standard == "stdcpp14" -}} cxx_std_14
    {{- else if language_standard == "stdcpp11" -}} cxx_std_11
    {{- else -}}
    {{ error.unsupported_language_standard }}
    {{- end -}}
)

{{~ end -}}

{{~ if !include_paths.is_empty ~}}
target_include_directories({{ project_name }} PUBLIC
    {{ include_paths.common | array.join "\n" }}
    {{~ for path in include_paths.debug ~}}
    $<$<CONFIG:Debug>:{{ path }}>
    {{~ end ~}}
    {{~ for path in include_paths.release ~}}
    $<$<CONFIG:Release>:{{ path }}>
    {{~ end ~}}
    {{~ for path in include_paths.x86 ~}}
    $<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x86>:{{ path }}>
    {{~ end ~}}
    {{~ for path in include_paths.x64 ~}}
    $<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x64>:{{ path }}>
    {{~ end ~}}
)

{{~ end -}}

{{~ if !defines.is_empty ~}}
target_compile_definitions({{ project_name }} PUBLIC
    {{ defines.common | array.join "\n" }}
    {{~ for define in defines.debug ~}}
    $<$<CONFIG:Debug>:{{ define }}>
    {{~ end ~}}
    {{~ for define in defines.release ~}}
    $<$<CONFIG:Release>:{{ define }}>
    {{~ end ~}}
    {{~ for define in defines.x86 ~}}
    $<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x86>:{{ define }}>
    {{~ end ~}}
    {{~ for define in defines.x64 ~}}
    $<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x64>:{{ define }}>
    {{~ end ~}}
)

{{~ end -}}

{{~ if !libraries.is_empty || qt_modules.size > 0 ~}}
target_link_libraries({{ project_name }} PUBLIC
    {{ libraries.common | array.join "\n" }}
    {{~ for library in libraries.debug ~}}
    $<$<CONFIG:Debug>:{{ library }}>
    {{~ end ~}}
    {{~ for library in libraries.release ~}}
    $<$<CONFIG:Release>:{{ library }}>
    {{~ end ~}}
    {{~ for library in libraries.x86 ~}}
    $<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x86>:{{ library }}>
    {{~ end ~}}
    {{~ for library in libraries.x64 ~}}
    $<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x64>:{{ library }}>
    {{~ end ~}}
    {{~ for module in qt_modules ~}}
    Qt5::{{ module }}
    {{~ end ~}}
    {{~ for package in conan_packages ~}}
    {{ package.cmake_target_name }}
    {{~ end ~}}
)

{{~ end -}}

{{~ if !options.is_empty ~}}
target_compile_options({{ project_name }} PUBLIC
    {{ options.common | array.join "\n" }}
    {{~ for option in options.debug ~}}
    $<$<CONFIG:Debug>:{{ option }}>
    {{~ end ~}}
    {{~ for option in options.release ~}}
    $<$<CONFIG:Release>:{{ option }}>
    {{~ end ~}}
    {{~ for option in options.x86 ~}}
    $<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x86>:{{ option }}>
    {{~ end ~}}
    {{~ for option in options.x64 ~}}
    $<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x64>:{{ option }}>
    {{~ end ~}}
)

{{~ end -}}

""";
}
