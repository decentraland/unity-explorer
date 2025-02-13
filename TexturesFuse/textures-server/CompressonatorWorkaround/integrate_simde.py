from io import IOBase
import sys
import os
import re
import fileinput
import glob
import subprocess
import shutil

import importlib.util

def replace_in_file(file_path, old_string, new_string):
    try:
        with fileinput.FileInput(file_path, inplace=True, backup='.bak') as file:
            for line in file:
                print(re.sub(old_string, new_string, line), end='')
    except Exception as e:
        print(f'something went wrong in file: {file_path} - {str(e)}\n', file=sys.stderr)
        os.rename(f'{file_path}.bak', file_path)
    finally:
        try:
            os.remove(f'{file_path}.bak')
        except OSError:
            pass

def replace_in_directory(directory : str, old_string : str,  new_string : str, extensions : list):
    for dirpath, _, filenames in os.walk(directory):
        for filename in filenames:
            if extensions:
                if not any(filename.endswith(ext) for ext in extensions):
                    continue
            print(f"    - Performing {filename}")
            file_path = os.path.join(dirpath, filename)
            replace_in_file(file_path, old_string, new_string)

def write_simde(file: IOBase, project_root: str):
    simde_root_path = f"{project_root}externals_repos/simde/simde/"
    
    content = f'\ninclude_directories({simde_root_path} {simde_root_path}x86 {simde_root_path}x86/avx512 {simde_root_path}arm {simde_root_path}mips {project_root}common/lib/ext/glm/gtx {project_root}common/lib/ext/glm/gtc {project_root}common/lib/ext/glm {project_root}common/lib/ext )\n'
    
    file.write(content)
    file.write("""
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/../cmp_core/shaders)
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/../cmp_core/source)
               """)

def link_opencv(file: IOBase, include_target_flag: bool):
    libs = "${OpenCV_LIBS}"
    opencv_dirs = "${OpenCV_INCLUDE_DIRS}"
    
    target_link = f"target_link_libraries(CMP_Core PRIVATE {libs})\n" if include_target_flag else ""
    
    content = f"\nfind_package(OpenCV REQUIRED)\ninclude_directories({opencv_dirs})\n{target_link}"
    file.write(content)
    
def insert_at_start(file_path: str, content: str):
    original = ""
    with open(file_path, 'r') as file:
        original = file.read()
    
    with open(file_path, 'w') as file:
        file.write(content + original)
        
def insert_at_end(file_path: str, content: str):
    original = ""
    with open(file_path, 'r') as file:
        original = file.read()
    
    with open(file_path, 'w') as file:
        file.write(original + content)
        
def set_vars(file_path: str):
    content = """
set(COMPRESSONATOR_ROOT_PATH ${CMAKE_CURRENT_SOURCE_DIR}/..)
set(PROJECT_FOLDER_SDK "SDK")
set(PROJECT_FOLDER_SDK_LIBS "${CMAKE_CURRENT_SOURCE_DIR}/Libraries")

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED True)

#Mac Only
#xcrun --show-sdk-path
set(CMAKE_OSX_SYSROOT "/Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk")
set(MACOSX_DEPLOYMENT_TARGET 10.11)
set(CMAKE_OSX_ARCHITECTURES "x86_64;arm64")
#add default headers
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -isysroot ${CMAKE_OSX_SYSROOT} -mmacosx-version-min=${MACOSX_DEPLOYMENT_TARGET}")
    """
    
    insert_at_start(file_path, content)
    
