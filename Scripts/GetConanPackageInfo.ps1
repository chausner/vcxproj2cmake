Push-Location Temp:

git clone https://github.com/conan-io/conan-center-index.git --depth 1

Get-ChildItem conan-center-index -Filter conanfile.py -Recurse | foreach { 
    $packageName = $_.Directory.Parent.Name
    $content = Get-Content $_ -Raw
    
    if ($content -match "self\.cpp_info\.set_property\(`"cmake_target_name`", `"([^`"]*)`"\)") { 
        $cmakeTargetName = $Matches[1]
    } else {
        $cmakeTargetName = $null
    }

    if ($content -match "self\.cpp_info\.set_property\(`"cmake_file_name`", `"([^`"]*)`"\)") {
        $cmakeFileName = $Matches[1] 
    } else {
        $cmakeFileName = $null
    }

    if ($packageName -in "cmake_package","header_only") {
        return
    }

    if ($cmakeTargetName -or $cmakeFileName) { 
        "$packageName,$cmakeFileName,$cmakeTargetName"
    } 
} | Sort-Object -Stable -Unique -Property { $_ -split "," } | Out-File -FilePath $PSScriptRoot\..\vcxproj2cmake\Resources\conan-packages.csv -Encoding utf8

Remove-Item -Recurse -Force conan-center-index

Pop-Location