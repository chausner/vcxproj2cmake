git clone https://github.com/conan-io/conan-center-index.git
Set-Location conan-center-index

Get-ChildItem -Recurse conanfile.py | foreach { 
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

    if ($cmakeTargetName -or $cmakeFileName) { 
        "$packageName,$cmakeFileName,$cmakeTargetName"
    } 
}