def append_line_to_cmake():

    with open('compressonator/CMakeLists.txt', 'a') as file:
        write_simde(file, '${CMAKE_CURRENT_SOURCE_DIR}/../')
        link_opencv(file, False)        
        
    framework_make = 'compressonator/cmp_framework/CMakeLists.txt'
    set_vars(framework_make)
    insert_at_start(framework_make, 'set(PROJECT_SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/..")')
    insert_at_end(framework_make,"""
target_include_directories(CMP_Framework 
    PUBLIC 
    ${CMAKE_CURRENT_SOURCE_DIR}/../../externals_repos/simde/simde/x86
    ${CMAKE_CURRENT_SOURCE_DIR}/../../externals_repos/simde/simde/x86/AVX512

    ${CMAKE_CURRENT_SOURCE_DIR}/../cmp_core/source
    ${CMAKE_CURRENT_SOURCE_DIR}/../cmp_core/shaders
    
    if (NOT TARGET anylog)
        add_subdirectory("../../../AnyLog" anylog_build)
    endif()
    target_link_libraries(CMP_Framework PRIVATE anylog)
    target_include_directories(CMP_Framework PRIVATE "../../../AnyLog")
)
""")
    
    insert_at_start('compressonator/external/stb/stb_image.h', '#include "sse2.h"')
    

    core_make = 'compressonator/cmp_core/CMakeLists.txt'
    set_vars(core_make)
    with open(core_make, 'a') as file:
        write_simde(file, '${CMAKE_CURRENT_SOURCE_DIR}/../../')
        #link_opencv(file, True)

if __name__ == "__main__":    
    
    directory_path = './compressonator'
    
    headers = '"avx.h"\n#include "avx512.h"\n#include "loadu.h"\n#include "set1.h"\n#include "sse.h"'
    
    pairs = [
        ('__m256', 'simde__m256'),
        ('__m128', 'simde__m128'),
        ('__m512', 'simde__m512'),
        (' _mm128_', ' simde_mm128_'),
        (' _mm256_', ' simde_mm256_'),
        (' _mm512_', ' simde_mm512_'),
        (' _mm', ' simde_mm'),
        
        ('_CMP_GT_OQ', 'SIMDE_CMP_GT_OQ'),
        
         # Add header
        ('<smmintrin.h>', headers),
        ('<xmmintrin.h>', headers),
        ('<immintrin.h>', headers),
        
        # GLM
        ('<glm/mat4x4.hpp>','"mat4x4.hpp"'),
        ('<glm/vec4.hpp>','"vec4.hpp"'),
        ('<glm/gtx/transform.hpp>','"gtx/transform.hpp"'),
        ('<glm/matrix.hpp>','"matrix.hpp"'),
        
        #disable march's
        ('target_compile_options(CMP_Core_AVX512 PRIVATE -march=knl)', ' '), # Remove knl
        ('-march=knl', ''),
        ('-march=haswell', ''),
        ('-march=nehalem', ''),
        
        #Remove threaded compress
        ('#define THREADED_COMPRESS', ''), #TODO back?
        
        #Fix plugins location
        ('g_pluginManager.getPluginList(".", TRUE)', 'g_pluginManager.getPluginList("./plugins", TRUE)')
    ]
    
    #file_name_target, from_path
    #root dir: custom_files
    custom_files = [
        ("compressonator/cmp_framework/compute_base.cpp", "compute_base.cpp"),
        ("compressonator/applications/_plugins/common/pluginmanager.cpp", "pluginmanager.cpp"),
        ("compressonator/cmp_compressonatorlib/CMakeLists.txt", "libCMakeLists.txt")
    ]
    
    allowed_extensions = ['cpp','c','h','txt','make','cmake']
    
    #clear
    result = subprocess.run(['git', 'reset', '--hard', 'HEAD'], cwd=directory_path, capture_output=True, text=True)
    print(result.stdout)
    
    for old_str, new_str in pairs:
        print(f'Replacing {old_str} to {new_str}')
        replace_in_directory(directory_path, old_str, new_str, allowed_extensions)
        
    bakFiles = glob.glob(os.path.join(directory_path, '**/*.bak'))
    for bak in bakFiles:
        os.remove(bak)
        
    for target_path, origin_path in custom_files:
        from_path = os.path.join('./custom_files', origin_path)
        to_path = os.path.join('./', target_path)
        print(f"Copying content from {from_path} to {to_path}")
        shutil.copy(from_path, to_path)
        
    append_line_to_cmake()
    
        